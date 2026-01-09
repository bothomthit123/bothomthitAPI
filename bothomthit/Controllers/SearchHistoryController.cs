using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bothomthit.Models;

namespace TourismApp.Api.Controllers;

[ApiController]
[Route("me/search-history")]
[Authorize] // yêu cầu JWT, lấy AccountId từ token
public class SearchHistoryController : ControllerBase
{
    private readonly AppDbContext _db;
    public SearchHistoryController(AppDbContext db) => _db = db;

    private int UserId()
    {
        var raw = User.FindFirstValue("account_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var id) || id <= 0)
            throw new UnauthorizedAccessException("Invalid token");
        return id;
    }

    //Lấy lịch sử tìm kiếm
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var uid = UserId();
        page = page <= 0 ? 1 : page;
        size = size <= 0 ? 20 : Math.Min(size, 100);

        var q = _db.SearchHistories.AsNoTracking()
            .Where(h => h.AccountId == uid)
            .OrderByDescending(h => h.SearchDateUtc);

        var total = await q.CountAsync(ct);
        var data = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return Ok(new { data, pagination = new { page, size, total } });
    }

    public sealed class AddHistoryRequest
    {
        public string Keyword { get; set; } = string.Empty;
    }

    //Thêm lịch sử tìm kiếm
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddHistoryRequest req, CancellationToken ct)
    {
        var uid = UserId();
        if (string.IsNullOrWhiteSpace(req.Keyword))
            return BadRequest(new { error = "keyword_required" });

        _db.SearchHistories.Add(new SearchHistory
        {
            AccountId = uid,
            Keyword = req.Keyword.Trim(),
            // SearchDateUtc dùng DEFAULT SYSUTCDATETIME() trong DB
        });

        await _db.SaveChangesAsync(ct);
        return StatusCode(201);
    }

    //Xóa lịch sử tìm kiếm
    [HttpDelete("{historyId:int}")]
    public async Task<IActionResult> Delete(int historyId, CancellationToken ct)
    {
        var uid = UserId();
        var h = await _db.SearchHistories.FirstOrDefaultAsync(x => x.HistoryId == historyId && x.AccountId == uid, ct);
        if (h == null) return NotFound();

        _db.SearchHistories.Remove(h);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

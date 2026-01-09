using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using bothomthit.Models;

namespace TourismApp.Api.Controllers;

[Authorize] // Bắt buộc phải có Token
[Route("api/[controller]")]
[ApiController]
public class SecureDocumentController : ControllerBase
{
    private readonly AppDbContext _db;

    public SecureDocumentController(AppDbContext db)
    {
        _db = db;
    }

    // Helper lấy ID người dùng từ Token
    private int GetUserId()
    {
        var claim = User.FindFirst("account_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }

    //  Lấy danh sách giấy tờ của tôi
    [HttpGet]
    public async Task<IActionResult> GetMyDocuments()
    {
        var uid = GetUserId();

        var docs = await _db.SecureDocuments.AsNoTracking()
            .Where(d => d.AccountId == uid)
            .OrderByDescending(d => d.IsPinned) // Những cái ghim (quan trọng) lên đầu
            .ThenBy(d => d.Title)
            .ToListAsync();

        return Ok(new { data = docs });
    }

    //  Xem chi tiết 1 giấy tờ
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocument(int id)
    {
        var uid = GetUserId();
        var doc = await _db.SecureDocuments.FirstOrDefaultAsync(d => d.DocId == id && d.AccountId == uid);

        if (doc == null) return NotFound(new { error = "Không tìm thấy tài liệu hoặc bạn không có quyền truy cập." });

        return Ok(new { data = doc });
    }

    //  Thêm giấy tờ mới
    [HttpPost]
    public async Task<IActionResult> AddDocument([FromBody] CreateSecureDocRequest req)
    {
        var uid = GetUserId();

        var doc = new SecureDocument
        {
            AccountId = uid,
            Title = req.Title,
            DocType = req.DocType,
            ImageUrl = req.ImageUrl, // Client upload ảnh lên Cloudinary rồi gửi link về đây
            ExpiryDate = req.ExpiryDate,
            IsPinned = req.IsPinned
        };

        _db.SecureDocuments.Add(doc);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDocument), new { id = doc.DocId }, new { data = doc });
    }

    //  Cập nhật thông tin (Sửa tên, ghim, ngày hết hạn)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDocument(int id, [FromBody] CreateSecureDocRequest req)
    {
        var uid = GetUserId();
        var doc = await _db.SecureDocuments.FirstOrDefaultAsync(d => d.DocId == id && d.AccountId == uid);

        if (doc == null) return NotFound();

        doc.Title = req.Title;
        doc.DocType = req.DocType;
        doc.ImageUrl = req.ImageUrl;
        doc.ExpiryDate = req.ExpiryDate;
        doc.IsPinned = req.IsPinned;

        await _db.SaveChangesAsync();

        return Ok(new { data = doc });
    }

    //  Xóa giấy tờ
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var uid = GetUserId();
        var doc = await _db.SecureDocuments.FirstOrDefaultAsync(d => d.DocId == id && d.AccountId == uid);

        if (doc == null) return NotFound();

        _db.SecureDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
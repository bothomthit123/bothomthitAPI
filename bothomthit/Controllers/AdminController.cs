using bothomthit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace TourismApp.Api.Controllers;

[ApiController]
[Route("api/admin")]
// Chỉ cho phép Role là Admin truy cập toàn bộ Controller này
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db) => _db = db;

    // 1. Lấy danh sách tất cả tài khoản
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAllAccounts()
    {
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Select(a => new
            {
                a.AccountId,
                a.Name,
                a.Email,
                a.Role,
                a.IsLocked, 
                a.CreatedAtUtc
            })
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();

        return Ok(new { data = accounts });
    }

    // 2. Khóa / Mở khóa tài khoản
    [HttpPost("accounts/{id}/toggle-lock")]
    public async Task<IActionResult> ToggleLockAccount(int id)
    {
        var acc = await _db.Accounts.FindAsync(id);
        if (acc == null) return NotFound();

        if (acc.Role == "Admin")
            return BadRequest(new { error = "cannot_lock_admin" });

        // Đảo ngược trạng thái khóa 
        acc.IsLocked = !acc.IsLocked;

        await _db.SaveChangesAsync();
        return Ok(new { message = acc.IsLocked ? "Locked" : "Unlocked", isLocked = acc.IsLocked });
    }

    // 3. Lấy danh sách TOÀN BỘ quảng cáo (để Admin duyệt/xóa)
    [HttpGet("advertisements/all")]
    public async Task<IActionResult> GetAllAds()
    {
        var ads = await _db.Advertisements
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
        // a là Account, ad là Advertisement, place là Place
            .Join(_db.Places,
                ad => ad.PlaceId,      
                place => place.PlaceId, 
                (ad, place) => new      
                {
                    ad.AdId,
                    ad.Title,
                    ad.BannerImageUrl,
                    SupplierName = place.Name, 
                    ad.StartUtc,
                    ad.EndUtc,
                    ad.CreatedAtUtc
                })
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();

        return Ok(new { data = ads });
    }
}
using bothomthit.Models; // Đảm bảo namespace này đúng
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using bothomthit.Models.DTOs; // Để dùng DTO mới

using Microsoft.AspNetCore.Http;

namespace TourismApp.Api.Controllers;

[ApiController]
[Route("api/advertisements")]
public class AdvertisementsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdvertisementsController(AppDbContext db)
    {
        _db = db;
    }

    // Hàm helper để lấy tài khoản từ token
    private async Task<Account?> _GetAccountFromClaims(ClaimsPrincipal user)
    {
        var claimsUserIdString = user.FindFirstValue("account_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claimsUserIdString, out var claimsUserId))
        {
            return null;
        }
        return await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == claimsUserId);
    }
    // GET: /api/advertisements/active-in-bounds
    // Lấy các quảng cáo đang hoạt động trong một khung nhìn bản đồ
    [HttpGet("active-in-bounds")]
    [AllowAnonymous] // Cho phép mọi người xem quảng cáo
    public async Task<IActionResult> GetActiveAdsInBounds(
        [FromQuery] double north,
        [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west
    )
    {
        var now = DateTime.UtcNow;

        // 1. Lọc Advertisements đang hoạt động (chưa xóa, đúng ngày)
        // 2. JOIN với bảng Places để lấy tọa độ
        // 3. Lọc theo tọa độ (bbox)
        // 4. Chọn (Select) các trường cần thiết vào DTO

        var adsInBounds = await _db.Advertisements
            .AsNoTracking()
            .Where(ad =>
                !ad.IsDeleted &&
                ad.StartUtc <= now &&
                ad.EndUtc >= now
            )
            .Join(_db.Places, // JOIN với bảng Places
                ad => ad.PlaceId,     // Khóa ngoại từ Advertisement
                place => place.PlaceId, // Khóa chính từ Place
                (ad, place) => new { Ad = ad, Place = place } // Kết quả join tạm thời
            )
            .Where(joined => // Lọc theo khung nhìn (bbox)
                joined.Place.Latitude >= south &&
                joined.Place.Latitude <= north &&
                joined.Place.Longitude >= west &&
                joined.Place.Longitude <= east
            )
            .Select(joined => new AdvertisementMarkerDto // Chuyển đổi sang DTO
            {
                AdId = joined.Ad.AdId,
                PlaceId = joined.Ad.PlaceId,
                Title = joined.Ad.Title,
                BannerImageUrl = joined.Ad.BannerImageUrl,
                Latitude = joined.Place.Latitude,   // Lấy từ Place
                Longitude = joined.Place.Longitude  // Lấy từ Place
            })
            .ToListAsync(); // Thực thi query

        // Giới hạn số lượng để tránh quá tải client
        var limitedAds = adsInBounds.Take(50).ToList();

        return Ok(new { data = limitedAds });
    }

    // === Endpoint để lấy chi tiết 1 Ad ===

    // GET: /api/advertisements/{adId}
    [HttpGet("{adId:int}")]
    [AllowAnonymous] // Cho phép mọi người xem chi tiết quảng cáo
    public async Task<IActionResult> GetAdDetails(int adId)
    {
        var adDetail = await _db.Advertisements
            .AsNoTracking()
            .Where(a => a.AdId == adId && !a.IsDeleted)
            .Join(_db.Places, // Join với bảng Places
                ad => ad.PlaceId,
                place => place.PlaceId,
                (ad, place) => new AdvertisementDetailDto // Ánh xạ sang DTO
                {
                    Title = ad.Title,
                    Description = ad.Description,
                    BannerImageUrl = ad.BannerImageUrl,
                    StartUtc = ad.StartUtc,
                    EndUtc = ad.EndUtc,
                    PlaceId = place.PlaceId,
                    PlaceName = place.Name, // Lấy từ Place
                    PlaceAddress = place.Address, // Lấy từ Place
                    Latitude = place.Latitude,
                    Longitude = place.Longitude
                })
            .FirstOrDefaultAsync(); // Lấy 1 bản ghi

        if (adDetail == null)
        {
            return NotFound(new { error = "advertisement_not_found" });
        }

        return Ok(new { data = adDetail });
    }

   
    // ===  Endpoint cho HomePage ===

    // GET: /api/advertisements/active
    // Lấy quảng cáo đang hoạt động, mới nhất
    [HttpGet("active")]
    [AllowAnonymous] // Cho phép mọi người xem quảng cáo
    public async Task<IActionResult> GetActiveAds()
    {
        var now = DateTime.UtcNow;

        var activeAds = await _db.Advertisements
            .AsNoTracking()
            .Where(ad =>
                !ad.IsDeleted &&
                ad.StartUtc <= now &&
                ad.EndUtc >= now
            )
            
            .OrderByDescending(ad => ad.CreatedAtUtc)
            .Take(20)
            // join với Place để lấy tọa độ
            .Join(_db.Places,
                ad => ad.PlaceId,
                place => place.PlaceId,
                (ad, place) => new
                {
                    ad.AdId,
                    ad.Title,
                    ad.Description,
                    ad.BannerImageUrl,
                    ad.StartUtc,
                    ad.EndUtc,
                    ad.PlaceId,
                    ad.SupplierId,
                    PlaceName = place.Name,
                    PlaceAddress = place.Address,
                    Latitude = place.Latitude,
                    Longitude = place.Longitude
                }
            )
            .ToListAsync();

        return Ok(new { data = activeAds });
    }


    // GET: /api/advertisements/for-place/{placeId}
    // Lấy danh sách quảng cáo cho một địa điểm (Flutter gọi)
    [HttpGet("for-place/{placeId:int}")]
    [Authorize] // Yêu cầu đăng nhập
    public async Task<IActionResult> GetAdsForPlace(int placeId)
    {
        var account = await _GetAccountFromClaims(User);
        if (account == null) return Unauthorized(new { error = "invalid_token" });

        // Kiểm tra supplierId
        var isOwner = await _db.Places.AnyAsync(p => p.PlaceId == placeId && p.SupplierId == account.SupplierId);
        if (!isOwner)
        {
            
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "not_place_owner" });
        }

        var ads = await _db.Advertisements
            .AsNoTracking()
            .Where(a => a.PlaceId == placeId && !a.IsDeleted)
            .OrderByDescending(a => a.StartUtc)
            .ToListAsync();

        return Ok(new { data = ads });
    }

    // POST: /api/advertisements
    // Tạo quảng cáo mới
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] Advertisement req)
    {
        var account = await _GetAccountFromClaims(User);
        if (account == null || account.Role != "Supplier" || account.SupplierId == null)
        {
            
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden_supplier_only" });
        }

        // Kiểm tra quyền sở hữu địa điểm
        var place = await _db.Places.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlaceId == req.PlaceId && !p.IsDeleted);

        if (place == null) return NotFound(new { error = "place_not_found" });
        if (place.SupplierId != account.SupplierId)
        {
            
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden_place_owner" });
        }

        // Gán lại SupplierId từ token cho an toàn
        req.SupplierId = account.SupplierId.Value;
        req.AdId = 0; // Đảm bảo là tạo mới
        req.IsDeleted = false;

        await _db.Advertisements.AddAsync(req);
        await _db.SaveChangesAsync();

        // Trả về đối tượng đã tạo
        return CreatedAtAction(nameof(GetAdsForPlace), new { placeId = req.PlaceId }, new { data = req });
    }

    // PUT: /api/advertisements/{adId}
    // Cập nhật quảng cáo
    [HttpPut("{adId:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int adId, [FromBody] Advertisement req)
    {
        var account = await _GetAccountFromClaims(User);
        if (account == null || account.Role != "Supplier")
        {
            
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden_supplier_only" });
        }

        var ad = await _db.Advertisements
            .FirstOrDefaultAsync(a => a.AdId == adId && !a.IsDeleted);

        if (ad == null) return NotFound();

        // Kiểm tra quyền sở hữu (SupplierId trên quảng cáo phải khớp với SupplierId của token)
        if (ad.SupplierId != account.SupplierId)
        {
            
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden_ad_owner" });
        }

        // Cập nhật các trường
        ad.Title = req.Title;
        ad.Description = req.Description;
        ad.BannerImageUrl = req.BannerImageUrl;
        ad.StartUtc = req.StartUtc;
        ad.EndUtc = req.EndUtc;

        await _db.SaveChangesAsync();

        return Ok(new { data = ad });
    }

    // DELETE: /api/advertisements/{adId}
    [HttpDelete("{adId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int adId)
    {
        var account = await _GetAccountFromClaims(User);
        if (account == null) return Unauthorized(new { error = "invalid_token" });

        // Tìm quảng cáo
        var ad = await _db.Advertisements
            .FirstOrDefaultAsync(a => a.AdId == adId && !a.IsDeleted);

        if (ad == null) return NotFound();

        
        // 1. Nếu là Admin -> Cho phép xóa luôn (Bỏ qua check chủ sở hữu)
        // 2. Nếu là Supplier -> Phải check chủ sở hữu

        bool isAdmin = account.Role == "Admin";
        bool isOwner = account.Role == "Supplier" && ad.SupplierId == account.SupplierId;

        if (!isAdmin && !isOwner)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden_action" });
        }

        ad.IsDeleted = true;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}


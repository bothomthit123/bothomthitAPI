using bothomthit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace TourismApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReviewsController(AppDbContext db)
    {
        _db = db;
    }

    private int _GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var s = user.FindFirstValue("account_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : 0;
    }

    
    // Lấy danh sách review của một địa điểm (dành cho trang chi tiết)
    [HttpGet("place/{placeId:int}")]
    [AllowAnonymous] // Ai cũng xem được review
    public async Task<IActionResult> GetReviewsForPlace(int placeId)
    {
        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.PlaceId == placeId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new {
                r.ReviewId,
                r.Rating,
                r.Comment,
                r.CreatedAtUtc,
                // Join để lấy tên người review
                UserName = _db.Accounts.Where(a => a.AccountId == r.AccountId).Select(a => a.Name).FirstOrDefault() ?? "Ẩn danh"
            })
            .ToListAsync();

        return Ok(new { data = reviews });
    }

    
    // Thêm review mới (có xử lý auto-create place)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req)
    {
        try
        {
            var accountId = _GetUserIdFromClaims(User);
            if (accountId <= 0) return Unauthorized();

            // Validate
            if (req.Rating < 0 || req.Rating > 5)
                return BadRequest(new { error = "Rating must be between 0 and 5" });

            // Tìm hoặc Tạo Place
            int placeId = await _GetOrCreatePlaceAsync(req);
            if (placeId <= 0)
            {
                return StatusCode(500, new { error = "Failed to resolve place." });
            }

            // Kiểm tra xem user đã review chưa
            var existingReview = await _db.Reviews
                .FirstOrDefaultAsync(r => r.AccountId == accountId && r.PlaceId == placeId && !r.IsDeleted);

            if (existingReview != null)
            {
                return Conflict(new { error = "already_reviewed", message = "Bạn đã đánh giá địa điểm này rồi." });
            }

            //  Tạo Review
            var newReview = new Review
            {
                AccountId = accountId,
                PlaceId = placeId,
                Rating = req.Rating,
                Comment = req.Comment,

                // --- GÁN NGÀY GIỜ ---
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow, 
                IsDeleted = false
            };

            _db.Reviews.Add(newReview);
            await _db.SaveChangesAsync(); 

            //  Cập nhật điểm trung bình
            await _UpdatePlaceRating(placeId);

            return CreatedAtAction(nameof(GetReviewsForPlace), new { placeId = placeId }, new { data = newReview });
        }
        catch (Exception ex)
        {
            
            Console.WriteLine($"Lỗi khi tạo Review: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Logic tạo địa điểm 
    private async Task<int> _GetOrCreatePlaceAsync(CreateReviewRequest req)
    {
        // Nếu provider là "internal" (địa điểm có sẵn), ProviderId chính là PlaceId
        if (req.Provider == "internal" || req.Provider == "partner")
        {
            if (int.TryParse(req.ProviderId, out int internalId)) return internalId;
        }

        // Kiểm tra bảng map
        var mapping = await _db.ExternalPlaceMaps.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Provider == req.Provider && m.ProviderPlaceId == req.ProviderId);

        if (mapping != null) return mapping.PlaceId;

        // Tạo mới
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var newPlace = new Place
            {
                Name = req.Name,
                Address = req.Address,
                Category = req.Category,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                IsPartnerPlace = false,
                IsDeleted = false
            };
            _db.Places.Add(newPlace);
            await _db.SaveChangesAsync();

            var newMap = new ExternalPlaceMap
            {
                Provider = req.Provider,
                ProviderPlaceId = req.ProviderId,
                PlaceId = newPlace.PlaceId
            };
            _db.ExternalPlaceMaps.Add(newMap);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return newPlace.PlaceId;
        }
        catch
        {
            await transaction.RollbackAsync();
            return 0;
        }
    }

    // Tính lại điểm trung bình
    private async Task _UpdatePlaceRating(int placeId)
    {
        var stats = await _db.Reviews
            .Where(r => r.PlaceId == placeId && !r.IsDeleted)
            .GroupBy(r => r.PlaceId)
            .Select(g => new { Average = g.Average(r => r.Rating) })
            .FirstOrDefaultAsync();

        if (stats != null)
        {
            var place = await _db.Places.FindAsync(placeId);
            if (place != null)
            {
                place.Rating = stats.Average; 
                await _db.SaveChangesAsync();
            }
        }
    }
}
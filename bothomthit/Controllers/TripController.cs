using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using bothomthit.Models;

namespace TourismApp.Api.Controllers;

[Authorize] // Bắt buộc đăng nhập
[Route("api/[controller]")]
[ApiController]
public class TripsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TripsController(AppDbContext db)
    {
        _db = db;
    }

    // Helper lấy ID người dùng từ Token
    private int GetUserId()
    {
        var claim = User.FindFirst("account_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }

    // Lấy danh sách chuyến đi của tôi
    [HttpGet]
    public async Task<IActionResult> GetMyTrips()
    {
        var uid = GetUserId();
        var trips = await _db.Trips.AsNoTracking()
            // t = Trip
            .Where(t => t.AccountId == uid)
            .OrderByDescending(t => t.StartDate)
            .Select(t => new {
                t.TripId,
                t.Title,
                t.StartDate,
                t.EndDate,
                t.CoverImageUrl,
                ItemCount = t.Items.Count // Đếm số lượng hoạt động
            })
            .ToListAsync();

        return Ok(new { data = trips });
    }

    // Lấy chi tiết 1 chuyến đi 
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTripDetail(int id)
    {
        var uid = GetUserId();
        var trip = await _db.Trips.AsNoTracking()
            .Include(t => t.Items) 
            .FirstOrDefaultAsync(t => t.TripId == id && t.AccountId == uid);

        if (trip == null) return NotFound(new { error = "Không tìm thấy chuyến đi" });

        // Sắp xếp Timeline theo thời gian
        trip.Items = trip.Items.OrderBy(i => i.StartTime).ToList();

        return Ok(new { data = trip });
    }

    //  Tạo chuyến đi mới
    [HttpPost]
    public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest req)
    {
        var uid = GetUserId();
        if (req.StartDate > req.EndDate) return BadRequest(new { error = "Ngày kết thúc phải sau ngày bắt đầu" });

        var trip = new Trip
        {
            AccountId = uid,
            Title = req.Title,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CoverImageUrl = req.CoverImageUrl ?? "https://source.unsplash.com/800x600/?travel" // Ảnh mặc định
        };

        _db.Trips.Add(trip);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTripDetail), new { id = trip.TripId }, new { data = trip });
    }

    // Xóa chuyến đi
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTrip(int id)
    {
        var uid = GetUserId();
        var trip = await _db.Trips.Include(t => t.Items)
                                  .FirstOrDefaultAsync(t => t.TripId == id && t.AccountId == uid);

        if (trip == null) return NotFound();

        // Xóa hết các item con trước (nếu DB không set Cascade Delete)
        _db.ItineraryItems.RemoveRange(trip.Items);
        _db.Trips.Remove(trip);

        await _db.SaveChangesAsync();
        return NoContent();
    }
    //  Cập nhật chuyến đi
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTrip(int id, [FromBody] CreateTripRequest req)
    {
        var uid = GetUserId();

        // Kiểm tra: Nếu user gửi ngày Bắt đầu > Kết thúc
        if (req.StartDate > req.EndDate)
        {
            return BadRequest(new { error = "Ngày kết thúc phải sau ngày bắt đầu" });
        }

        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.TripId == id && t.AccountId == uid);

        if (trip == null) return NotFound(new { error = "Không tìm thấy chuyến đi" });

        // Cập nhật dữ liệu
        trip.Title = req.Title;
        trip.StartDate = req.StartDate;
        trip.EndDate = req.EndDate;
        // Chỉ cập nhật ảnh nếu user có gửi lên chuỗi khác rỗng
        if (!string.IsNullOrEmpty(req.CoverImageUrl))
        {
            trip.CoverImageUrl = req.CoverImageUrl;
        }

        await _db.SaveChangesAsync();

        
        return Ok(new { data = trip });
    }
}
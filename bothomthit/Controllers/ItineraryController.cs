using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using bothomthit.Models;

namespace TourismApp.Api.Controllers;

[Authorize]
[Route("api/trips/{tripId}/items")] // Route con: /api/trips/1/items
[ApiController]
public class ItineraryController : ControllerBase
{
    private readonly AppDbContext _db;

    public ItineraryController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst("account_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }

    // Helper kiểm tra quyền sở hữu chuyến đi
    private async Task<bool> IsTripOwner(int tripId, int userId)
    {
        return await _db.Trips.AnyAsync(t => t.TripId == tripId && t.AccountId == userId);
    }

    // 1. Thêm hoạt động vào lịch trình
    [HttpPost]
    public async Task<IActionResult> AddItem(int tripId, [FromBody] CreateItineraryItemRequest req)
    {
        var uid = GetUserId();
        if (!await IsTripOwner(tripId, uid)) return Forbid();

        var item = new ItineraryItem
        {
            TripId = tripId,
            Type = req.Type,
            Title = req.Title,
            BookingReference = req.BookingReference,
            LocationName = req.LocationName,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            Notes = req.Notes
        };

        _db.ItineraryItems.Add(item);
        await _db.SaveChangesAsync();

        return Ok(new { data = item });
    }

    // 2. Xóa hoạt động
    [HttpDelete("{itemId}")]
    public async Task<IActionResult> DeleteItem(int tripId, int itemId)
    {
        var uid = GetUserId();
        // Kiểm tra item có thuộc trip này và trip có thuộc user này không
        var item = await _db.ItineraryItems
            .Include(i => i.Trip)
            .FirstOrDefaultAsync(i => i.ItemId == itemId && i.TripId == tripId && i.Trip.AccountId == uid);

        if (item == null) return NotFound();

        _db.ItineraryItems.Remove(item);
        await _db.SaveChangesAsync();

        return NoContent();
    }
    // 3. Cập nhật hoạt động 
    [HttpPut("{itemId}")]
    public async Task<IActionResult> UpdateItem(int tripId, int itemId, [FromBody] UpdateItineraryItemRequest req)
    {
        var uid = GetUserId();

        // Kiểm tra item có tồn tại và thuộc sở hữu của user không
        var item = await _db.ItineraryItems
            .Include(i => i.Trip)
            .FirstOrDefaultAsync(i => i.ItemId == itemId && i.TripId == tripId && i.Trip.AccountId == uid);

        if (item == null) return NotFound(new { error = "Không tìm thấy mục lịch trình hoặc bạn không có quyền." });

        // Cập nhật từng trường nếu có dữ liệu gửi lên
        if (!string.IsNullOrEmpty(req.Type)) item.Type = req.Type;
        if (!string.IsNullOrEmpty(req.Title)) item.Title = req.Title;
        if (req.BookingReference != null) item.BookingReference = req.BookingReference;
        if (req.LocationName != null) item.LocationName = req.LocationName;

        // Cập nhật thời gian (nếu có)
        if (req.StartTime.HasValue) item.StartTime = req.StartTime.Value;
        if (req.EndTime.HasValue) item.EndTime = req.EndTime.Value;

        if (req.Notes != null) item.Notes = req.Notes;
        if (req.AttachmentUrl != null) item.AttachmentUrl = req.AttachmentUrl;

        await _db.SaveChangesAsync();

        return Ok(new { data = item });
    }
}
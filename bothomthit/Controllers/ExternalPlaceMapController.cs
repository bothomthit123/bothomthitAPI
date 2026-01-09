using bothomthit.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TourismApp.Api.Controllers;

[ApiController]
[Route("api/places/import")] // Route độc lập, không cần placeId trước
public class PlaceImportController : ControllerBase
{
    private readonly AppDbContext _db;
    public PlaceImportController(AppDbContext db) => _db = db;

    // Class nhận dữ liệu từ Flutter gửi lên
    public class ImportRequest
    {
        public string Provider { get; set; }       // "osm" hoặc "fsq"
        public string ProviderId { get; set; }     // ID bên Foursquare/OSM
        public string Name { get; set; }
        public string? Address { get; set; }
        public string? Category { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Import([FromBody] ImportRequest req)
    {
        // 1. Kiểm tra trong bảng ExternalPlaceMaps xem đã map chưa
        var mapping = await _db.ExternalPlaceMaps.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Provider == req.Provider && m.ProviderPlaceId == req.ProviderId);

        // Nếu đã có rồi -> Trả về PlaceId luôn
        if (mapping != null)
        {
            return Ok(new { placeId = mapping.PlaceId, isNew = false });
        }

        // 2. Nếu chưa có -> Tạo Place mới trong bảng Places
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
        await _db.SaveChangesAsync(); // Lưu để lấy newPlace.PlaceId

        // 3. Tạo liên kết vào bảng ExternalPlaceMaps 
        var newMap = new ExternalPlaceMap
        {
            PlaceId = newPlace.PlaceId,
            Provider = req.Provider,
            ProviderPlaceId = req.ProviderId
        };
        _db.ExternalPlaceMaps.Add(newMap);
        await _db.SaveChangesAsync();

        return StatusCode(201, new { placeId = newPlace.PlaceId, isNew = true });
    }
}
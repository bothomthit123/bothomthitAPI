using bothomthit.Models; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; 

namespace TourismApp.Api.Controllers;

[ApiController]
[Route("api/places")] 
public class PlacesController : ControllerBase
{
    private readonly AppDbContext _db;
    public PlacesController(AppDbContext db) => _db = db;

    // Thêm một hàm helper để lấy thông tin tài khoản từ token
    private async Task<Account?> _GetAccountFromClaims(ClaimsPrincipal user)
    {
        var claimsUserIdString = user.FindFirstValue("account_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claimsUserIdString, out var claimsUserId))
        {
            return null;
        }
        return await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == claimsUserId);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] double? lat, [FromQuery] double? lon,
        [FromQuery] double? radiusMeters, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var query = _db.Places.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || (p.Address ?? "").Contains(q));

        if (lat.HasValue && lon.HasValue && radiusMeters.HasValue && radiusMeters.Value > 0)
        {
            var d = radiusMeters.Value / 1000.0 * 0.009;
            query = query.Where(p =>
                p.Latitude >= lat - d && p.Latitude <= lat + d &&
                p.Longitude >= lon - d && p.Longitude <= lon + d);
        }

        var data = await query
            .OrderBy(p => p.PlaceId)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new {
                p.PlaceId,
                p.Name,
                p.Address,
                p.Latitude,
                p.Longitude,
                source = p.IsPartnerPlace ? "partner" : "foursquare",
                p.Rating,
                p.Category
            })
            .ToListAsync();

        return Ok(new { data, pagination = new { page, size, count = data.Count } });
    }

    [HttpGet("{placeId:int}")]
    public async Task<IActionResult> Get(int placeId)
    {
        var place = await _db.Places.AsNoTracking().FirstOrDefaultAsync(p => p.PlaceId == placeId && !p.IsDeleted);
        if (place == null) return NotFound();

        var now = DateTime.UtcNow;
        var ads = await _db.Advertisements.AsNoTracking()
            .Where(a => a.PlaceId == placeId && !a.IsDeleted && a.StartUtc <= now && now < a.EndUtc)
            .OrderBy(a => a.EndUtc).ToListAsync();

        var avgRating = await _db.Reviews.AsNoTracking()
            .Where(r => r.PlaceId == placeId && !r.IsDeleted)
            .Select(r => (double?)r.Rating).DefaultIfEmpty().AverageAsync();

        return Ok(new
        {
            data = new
            {
                place, // Trả về toàn bộ đối tượng Place
                avgRating = Math.Round(avgRating ?? 0, 2),
                activeAds = ads.Select(a => new { a.AdId, a.Title, a.Description, a.BannerImageUrl, a.StartUtc, a.EndUtc })
            }
        });
    }

    //  Thêm [Authorize]
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] Place req)
    {
        //  Lấy thông tin tài khoản từ Token
        var account = await _GetAccountFromClaims(User);
        if (account == null || account.Role != "Supplier" || account.SupplierId == null)
        {
            return Forbid(); // Cấm nếu không phải Supplier hoặc không có SupplierId
        }

        req.PlaceId = 0;
        req.IsPartnerPlace = true; // Địa điểm do Supplier tạo luôn là partner
        req.SupplierId = account.SupplierId;

        await _db.Places.AddAsync(req);
        await _db.SaveChangesAsync();

        // Trả về toàn bộ đối tượng 'req' (đã được cập nhật PlaceId) trong key 'data'
        return CreatedAtAction(nameof(Get), new { placeId = req.PlaceId }, new { data = req });
    }

    //  Thêm [Authorize]
    [HttpPut("{placeId:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int placeId, [FromBody] Place req)
    {
        //  Kiểm tra supplier
        var account = await _GetAccountFromClaims(User);
        if (account == null || account.Role != "Supplier" || account.SupplierId == null)
        {
            return Forbid();
        }

        var p = await _db.Places.FirstOrDefaultAsync(x => x.PlaceId == placeId && !x.IsDeleted);
        if (p == null) return NotFound();

        //  Kiểm tra sở hữu
        if (p.SupplierId != account.SupplierId)
        {
            return Forbid(); // Cấm sửa địa điểm của Supplier khác
        }

        // Cập nhật thông tin
        p.Name = req.Name ?? p.Name;
        p.Address = req.Address ?? p.Address;
        p.Description = req.Description ?? p.Description;
        // Chỉ cập nhật lat/lon nếu client gửi giá trị (không phải 0)
      
        if (req.Latitude != 0) p.Latitude = req.Latitude;
        if (req.Longitude != 0) p.Longitude = req.Longitude;

        p.Category = req.Category ?? p.Category;
        p.OpeningHours = req.OpeningHours ?? p.OpeningHours;

        // Logic cập nhật ClosingHours
        p.ClosingHours = req.ClosingHours ?? p.ClosingHours;

        await _db.SaveChangesAsync();

        // Trả về đối tượng đã cập nhật
        return Ok(new { data = p });
    }

    // Thêm [Authorize]
    [HttpDelete("{placeId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int placeId)
    {
        //  Kiểm tra supplier
        var account = await _GetAccountFromClaims(User);
        if (account == null || account.Role != "Supplier" || account.SupplierId == null)
        {
            return Forbid();
        }

        var p = await _db.Places.FirstOrDefaultAsync(x => x.PlaceId == placeId && !x.IsDeleted);
        if (p == null) return NotFound();

        // Kiểm tra sở hữu
        if (p.SupplierId != account.SupplierId)
        {
            return Forbid();
        }

        p.IsDeleted = true;
        await _db.SaveChangesAsync();

        
        return NoContent();
    }
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Ok(new { data = new List<object>() });
        }

        // Tìm kiếm gần đúng theo Tên hoặc Địa chỉ, ưu tiên những quán chưa bị xóa
        var list = await _db.Places
            .AsNoTracking()
            .Where(p => !p.IsDeleted && (p.Name.Contains(keyword) || (p.Address != null && p.Address.Contains(keyword))))
            .OrderByDescending(p => p.Rating) // Ưu tiên quán có rating cao hiển thị trước
            .Take(20) // Lấy tối đa 20 kết quả 
            .Select(p => new
            {
                p.PlaceId,
                p.Name,
                p.Address,
                p.Category,
                p.Latitude,
                p.Longitude,
                p.Rating,
                p.SupplierId

            })
            .ToListAsync();

        return Ok(new { data = list });
    }
    [HttpGet("visible")]
    public async Task<IActionResult> GetVisible([FromQuery] double north, [FromQuery] double south, [FromQuery] double east, [FromQuery] double west)
    {
        // Validate tọa độ
        if (north == 0 || south == 0 || east == 0 || west == 0)
            return Ok(new { data = new List<object>() });

        var list = await _db.Places
            .AsNoTracking()
            .Where(p => !p.IsDeleted &&
                        p.Latitude >= south && p.Latitude <= north &&
                        p.Longitude >= west && p.Longitude <= east)
            .Take(50) // Giới hạn 50 địa điểm để không làm lag app
            .Select(p => new
            {
                p.PlaceId,
                p.Name,
                p.Address,
                p.Category,
                p.Latitude,
                p.Longitude,
                p.Rating,
                p.SupplierId
            })
            .ToListAsync();

        return Ok(new { data = list });
    }
}

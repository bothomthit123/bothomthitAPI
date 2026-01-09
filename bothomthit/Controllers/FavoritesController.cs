using bothomthit.Models; // Đảm bảo namespace này đúng
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace TourismApp.Api.Controllers;


public class AddFavoriteRequest
{
    public string Provider { get; set; }
    public string ProviderId { get; set; }
    public string Name { get; set; }
    public string? Address { get; set; }
    public string? Category { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

[Authorize]
[ApiController]
[Route("me/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoritesController(AppDbContext db)
    {
        _db = db;
    }

    private int _GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var s = user.FindFirstValue("account_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : 0;
    }

    // GET: /me/favorites
    [HttpGet]
    public async Task<IActionResult> GetMyFavorites([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var accountId = _GetUserIdFromClaims(User);
        if (accountId <= 0) return Unauthorized();

       
        // Join 2 bảng
        var favoritesQuery =
            from f in _db.Favorites.AsNoTracking() 
            join p in _db.Places.AsNoTracking()    
              on f.PlaceId equals p.PlaceId      // dùng PlaceId để nối 2 bảng
            where f.AccountId == accountId && !p.IsDeleted 
            orderby p.Name 
            select p;      

        var favorites = await favoritesQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
     

        return Ok(new { data = favorites });
    }

    // POST: /me/favorites
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddFavoriteRequest req)
    {
        var accountId = _GetUserIdFromClaims(User);
        if (accountId <= 0) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Provider) || string.IsNullOrWhiteSpace(req.ProviderId))
        {
            return BadRequest(new { error = "Provider and ProviderId are required." });
        }

        var placeId = await _GetOrCreatePlaceAsync(req);
        if (placeId <= 0)
        {
            return StatusCode(500, new { error = "Failed to create place." });
        }

        var exists = await _db.Favorites
            .AnyAsync(f => f.AccountId == accountId && f.PlaceId == placeId);

        if (exists)
        {
            return Conflict(new { error = "already_favorited" }); 
        }

        var newFavorite = new Favorite
        {
            AccountId = accountId,
            PlaceId = placeId
            // CreatedAtUtc sẽ tự động được gán bởi DEFAULT SYSUTCDATETIME()
        };

        await _db.Favorites.AddAsync(newFavorite);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyFavorites), new { data = newFavorite }); 
    }

    private async Task<int> _GetOrCreatePlaceAsync(AddFavoriteRequest req)
    {
        var mapping = await _db.ExternalPlaceMaps.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Provider == req.Provider && m.ProviderPlaceId == req.ProviderId);

        if (mapping != null)
        {
            return mapping.PlaceId;
        }

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
            //tạo địa điểm mới trong ExternalPlaceMap nếu địa điểm chưa tồn tại
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[FAV_ERROR] {ex.Message}");
            return 0;
        }
    }

    // DELETE: /me/favorites/{placeId}
    [HttpDelete("{placeId:int}")]
    public async Task<IActionResult> Delete(int placeId)
    {
        var accountId = _GetUserIdFromClaims(User);
        if (accountId <= 0) return Unauthorized();

        var favorite = await _db.Favorites
            .FirstOrDefaultAsync(f => f.AccountId == accountId && f.PlaceId == placeId);

        if (favorite == null)
        {
            return NotFound();
        }

        _db.Favorites.Remove(favorite);
        await _db.SaveChangesAsync();

        return NoContent(); 
    }
}
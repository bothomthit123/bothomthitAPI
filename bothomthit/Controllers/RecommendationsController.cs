using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bothomthit.Models;
using TourismApp.Api.Services;

namespace TourismApp.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RecommendationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RecommendationService _mlService;

    public RecommendationsController(AppDbContext db, RecommendationService mlService)
    {
        _db = db;
        _mlService = mlService;
    }

    // --- API KÍCH HOẠT HUẤN LUYỆN & TRẢ VỀ ĐIỂM SỐ  ---
    [HttpPost("train")]
    public async Task<IActionResult> ForceTrain()
    {
        try
        {
            // Nhận kết quả metrics trả về từ Service
            var metrics = await _mlService.TrainModel();

            return Ok(new
            {
                message = "Huấn luyện hoàn tất!",
                metrics = new
                {
                    RMSE = Math.Round(metrics.RootMeanSquaredError, 4),
                    RSquared = Math.Round(metrics.RSquared, 4)
                },
                note = "RMSE càng thấp càng tốt. RSquared càng gần 1 càng tốt."
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"Lỗi huấn luyện: {ex.Message}");
        }
    }

    // --- API LẤY DANH SÁCH GỢI Ý ---
    
    [HttpGet]
    public async Task<IActionResult> GetRecommendations([FromQuery] double? lat, [FromQuery] double? lng)
    {
        // 1. Lấy AccountId từ Token
        int? accountId = null;
        var userIdClaim = User.FindFirst("account_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int id)) accountId = id;

        // 2. KHỞI TẠO QUERY CƠ BẢN
        var query = _db.Places.AsNoTracking()
                              .Include(p => p.Reviews)
                              .Include(p => p.ExternalMaps)
                              .Where(p => !p.IsDeleted);

        // 3. LỌC KẾT QUẢ
        List<Place> candidatePlaces;

        if (lat.HasValue && lng.HasValue)
        {
            double range = 0.2; // Khoảng 20km
            query = query.Where(p => p.Latitude >= lat - range && p.Latitude <= lat + range &&
                                     p.Longitude >= lng - range && p.Longitude <= lng + range);

            candidatePlaces = await query.ToListAsync();
        }
        else
        {
            // CASE B: Không có GPS -> Lấy Top 100 quán đông review nhất để lọc
            candidatePlaces = await query.OrderByDescending(p => p.Reviews.Count)
                                         .Take(100)
                                         .ToListAsync();
        }

        List<dynamic> resultList = new List<dynamic>();
        string reason = "top_rated";

        // 4. CHẤM ĐIỂM (AI PREDICTION)
        if (accountId.HasValue && candidatePlaces.Any())
        {
            // Chạy vòng lặp qua các ứng viên để dự đoán điểm số
            var predictions = candidatePlaces.Select(p => new
            {
                Place = p,
                // Gọi Service dự đoán (đã có lock thread-safe)
                PredictedScore = _mlService.PredictScore(accountId.Value, p.PlaceId)
            })
            // Sắp xếp giảm dần theo điểm AI chấm
            .OrderByDescending(x => x.PredictedScore)
            .ToList();

            // Nếu AI tìm được gợi ý có điểm > 0.001 (tức là có liên quan)
            if (predictions.Any(x => x.PredictedScore > 0.001f))
            {
                // Lấy Top 10 sau khi AI lọc
                resultList = predictions.Take(10)
                                        .Select(x => MapToDto(x.Place))
                                        .ToList();

                if (lat.HasValue) reason = "ai_personalized_nearby";
                else reason = "ai_personalized";

                return Ok(new { data = resultList, reason = reason });
            }
        }

        // 5. FALLBACK - TOP RATED
        // Nếu không Login hoặc AI chưa học được -> Trả về địa điểm Rating cao nhất
        var topRated = candidatePlaces
            .OrderByDescending(p => p.Reviews != null && p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0)
            .ThenByDescending(p => p.Reviews.Count) // Ưu tiên quán đông nhiều đánh giá
            .Take(10)
            .ToList();

        resultList = topRated.Select(p => MapToDto(p)).ToList();

        if (lat.HasValue) reason = "top_rated_nearby";

        return Ok(new { data = resultList, reason = reason });
    }

    // --- HÀM HELPER (Private) ---
    private dynamic MapToDto(Place p)
    {
        // 1. Xử lý ProviderId
        var extMap = p.ExternalMaps?.FirstOrDefault();
        string providerIdStr;

        if (extMap != null)
        {
            if (extMap.Provider == "foursquare")
                providerIdStr = $"fsq_{extMap.ProviderPlaceId}";
            else if (extMap.Provider == "osm")
                providerIdStr = $"osm_{extMap.ProviderPlaceId}";
            else
                providerIdStr = $"{extMap.Provider}_{extMap.ProviderPlaceId}";
        }
        else
        {
            // Địa điểm nội bộ do Supplier tạo
            providerIdStr = $"internal_{p.PlaceId}";
        }

        // 2. Tính Rating trung bình
        double ratingVal = 0.0;
        if (p.Reviews != null && p.Reviews.Any())
        {
            ratingVal = Math.Round(p.Reviews.Average(r => (double)r.Rating), 1);
        }
        // Fallback rating
        else if (p.Rating.HasValue)
        {
            ratingVal = (double)p.Rating.Value;
        }

        // 3. Trả về Object với ảnh ngẫu nhiên
        return new
        {
            p.PlaceId,
            p.Name,
            p.Address,
            p.Category,
            p.Latitude,
            p.Longitude,
            ProviderId = providerIdStr,
            Rating = ratingVal,
            ReviewCount = p.Reviews?.Count ?? 0,
            ImageUrl = $"https://loremflickr.com/400/300/{Uri.EscapeDataString(p.Category ?? "travel")}"
        };
    }
}
using bothomthit.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using System.Data;
namespace TourismApp.Api.Services;

// Class kết quả dự đoán
public class PlaceRatingPrediction
{
    public float Label;
    public float Score;
}

// Class dữ liệu đầu vào cho ML
public class PlaceRating
{
    public float UserId { get; set; }
    public float PlaceId { get; set; }
    public float Label { get; set; }
}
public class ModelPerformanceMetrics
{
    public double RootMeanSquaredError { get; set; } // Càng thấp càng tốt 
    public double RSquared { get; set; }             // Càng gần 1 càng tốt 
}

public class RecommendationService
{
    private static string _modelPath = Path.Combine(Environment.CurrentDirectory, "recommendationModel.zip");
    private readonly AppDbContext _db;
    private MLContext _mlContext;
    private ITransformer? _trainedModel;
    private PredictionEngine<PlaceRating, PlaceRatingPrediction>? _predictionEngine;

    // Khóa luồng để đảm bảo an toàn khi chạy trên Web API
    private readonly object _predictionLock = new object();

    public RecommendationService(AppDbContext db)
    {
        _db = db;
        _mlContext = new MLContext();

        // Load model cũ nếu có để dùng ngay
        if (File.Exists(_modelPath))
        {
            try
            {
                _trainedModel = _mlContext.Model.Load(_modelPath, out var schema);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<PlaceRating, PlaceRatingPrediction>(_trainedModel);
            }
            catch
            {
                // Bỏ qua nếu file lỗi
            }
        }
    }

    public async Task<ModelPerformanceMetrics> TrainModel()
    {
        // --- 1. LẤY DỮ LIỆU TỪ REVIEW  ---
        // Trọng số: Theo điểm đánh giá thực tế (1.0 - 5.0)
        var reviews = await _db.Reviews.AsNoTracking()
            .Select(r => new PlaceRating
            {
                UserId = (float)r.AccountId,
                PlaceId = (float)r.PlaceId,
                Label = (float)r.Rating
            }).ToListAsync();

        // --- 2. LẤY DỮ LIỆU TỪ FAVORITES  ---
        // Trọng số: 5.0 (Rất thích)
        var favorites = await _db.Favorites.AsNoTracking()
            .Select(f => new PlaceRating
            {
                UserId = (float)f.AccountId,
                PlaceId = (float)f.PlaceId,
                Label = 5.0f
            }).ToListAsync();

        // --- 3. LẤY DỮ LIỆU TỪ SEARCH HISTORY  ---
        // Trọng số: 3.5 (Quan tâm/Tò mò)
        var searchSignals = await GetSearchHistorySignals();

        // --- 4. GỘP DỮ LIỆU ---
        var trainingData = new List<PlaceRating>();
        trainingData.AddRange(reviews);
        trainingData.AddRange(favorites);
        trainingData.AddRange(searchSignals);

        // --- 5. XỬ LÝ TRÙNG LẶP & MÂU THUẪN ---
        // Nếu 1 User tương tác nhiều kiểu với 1 Place, lấy điểm cao nhất
        var uniqueData = trainingData
            .GroupBy(x => new { x.UserId, x.PlaceId })
            .Select(g => new PlaceRating
            {
                UserId = g.Key.UserId,
                PlaceId = g.Key.PlaceId,
                Label = g.Max(x => x.Label)
            })
            .ToList();

        // Kiểm tra dữ liệu: Nếu quá ít thì không train, trả về kết quả rỗng
        if (uniqueData.Count < 20)
        {
            return new ModelPerformanceMetrics { RootMeanSquaredError = 0, RSquared = 0 };
        }

        // --- 6. HUẤN LUYỆN MODEL ---
        IDataView trainingDataView = _mlContext.Data.LoadFromEnumerable(uniqueData);

        // Dùng 80% để học (Train), 20% để thi (Test) để đánh giá khách quan
        var dataSplit = _mlContext.Data.TrainTestSplit(trainingDataView, testFraction: 0.2);
        IDataView trainData = dataSplit.TrainSet;
        IDataView testData = dataSplit.TestSet;

        // Xây dựng Pipeline
        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "userIdEncoded", inputColumnName: "UserId")
            .Append(_mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "placeIdEncoded", inputColumnName: "PlaceId"))
            .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(
                new MatrixFactorizationTrainer.Options
                {
                    MatrixColumnIndexColumnName = "userIdEncoded",
                    MatrixRowIndexColumnName = "placeIdEncoded",
                    LabelColumnName = "Label",
                    NumberOfIterations = 50,
                    ApproximationRank = 100
                }));

        //  Chỉ Train trên tập trainData (80%)
        _trainedModel = pipeline.Fit(trainData);

        //  Đánh giá hiệu suất trên tập testData (20%)
        var predictions = _trainedModel.Transform(testData);
        var metrics = _mlContext.Recommendation().Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

        // Lưu model (Dùng Schema gốc của toàn bộ dữ liệu để lưu cấu trúc đúng)
        _mlContext.Model.Save(_trainedModel, trainingDataView.Schema, _modelPath);

        lock (_predictionLock)
        {
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<PlaceRating, PlaceRatingPrediction>(_trainedModel);
        }

        // Trả về kết quả đánh giá (RMSE càng nhỏ càng tốt, RSquared càng gần 1 càng tốt)
        return new ModelPerformanceMetrics
        {
            RootMeanSquaredError = metrics.RootMeanSquaredError,
            RSquared = metrics.RSquared
        };
    }

    // --- HÀM BIẾN TỪ KHÓA THÀNH ĐIỂM SỐ ---
    private async Task<List<PlaceRating>> GetSearchHistorySignals()
    {
        // 1. Lấy tất cả lịch sử tìm kiếm
        var histories = await _db.SearchHistories.AsNoTracking().ToListAsync();
        if (!histories.Any()) return new List<PlaceRating>();

        // 2. Lấy danh sách Place nhẹ (chỉ cần Id, Name, Category, Address để so khớp)
        var places = await _db.Places.AsNoTracking()
            .Select(p => new { p.PlaceId, p.Name, p.Category, p.Address })
            .ToListAsync();

        var signals = new List<PlaceRating>();

        // 3. So khớp (Mapping Logic)
        foreach (var h in histories)
        {
            if (string.IsNullOrWhiteSpace(h.Keyword) || h.Keyword.Length < 2) continue; // Bỏ qua từ khóa quá ngắn

            string k = h.Keyword.ToLower().Trim();

            // Tìm các quán có Tên hoặc Danh mục hoặc Địa chỉ chứa từ khóa
            var matchedPlaces = places.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(k)) ||
                (p.Category != null && p.Category.ToLower().Contains(k)) ||
                (p.Address != null && p.Address.ToLower().Contains(k))
            );

            foreach (var match in matchedPlaces)
            {
                signals.Add(new PlaceRating
                {
                    UserId = (float)h.AccountId,
                    PlaceId = (float)match.PlaceId,
                    Label = 3.5f // Gán điểm "Quan tâm"
                });
            }
        }

        return signals;
    }


    // Hàm dự đoán (Thread-Safe)
    public float PredictScore(int userId, int placeId)
    {
        lock (_predictionLock)
        {
            if (_predictionEngine == null) return 0;

            var prediction = _predictionEngine.Predict(new PlaceRating
            {
                UserId = (float)userId,
                PlaceId = (float)placeId
            });

            return prediction.Score;
        }
    }
}
using Microsoft.ML.Data;

namespace bothomthit.Models
{
    // Dữ liệu đầu vào: Một lượt đánh giá cụ thể
    public class PlaceRating
    {
        [LoadColumn(0)] public float UserId;  // AccountId
        [LoadColumn(1)] public float PlaceId; // PlaceId
        [LoadColumn(2)] public float Label;   // Rating (1-5)
    }

    // Dữ liệu dự đoán: Điểm số mà AI nghĩ user sẽ chấm cho địa điểm đó
    public class PlaceRatingPrediction
    {
        public float Label; // Giá trị thực (nếu có)
        public float Score; // Giá trị AI dự đoán
    }
}


namespace bothomthit.Models
{
    public class ExternalPlaceMap
    {
        

        public string Provider { get; set; } = null!;
        public string ProviderPlaceId { get; set; } = null!; //ID của địa điểm đó trên hệ thống của nhà cung cấp

        public int PlaceId { get; set; }

       
        [System.Text.Json.Serialization.JsonIgnore] //Ngăn các thuộc tính xuất hiện trong JSON để tránh vòng lặp vô hạn
        public virtual Place? Place { get; set; } // Liên kết đến Place trong hệ thống để điều hướng

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
//Model này dùng để lưu liên kết giữa Place trong hệ thống và Place từ nguồn bên ngoài như 1 Mapping
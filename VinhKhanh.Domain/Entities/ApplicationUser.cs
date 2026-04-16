using Microsoft.AspNetCore.Identity;

namespace VinhKhanh.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    // Tên đầy đủ
    public string FullName { get; set; } = string.Empty;
    
    // Nếu là quán (ShopOwner) thì lưu ID của quán
    public int? PoiId { get; set; }
    
    // Chủ quán cần được admin duyệt
    public bool IsApproved { get; set; } = false;
    
    // Ngày cài đặt/Kích hoạt App (Dành cho chức năng tính phí sau 7 ngày của Visitor)
    public DateTime ActivationDate { get; set; } = DateTime.UtcNow;
}

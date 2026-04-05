namespace VinhKhanh.Domain.Entities;

public class Poi
{
    public int Id { get; set; }
    public string BasePoiId { get; set; } = string.Empty; // Mã định danh base
    public string CategoryCode { get; set; } = "FOOD_STREET";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 50;
    public string? ImageUrl { get; set; }
    public int Priority { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? OwnerSecretInfo { get; set; } // Sẽ được mã hóa

    // New fields
    public PoiStatus Status { get; set; } = PoiStatus.Draft;
    public bool IsPremium { get; set; } = false;
    public string? OwnerId { get; set; }
    public string? RejectionReason { get; set; }

    // Navigation properties
    public ICollection<PoiLocalization> Localizations { get; set; } = new List<PoiLocalization>();
    public ApplicationUser? Owner { get; set; }
}

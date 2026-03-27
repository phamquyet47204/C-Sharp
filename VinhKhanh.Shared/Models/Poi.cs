namespace VinhKhanh.Shared.Models;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 15; // meters
    public int Priority { get; set; } = 0;   // higher = plays first
    public string? AudioFile { get; set; }   // optional .mp3 path
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

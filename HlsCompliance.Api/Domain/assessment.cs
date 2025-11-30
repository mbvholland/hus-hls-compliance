namespace HlsCompliance.Api.Domain;

public class Assessment
{
    public Guid Id { get; set; }

    public string Organisation { get; set; } = string.Empty;

    public string Supplier { get; set; } = string.Empty;

    public string Solution { get; set; } = string.Empty;

    public string HlsVersion { get; set; } = "1.0";

    public string Phase1Status { get; set; } = "not_started";
    public string Phase2Status { get; set; } = "not_started";
    public string Phase3Status { get; set; } = "not_started";
    public string Phase4aStatus { get; set; } = "not_started";
    public string Phase4bStatus { get; set; } = "not_started";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

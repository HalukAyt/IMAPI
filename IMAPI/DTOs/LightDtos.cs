namespace IMAPI.Api.DTOs;

public record LightResponse(Guid Id, int ChNo, string? Name, bool IsOn, DateTime UpdatedAt);

public class LightSetRequest
{
    public int? State { get; set; }   // 0=OFF, 1=ON
    public bool? Toggle { get; set; } // true ise tersine çevir
}

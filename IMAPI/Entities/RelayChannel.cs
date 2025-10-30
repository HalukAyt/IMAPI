namespace IMAPI.Entities;

public class RelayChannel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public int Index { get; set; } // 0..7
    public string Name { get; set; } = $"CH";
    public bool ActiveLow { get; set; } = true;
}
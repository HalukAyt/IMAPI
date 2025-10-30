namespace IMAPI.Entities
{
    public class Boat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = default!;
        public string OwnerId { get; set; } = default!; // AppUser.Id
        public AppUser? Owner { get; set; }
        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}

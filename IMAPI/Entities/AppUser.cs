namespace IMAPI.Entities
{
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public ICollection<Boat> Boats { get; set; } = new List<Boat>();
    }
}

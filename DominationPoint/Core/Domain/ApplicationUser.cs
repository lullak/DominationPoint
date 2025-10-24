using Microsoft.AspNetCore.Identity;

namespace DominationPoint.Core.Domain
{
    public class ApplicationUser : IdentityUser
    {
        public required string ColorHex { get; set; }
        public string? NumpadCode { get; set; }
    }
}

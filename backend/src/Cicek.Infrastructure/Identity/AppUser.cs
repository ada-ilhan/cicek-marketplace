using Microsoft.AspNetCore.Identity;

namespace Cicek.Infrastructure.Identity;

/// <summary>
/// Identity store account. Kept in Infrastructure because it is shaped by ASP.NET Core Identity;
/// customer/florist profile data belongs to their own modules, linked by user id.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
        Id = Guid.CreateVersion7();
    }

    public AppRole(string roleName)
        : this()
    {
        Name = roleName;
    }
}

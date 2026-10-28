using Microsoft.AspNetCore.Identity;

namespace ValoresReceitas.Api.Models;

public sealed class AccountUser : IdentityUser
{
    public long WorkspaceRevision { get; set; }
    public bool IsDeleted { get; set; }
}
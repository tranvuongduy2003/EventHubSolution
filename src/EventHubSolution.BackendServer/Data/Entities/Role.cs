using Microsoft.AspNetCore.Identity;

namespace EventHubSolution.BackendServer.Data.Entities
{
    public class Role : IdentityRole
    {
        public Role()
        {
        }

        public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    }
}

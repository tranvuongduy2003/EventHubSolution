using EventHubSolution.BackendServer.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace EventHubSolution.BackendServer.Data
{
    public class DbInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly string AdminRoleName = "Admin";
        private readonly string UserRoleName = "Member";

        public DbInitializer(ApplicationDbContext context,
          UserManager<User> userManager,
          RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task Seed()
        {
        }
    }
}

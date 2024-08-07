﻿
using Bogus;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.ViewModels.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Extensions;
using Event = EventHubSolution.BackendServer.Data.Entities.Event;
using Function = EventHubSolution.BackendServer.Data.Entities.Function;
using PaymentMethod = EventHubSolution.BackendServer.Data.Entities.PaymentMethod;
using Review = EventHubSolution.BackendServer.Data.Entities.Review;

namespace EventHubSolution.BackendServer.Data
{
    public class DbInitializer
    {
        private const int MAX_EVENTS_QUANTITY = 1000;
        private const int MAX_CATEGORIES_QUANTITY = 20;
        private const int MAX_USERS_QUANTITY = 10;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;

        public DbInitializer(ApplicationDbContext context,
          UserManager<User> userManager,
          RoleManager<Role> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task Seed()
        {
            SeedRoles().Wait();
            SeedUsers().Wait();
            SeedFunctions().Wait();
            SeedCommands().Wait();
            SeedPermission().Wait();
            SeedCategories().Wait();
            SeedPaymentMethods().Wait();
            SeedEvents().Wait();
            SeedReviews().Wait();
        }

        private async Task SeedRoles()
        {
            if (!_roleManager.Roles.Any())
            {
                await _roleManager.CreateAsync(new Role()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = UserRole.ADMIN.GetDisplayName(),
                    ConcurrencyStamp = "1",
                    NormalizedName = UserRole.ADMIN.GetDisplayName().Normalize()
                });
                await _roleManager.CreateAsync(new Role()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = UserRole.CUSTOMER.GetDisplayName(),
                    ConcurrencyStamp = "2",
                    NormalizedName = UserRole.CUSTOMER.GetDisplayName().Normalize()
                });
                await _roleManager.CreateAsync(new Role()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = UserRole.ORGANIZER.GetDisplayName(),
                    ConcurrencyStamp = "3",
                    NormalizedName = UserRole.ORGANIZER.GetDisplayName().Normalize()
                });
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedUsers()
        {
            if (!_userManager.Users.Any())
            {
                var fakerAvatar = new Faker<FileStorage>()
                    .RuleFor(f => f.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(f => f.FileName, f => $"{f.Person.UserName}.png")
                    .RuleFor(f => f.FileType, _ => "image/png")
                    .RuleFor(f => f.FileContainer, _ => FileContainer.USERS)
                    .RuleFor(f => f.FileSize, f => f.Random.Int(1000, 5000))
                    .RuleFor(f => f.FilePath, f => f.Person.Avatar);

                var adminAvatar = fakerAvatar.Generate();
                _context.FileStorages.Add(adminAvatar);
                User admin = new User()
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = "admin@gmail.com",
                    NormalizedEmail = "admin@gmail.com",
                    UserName = "admin",
                    NormalizedUserName = "admin",
                    LockoutEnabled = false,
                    PhoneNumber = "0829440357",
                    FullName = "Admin",
                    Dob = new Faker().Person.DateOfBirth,
                    Gender = Gender.MALE,
                    Bio = new Faker().Lorem.ToString(),
                    AvatarId = adminAvatar.Id,
                    Status = UserStatus.ACTIVE,
                };
                var result = await _userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync("admin@gmail.com");
                    await _userManager.AddToRoleAsync(user, UserRole.ADMIN.GetDisplayName());
                }


                var userFaker = new Faker<User>()
                    .RuleFor(u => u.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(u => u.Email, f => f.Person.Email)
                    .RuleFor(u => u.NormalizedEmail, f => f.Person.Email)
                    .RuleFor(u => u.UserName, f => f.Person.UserName)
                    .RuleFor(u => u.NormalizedUserName, f => f.Person.UserName)
                    .RuleFor(u => u.LockoutEnabled, f => false)
                    .RuleFor(u => u.PhoneNumber, f => f.Phone.PhoneNumber("###-###-####"))
                    .RuleFor(u => u.FullName, f => f.Person.FullName)
                    .RuleFor(u => u.Dob, f => f.Person.DateOfBirth)
                    .RuleFor(u => u.Gender, f => f.PickRandom<Gender>())
                    .RuleFor(u => u.Bio, f => f.Lorem.ToString())
                    .RuleFor(u => u.Status, _ => UserStatus.ACTIVE);

                for (int userIndex = 0; userIndex < MAX_USERS_QUANTITY * 2; userIndex++)
                {
                    var customerAvatar = fakerAvatar.Generate();
                    _context.FileStorages.Add(customerAvatar);
                    userFaker.RuleFor(u => u.AvatarId, _ => customerAvatar.Id);
                    var customer = userFaker.Generate();
                    var customerResult = await _userManager.CreateAsync(customer, "User@123");
                    if (customerResult.Succeeded)
                    {
                        var user = await _userManager.FindByEmailAsync(customer.Email);
                        await _userManager.AddToRolesAsync(user, new List<string> { UserRole.CUSTOMER.GetDisplayName(), UserRole.ORGANIZER.GetDisplayName() });
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedFunctions()
        {
            if (!_context.Functions.Any())
            {
                _context.Functions.AddRange(new List<Function>
                {
                    new Function { Id = FunctionCode.DASHBOARD.GetDisplayName(), Name = "Dashboard", ParentId = null, SortOrder = 0, Url = "/dashboard" },

                    new Function { Id = FunctionCode.CONTENT.GetDisplayName(),Name = "Contents", ParentId = null, SortOrder = 0, Url = "/content" },

                    new Function { Id = FunctionCode.CONTENT_CATEGORY.GetDisplayName(), Name = "Categories", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 1, Url = "/content/category"  },
                    new Function { Id = FunctionCode.CONTENT_EVENT.GetDisplayName(), Name = "Events", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 1, Url = "/content/event" },
                    new Function { Id = FunctionCode.CONTENT_REVIEW.GetDisplayName(), Name = "Reviews", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 2, Url = "/content/review" },
                    new Function { Id = FunctionCode.CONTENT_TICKET.GetDisplayName(), Name = "Tickets", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 2, Url = "/content/ticket" },
                    new Function { Id = FunctionCode.CONTENT_CHAT.GetDisplayName(), Name = "Chats", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 2, Url = "/content/chat" },
                    new Function { Id = FunctionCode.CONTENT_PAYMENT.GetDisplayName(), Name = "Payments", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 2, Url = "/content/payment" },

                    new Function { Id = FunctionCode.STATISTIC.GetDisplayName(), Name = "Statistics", ParentId = null, SortOrder = 0, Url = "/statistic" },

                    new Function { Id = FunctionCode.SYSTEM.GetDisplayName(), Name = "System", ParentId = null, SortOrder = 0, Url = "/system" },

                    new Function { Id = FunctionCode.SYSTEM_USER.GetDisplayName(), Name = "Users", ParentId = FunctionCode.SYSTEM.GetDisplayName(), SortOrder = 1, Url = "/system/user" },
                    new Function { Id = FunctionCode.SYSTEM_ROLE.GetDisplayName(), Name = "Roles", ParentId = FunctionCode.SYSTEM.GetDisplayName(), SortOrder = 1, Url = "/system/role" },
                    new Function { Id = FunctionCode.SYSTEM_FUNCTION.GetDisplayName(), Name = "Functions", ParentId = FunctionCode.SYSTEM.GetDisplayName(), SortOrder = 1, Url = "/system/function" },
                    new Function { Id = FunctionCode.SYSTEM_PERMISSION.GetDisplayName(), Name = "Permissions", ParentId = FunctionCode.SYSTEM.GetDisplayName(), SortOrder = 1,Url = "/system/permission" },

                });

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedCommands()
        {
            if (!_context.Commands.Any())
            {
                _context.Commands.AddRange(new List<Command>()
                {
                    new Command() { Id = "VIEW", Name = "View" },
                    new Command() { Id = "CREATE", Name = "Create" },
                    new Command() { Id = "UPDATE", Name = "Update" },
                    new Command() { Id = "DELETE", Name = "Delete" },
                    new Command() { Id = "APPROVE", Name = "Approve" },
                });

                await _context.SaveChangesAsync();
            }

            if (!_context.CommandInFunctions.Any())
            {
                var functions = _context.Functions;

                foreach (var function in functions)
                {
                    var createAction = new CommandInFunction()
                    {
                        CommandId = "CREATE",
                        FunctionId = function.Id
                    };
                    _context.CommandInFunctions.Add(createAction);

                    var updateAction = new CommandInFunction()
                    {
                        CommandId = "UPDATE",
                        FunctionId = function.Id
                    };
                    _context.CommandInFunctions.Add(updateAction);
                    var deleteAction = new CommandInFunction()
                    {
                        CommandId = "DELETE",
                        FunctionId = function.Id
                    };
                    _context.CommandInFunctions.Add(deleteAction);

                    var viewAction = new CommandInFunction()
                    {
                        CommandId = "VIEW",
                        FunctionId = function.Id
                    };
                    _context.CommandInFunctions.Add(viewAction);
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedPermission()
        {
            if (!_context.Permissions.Any())
            {
                var functions = _context.Functions;
                var adminRole = await _roleManager.FindByNameAsync(UserRole.ADMIN.GetDisplayName());
                foreach (var function in functions)
                {
                    _context.Permissions.Add(new Permission(function.Id, adminRole.Id, "CREATE"));
                    _context.Permissions.Add(new Permission(function.Id, adminRole.Id, "UPDATE"));
                    _context.Permissions.Add(new Permission(function.Id, adminRole.Id, "DELETE"));
                    _context.Permissions.Add(new Permission(function.Id, adminRole.Id, "VIEW"));
                }

                var customerRole = await _roleManager.FindByNameAsync(UserRole.CUSTOMER.GetDisplayName());
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), customerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), customerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), customerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CATEGORY.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_EVENT.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), customerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), customerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), customerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_TICKET.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_REVIEW.GetDisplayName(), customerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_REVIEW.GetDisplayName(), customerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_REVIEW.GetDisplayName(), customerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_REVIEW.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), customerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), customerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), customerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.SYSTEM.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.SYSTEM.GetDisplayName(), customerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.SYSTEM_USER.GetDisplayName(), customerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.SYSTEM_USER.GetDisplayName(), customerRole.Id, "UPDATE"));

                var organizerRole = await _roleManager.FindByNameAsync(UserRole.ORGANIZER.GetDisplayName());
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), organizerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), organizerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), organizerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CATEGORY.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_EVENT.GetDisplayName(), organizerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_EVENT.GetDisplayName(), organizerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_EVENT.GetDisplayName(), organizerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_EVENT.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), organizerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), organizerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_PAYMENT.GetDisplayName(), organizerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_TICKET.GetDisplayName(), organizerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_TICKET.GetDisplayName(), organizerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_TICKET.GetDisplayName(), organizerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_TICKET.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_REVIEW.GetDisplayName(), organizerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_REVIEW.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), organizerRole.Id, "UPDATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), organizerRole.Id, "CREATE"));
                _context.Permissions.Add(new Permission(FunctionCode.CONTENT_CHAT.GetDisplayName(), organizerRole.Id, "DELETE"));
                _context.Permissions.Add(new Permission(FunctionCode.SYSTEM_USER.GetDisplayName(), organizerRole.Id, "VIEW"));
                _context.Permissions.Add(new Permission(FunctionCode.SYSTEM_USER.GetDisplayName(), organizerRole.Id, "UPDATE"));

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedCategories()
        {
            if (!_context.Categories.Any())
            {
                #region Categories Data
                // WARNING: Must not change the items' order, just add more!
                var categoryNames = new List<string> { "Academic", "Anniversary", "Charities", "Community", "Concerts", "Conferences", "Fashion", "Festivals & Fairs", "Film", "Food & Drink", "Holidays", "Kids & Family", "Lectures & Books", "Music", "Nightlife", "Other", "Performing Arts", "Politics", "Sports & Active Life", "Visual Arts" };

                var icons = new List<FileStorage>()
                {
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "academic.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/academic.png?sp=r&st=2024-04-07T03:31:12Z&se=2025-07-04T11:31:12Z&spr=https&sv=2022-11-02&sr=b&sig=yOhX7zG3VQx7pWybAUPGxmQUp96XN5TBhT8DYburC3I%3D", FileSize = 1761 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "anniversary.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/anniversary.png?sp=r&st=2024-04-07T03:46:23Z&se=2025-07-04T11:46:23Z&spr=https&sv=2022-11-02&sr=b&sig=zgTti9FF9GlRhpy%2Fjtpf5HjMoYxzbuZDMGN3t89%2FvzU%3D", FileSize = 4375 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "charities.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/charities.png?sp=r&st=2024-04-07T03:46:44Z&se=2025-04-07T11:46:44Z&spr=https&sv=2022-11-02&sr=b&sig=cBzcgm1fDRph7tzAK24OtniOpPAFPpCh0K643SYS0YA%3D", FileSize = 3203 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "communities.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/communities.png?sp=r&st=2024-04-07T03:47:01Z&se=2025-07-04T11:47:01Z&spr=https&sv=2022-11-02&sr=b&sig=HfnVkw390QGSI63DwIbK2d3EaZUo%2B%2FebDswsf1p2KQ8%3D", FileSize = 3944 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "concerts.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/concerts.png?sp=r&st=2024-04-07T03:47:21Z&se=2025-04-07T11:47:21Z&spr=https&sv=2022-11-02&sr=b&sig=8a%2BAwYyuwbHTgARKss2gbSvvayqQl%2FpTmqiq%2Beu13Tc%3D", FileSize = 4407 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "conferences.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/conferences.png?sp=r&st=2024-04-07T03:47:39Z&se=2025-04-07T11:47:39Z&spr=https&sv=2022-11-02&sr=b&sig=uUeJIxpeXoazrGWlRFA2zL3QW0Wi9SLaCOK9K8UgEkg%3D", FileSize = 2712 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "fashion.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/fashion.png?sp=r&st=2024-04-07T03:47:56Z&se=2025-04-07T11:47:56Z&spr=https&sv=2022-11-02&sr=b&sig=KYJ5gPT8LUycfqmw5aRhYIV%2BgS4Z919qqd3UdBKY3Fs%3D", FileSize = 4229 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "festivals-and-fairs.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/festivals-and-fairs.png?sp=r&st=2024-04-07T03:48:11Z&se=2025-04-07T11:48:11Z&spr=https&sv=2022-11-02&sr=b&sig=VsxNlIz64dEHh6%2FkQxRQrRpKcgIXbw4x9ugzS3rMAjs%3D", FileSize = 4505 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "film.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/film.png?sp=r&st=2024-04-07T03:48:25Z&se=2025-07-04T11:48:25Z&spr=https&sv=2022-11-02&sr=b&sig=dLbaWe8U1MWU%2F2x3nshQb9MJ7ODE18nuroSt0YiAtHY%3D", FileSize = 4161 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "food-and-drink.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/food-and-drink.png?sp=r&st=2024-04-07T03:48:46Z&se=2025-04-07T11:48:46Z&spr=https&sv=2022-11-02&sr=b&sig=LL42HfnOHw%2FZAu3pjNHecbZgEOcr6Geva7K%2B4WztJUY%3D", FileSize = 2431 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "holidays.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/holidays.png?sp=r&st=2024-04-07T03:49:02Z&se=2025-04-07T11:49:02Z&spr=https&sv=2022-11-02&sr=b&sig=DlpJfJ32Y9ZuaJ7AaoNVhhKYNNk55i2OKn1tOe90rJ8%3D", FileSize = 4635 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "kids-and-family.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/kids-and-family.png?sp=r&st=2024-04-07T03:49:20Z&se=2025-04-07T11:49:20Z&spr=https&sv=2022-11-02&sr=b&sig=0Y9FimXwlLujB3QdivNoma6ZCdKee0JNhWDDXQV84h4%3D", FileSize = 3683 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "lectures-and-books.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/lectures-and-books.png?sp=r&st=2024-04-07T03:49:40Z&se=2025-04-07T11:49:40Z&spr=https&sv=2022-11-02&sr=b&sig=BThGo%2BqARKO%2BuKxCfSw3NPB4lVw%2BcWhOb26HB3nKnSY%3D", FileSize = 2404 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "music.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/music.png?sp=r&st=2024-04-07T03:49:58Z&se=2025-04-07T11:49:58Z&spr=https&sv=2022-11-02&sr=b&sig=rY0M%2FbUszfOfWAcehEarOJGL4jwcRlL9E1GTthcfKQY%3D", FileSize = 2832 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "nightlife.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/nightlife.png?sp=r&st=2024-04-07T03:51:33Z&se=2025-04-07T11:51:33Z&spr=https&sv=2022-11-02&sr=b&sig=Zj2er67XbLMVz1Sy2892il79%2FBCiZu4ybYZacF0gznQ%3D", FileSize = 2344 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "other.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/other.png?sp=r&st=2024-04-07T03:50:32Z&se=2025-04-07T11:50:32Z&spr=https&sv=2022-11-02&sr=b&sig=DCM7%2Fhc5qjRN0CHRXMV95TlJoJrt9GZZN6mpRXoa1j4%3D", FileSize = 860 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "performing-arts.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/performing-arts.png?sp=r&st=2024-04-07T03:50:46Z&se=2025-04-07T11:50:46Z&spr=https&sv=2022-11-02&sr=b&sig=F4GoIhCahUO4%2BE7zdvFrRjWaP6KRignel%2FzA0eZxnmk%3D", FileSize = 2991 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "politics.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/politics.png?sp=r&st=2024-04-07T03:51:01Z&se=2025-04-07T11:51:01Z&spr=https&sv=2022-11-02&sr=b&sig=ZAgVqondk2RfwyBTLYdw6EnyysjrBaVVofIosPMD%2F9o%3D", FileSize = 2197 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "sports-and-active-life.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/sports-and-active-life.png?sp=r&st=2024-04-07T03:52:23Z&se=2025-04-07T11:52:23Z&spr=https&sv=2022-11-02&sr=b&sig=Oy1lN5E5QHP6BXhyMhvpuZJzHp70aeVr2dBced8tQ%2F4%3D", FileSize = 6994 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.CATEGORIES, FileName = "visual-arts.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/categories/visual-arts.png?sp=r&st=2024-04-07T03:52:38Z&se=2025-07-04T11:52:38Z&spr=https&sv=2022-11-02&sr=b&sig=qyh98jEeqJYLOd7HEeHsqjegC%2FoyH3fRrh4Gjl5HkA0%3D", FileSize = 3421 },
                };
                _context.FileStorages.AddRange(icons);
                #endregion

                var fakerCategory = new Faker<Category>()
                    .RuleFor(c => c.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(c => c.Color, f => f.Commerce.Color());

                var categories = new List<Category>();
                for (int i = 0; i < MAX_CATEGORIES_QUANTITY; i++)
                {
                    fakerCategory
                        .RuleFor(c => c.Name, _ => categoryNames[i])
                        .RuleFor(c => c.IconImageId, _ => icons[i].Id);

                    var category = fakerCategory.Generate();
                    categories.Add(category);
                }

                _context.Categories.AddRange(categories);

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedPaymentMethods()
        {
            if (!_context.PaymentMethods.Any())
            {
                #region Categories Data
                // WARNING: Must not change the items' order, just add more!
                var bankNames = new List<string> { "ACB", "Agribank", "BIDV", "HDBank", "MBBank", "Momo", "MSB", "OceanBank", "Sacombank", "SCB", "SeABank", "SHB", "Techcombank", "TPBank", "Vietcombank", "Vietinbank", "VPBank", "ZaloPay" };

                var icons = new List<FileStorage>()
                {
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "acb.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/acb.png?sp=r&st=2024-05-14T09:26:36Z&se=2024-05-14T17:26:36Z&sv=2022-11-02&sr=b&sig=qY57vVOpbLW3VMhyJajAlg8%2Ff2OK0DQOKMQD3kRhWts%3D", FileSize = 1761 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "agribank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/agribank.png?sp=r&st=2024-05-14T09:26:54Z&se=2024-05-14T17:26:54Z&sv=2022-11-02&sr=b&sig=XOC6pGKHrzjHKdMLgCBPiaUMTi6p%2F%2FZRtjdeANRn%2Bz8%3D", FileSize = 4375 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "bidv.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/bidv.png?sp=r&st=2024-05-14T09:27:08Z&se=2024-05-14T17:27:08Z&sv=2022-11-02&sr=b&sig=E8qLtCkDQf1Akb3XQQc9UjtwbhxB8gpzCV17rxTG%2B5M%3D", FileSize = 3203 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "hdbank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/hdbank.png?sp=r&st=2024-05-14T09:27:24Z&se=2024-05-14T17:27:24Z&sv=2022-11-02&sr=b&sig=Kx0IioXDWWjPBCFU2AETz5VgpfqpYSdoOiI575dGh8g%3D", FileSize = 3944 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "mbb.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/mbb.png?sp=r&st=2024-05-14T09:27:44Z&se=2024-05-14T17:27:44Z&sv=2022-11-02&sr=b&sig=nyZpiO7z%2FYU08qeCJWxqGJNMSGZ7kluKf915X79WnJU%3D", FileSize = 4407 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "momo.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/momo.png?sp=r&st=2024-05-14T09:28:00Z&se=2024-05-14T17:28:00Z&sv=2022-11-02&sr=b&sig=a9uXIMkoPkATHalLVE%2FZFzwYSTuEWX%2FdeCxrbXrW5nU%3D", FileSize = 2712 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "msb.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/msb.png?sp=r&st=2024-05-14T09:28:17Z&se=2024-05-14T17:28:17Z&sv=2022-11-02&sr=b&sig=9oagSF1ULUN3dIhsZr5BzDxEnR%2BM156phj6xGDSHO1s%3D", FileSize = 4229 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "oceanbank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/oceanbank.png?sp=r&st=2024-05-14T09:28:38Z&se=2024-05-14T17:28:38Z&sv=2022-11-02&sr=b&sig=jzRy%2FMHzFSxMTF9gKjN8%2FbnzaAcJVeJBv81Yzml6lWE%3D", FileSize = 4505 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "sacombank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/sacombank.png?sp=r&st=2024-05-14T09:28:54Z&se=2024-05-14T17:28:54Z&sv=2022-11-02&sr=b&sig=5oT00ylfxc%2FuS9D4j1BldQzyi7wDD3ptRgFbE8R0HXQ%3D", FileSize = 4161 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "scb.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/scb.png?sp=r&st=2024-05-14T09:29:07Z&se=2024-05-14T17:29:07Z&sv=2022-11-02&sr=b&sig=upYO0NKivoGbi%2FyfU%2BuBrJPD%2BXBO2L1XxpjCV43MjwQ%3D", FileSize = 2431 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "seabank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/seabank.png?sp=r&st=2024-05-14T09:29:23Z&se=2024-05-14T17:29:23Z&sv=2022-11-02&sr=b&sig=JcKkdvXQZ4RZigkVbsD09wKsk6akd7IqHnjwH9fIWV0%3D", FileSize = 4635 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "shb.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/shb.png?sp=r&st=2024-05-14T09:29:37Z&se=2024-05-14T17:29:37Z&sv=2022-11-02&sr=b&sig=pkBOYI4U3fUDF8LtyKwDKg%2FbLP8kicn79uA%2FEZxdi9k%3D", FileSize = 3683 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "tcb.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/tcb.png?sp=r&st=2024-05-14T09:29:54Z&se=2024-05-14T17:29:54Z&sv=2022-11-02&sr=b&sig=Tc90anLMq%2BFxQ0h%2Bn%2BV8iqLIIRWAFFxv6%2BBfCdd8LYI%3D", FileSize = 2404 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "tpbank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/tpbank.png?sp=r&st=2024-05-14T09:30:07Z&se=2024-05-14T17:30:07Z&sv=2022-11-02&sr=b&sig=XpbaI2gXUAXM1sbEVANbXxp0IxIBdC86t9qCSgaIkjw%3D", FileSize = 2832 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "vietcombank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/vietcombank.png?sp=r&st=2024-05-14T09:30:19Z&se=2024-05-14T17:30:19Z&sv=2022-11-02&sr=b&sig=DB2xzBtfu3YceSCRJ18DiA4u6fNx4KaKviDx1d3rcW0%3D", FileSize = 2344 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "vietinbank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/vietinbank.png?sp=r&st=2024-05-14T09:30:37Z&se=2024-05-14T17:30:37Z&sv=2022-11-02&sr=b&sig=1smGwC9173bZ4fp2GYm6FGnszCvIjKmtH7UHEmQ0iPQ%3D", FileSize = 860 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "vpbank.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/vpbank.png?sp=r&st=2024-05-14T09:30:52Z&se=2024-05-14T17:30:52Z&sv=2022-11-02&sr=b&sig=bgHZEOJ1qr914PzudzIMpyCpl%2BjdKErLOwXdo1GMR%2Fo%3D", FileSize = 2991 },
                    new FileStorage() { Id = Guid.NewGuid().ToString(), FileContainer = FileContainer.BANKS, FileName = "zalopay.png", FileType = "image/png", FilePath = "https://eventhubazureblobstorage.blob.core.windows.net/files/banks/zalopay.png?sp=r&st=2024-05-14T09:31:06Z&se=2024-05-14T17:31:06Z&sv=2022-11-02&sr=b&sig=V5budADtln%2BgliRJaglu2zdZnZq7KcHAogl33Lc8aEw%3D", FileSize = 2197 },
                };
                _context.FileStorages.AddRange(icons);
                #endregion

                var paymentMethods = new List<PaymentMethod>();
                for (int i = 0; i < bankNames.Count; i++)
                {
                    var method = new PaymentMethod
                    {
                        Id = Guid.NewGuid().ToString(),
                        MethodLogoId = icons[i].Id,
                        MethodName = bankNames[i]
                    };
                    _context.PaymentMethods.Add(method);
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedEvents()
        {
            if (!_context.Events.Any())
            {
                var users = _userManager.Users;
                var categories = _context.Categories;

                var fakerEvent = new Faker<Event>()
                    .RuleFor(e => e.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(e => e.CreatorId, f => f.PickRandom<User>(users).Id)
                    .RuleFor(e => e.Name, f => f.Commerce.ProductName())
                    .RuleFor(e => e.Description, f => f.Commerce.ProductDescription())
                    .RuleFor(e => e.Promotion, f => f.Random.Double(0.0, 1.0))
                    .RuleFor(e => e.Location, f => f.Address.FullAddress())
                    .RuleFor(e => e.EventPaymentType, f => f.Random.Enum<EventPaymentType>())
                    .RuleFor(e => e.EventCycleType, f => f.Random.Enum<EventCycleType>());

                var fakerTicketType = new Faker<TicketType>()
                    .RuleFor(t => t.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(t => t.Name, f => f.Commerce.ProductMaterial())
                    .RuleFor(t => t.Quantity, f => f.Random.Int(0, 1000))
                    .RuleFor(t => t.Price, f => f.Random.Long(0, 100000000));

                var fakerEmailContent = new Faker<EmailContent>()
                    .RuleFor(e => e.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(e => e.Content, _ => "<p>Dear attendee,<br>Thank you for attending our event! We hope you found it informative and enjoyable<br>Best regards,<br>EventHub</p>");

                var fakerCoverImage = new Faker<FileStorage>()
                        .RuleFor(f => f.Id, _ => Guid.NewGuid().ToString())
                        .RuleFor(f => f.FileName, f => $"{f.Lorem.Word()}.png")
                        .RuleFor(f => f.FileType, _ => "image/png")
                        .RuleFor(f => f.FileContainer, _ => FileContainer.EVENTS)
                        .RuleFor(f => f.FileSize, f => f.Random.Int(1000, 5000))
                        .RuleFor(f => f.FilePath, f => f.Image.Business(1024, 768));

                var fakerSubImage = new Faker<FileStorage>()
                        .RuleFor(f => f.Id, _ => Guid.NewGuid().ToString())
                        .RuleFor(f => f.FileName, f => $"{f.Lorem.Word()}.png")
                        .RuleFor(f => f.FileType, _ => "image/png")
                        .RuleFor(f => f.FileContainer, _ => FileContainer.EVENTS)
                        .RuleFor(f => f.FileSize, f => f.Random.Int(1000, 5000))
                        .RuleFor(f => f.FilePath, f => f.Image.Business(640, 640));

                var fakerReason = new Faker<Reason>()
                        .RuleFor(f => f.Id, _ => Guid.NewGuid().ToString())
                        .RuleFor(t => t.Name, f => f.Commerce.ProductDescription());

                var fakerEventCategory = new Faker<EventCategory>()
                    .RuleFor(ec => ec.CategoryId, f => f.PickRandom<Category>(categories).Id);

                for (int eventIndex = 0; eventIndex < MAX_EVENTS_QUANTITY; eventIndex++)
                {
                    #region EventTime
                    var eventStartTime = DateTime.UtcNow.Subtract(TimeSpan.FromDays(new Faker().Random.Number(0, 60)));
                    var eventEndTime = DateTime.UtcNow.Add(TimeSpan.FromDays(new Faker().Random.Number(1, 60)));
                    var eventItem = fakerEvent.Generate();
                    eventItem.StartTime = eventStartTime;
                    eventItem.EndTime = eventEndTime;
                    if (eventItem.StartTime <= DateTime.UtcNow && DateTime.UtcNow <= eventItem.EndTime)
                        eventItem.Status = EventStatus.OPENING;
                    else if (DateTime.UtcNow < eventItem.StartTime)
                        eventItem.Status = EventStatus.UPCOMING;
                    else
                        eventItem.Status = EventStatus.CLOSED;
                    #endregion

                    #region CoverImage
                    var coverImage = fakerCoverImage.Generate();
                    _context.FileStorages.Add(coverImage);
                    eventItem.CoverImageId = coverImage.Id;
                    #endregion

                    _context.Events.Add(eventItem);

                    #region EmailContent
                    fakerEmailContent.RuleFor(ec => ec.EventId, _ => eventItem.Id);
                    var emailContent = fakerEmailContent.Generate();
                    _context.EmailContents.Add(emailContent);
                    #endregion

                    #region EventCategories
                    fakerEventCategory.RuleFor(ec => ec.EventId, _ => eventItem.Id);
                    var eventCategories = fakerEventCategory.GenerateBetween(1, 3).DistinctBy(ec => ec.CategoryId);
                    _context.EventCategories.AddRange(eventCategories);
                    #endregion

                    #region EventSubImages
                    var subImages = fakerSubImage.Generate(5);
                    subImages.ForEach(image =>
                    {
                        _context.FileStorages.Add(image);
                        var eventSubImage = new EventSubImage()
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventId = eventItem.Id,
                            ImageId = image.Id
                        };
                        _context.EventSubImages.Add(eventSubImage);
                    });
                    #endregion

                    #region TicketTypes
                    fakerTicketType.RuleFor(t => t.EventId, _ => eventItem.Id);
                    var ticketTypes = fakerTicketType.Generate(3);
                    _context.TicketTypes.AddRange(ticketTypes);
                    #endregion

                    #region Reasons
                    fakerReason.RuleFor(t => t.EventId, _ => eventItem.Id);
                    var reasons = fakerReason.Generate(3);
                    _context.Reasons.AddRange(reasons);
                    #endregion

                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedReviews()
        {
            if (!_context.Reviews.Any())
            {
                var users = _userManager.Users;
                var events = _context.Events;

                var fakerReview = new Faker<Review>()
                    .RuleFor(e => e.Id, _ => Guid.NewGuid().ToString())
                    .RuleFor(e => e.Content, f => f.Lorem.Text())
                    .RuleFor(e => e.Rate, f => f.Random.Double(0.0, 5.0))
                    .RuleFor(e => e.EventId, f => f.PickRandom<Event>(events).Id)
                    .RuleFor(e => e.UserId, f => f.PickRandom<User>(users).Id);

                var reviews = fakerReview.Generate(1000);
                _context.Reviews.AddRange(reviews);

                await _context.SaveChangesAsync();
            }
        }
    }
}

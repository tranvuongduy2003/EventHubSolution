using Bogus;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.ViewModels.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Extensions;

namespace EventHubSolution.BackendServer.Data
{
    public class DbInitializer
    {
        private const int MAX_EVENTS_QUANTITY = 1000;
        private const int MAX_USERS_QUANTITY = 10;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

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
            await SeedRoles();
            await SeedUsers();
            await SeedFunctions();
            await SeedCommands();
            await SeedPermission();
            await SeedCategories();
            await SeedEvents();
            await SeedReviews();

            await _context.SaveChangesAsync();
        }

        private async Task SeedRoles()
        {
            if (!_roleManager.Roles.Any())
            {
                await _roleManager.CreateAsync(new IdentityRole()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = UserRole.ADMIN.GetDisplayName(),
                    ConcurrencyStamp = "1",
                    NormalizedName = UserRole.ADMIN.GetDisplayName().Normalize()
                });
                await _roleManager.CreateAsync(new IdentityRole()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = UserRole.CUSTOMER.GetDisplayName(),
                    ConcurrencyStamp = "2",
                    NormalizedName = UserRole.CUSTOMER.GetDisplayName().Normalize()
                });
                await _roleManager.CreateAsync(new IdentityRole()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = UserRole.ORGANIZER.GetDisplayName(),
                    ConcurrencyStamp = "3",
                    NormalizedName = UserRole.ORGANIZER.GetDisplayName().Normalize()
                });
            }
        }

        private async Task SeedUsers()
        {
            if (!_userManager.Users.Any())
            {
                _context.FileStorages.Add(new FileStorage
                {
                    Id = Guid.NewGuid().ToString(),
                });

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
                    AvatarId = Guid.NewGuid().ToString(), // TODO: generate avatar
                    Status = UserStatus.ACTIVE,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
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
                    .RuleFor(u => u.AvatarId, f => f.Person.Avatar) // TODO: generate avatar
                    .RuleFor(u => u.Status, _ => UserStatus.ACTIVE)
                    .RuleFor(u => u.CreatedAt, _ => DateTime.Now)
                    .RuleFor(u => u.UpdatedAt, _ => DateTime.Now);

                for (int userIndex = 0; userIndex < MAX_USERS_QUANTITY * 2; userIndex++)
                {
                    var customer = userFaker.Generate();
                    var customerResult = await _userManager.CreateAsync(customer, "User@123");
                    if (customerResult.Succeeded)
                    {
                        var user = await _userManager.FindByEmailAsync(customer.Email);
                        await _userManager.AddToRolesAsync(user, new List<string> { UserRole.CUSTOMER.GetDisplayName(), UserRole.ORGANIZER.GetDisplayName() });
                    }
                }
            }
        }

        private async Task SeedFunctions()
        {
            if (!_context.Functions.Any())
            {
                _context.Functions.AddRange(new List<Function>
                {
                    new Function { Id = FunctionCode.DASHBOARD.GetDisplayName(), Name = "Thống kê", ParentId = null, SortOrder = 1, Url = "/dashboard" },

                    new Function { Id = FunctionCode.CONTENT.GetDisplayName(),Name = "Nội dung", ParentId = null, Url = "/content" },

                    new Function { Id = FunctionCode.CONTENT_CATEGORY.GetDisplayName(), Name = "Danh mục", ParentId = FunctionCode.CONTENT.GetDisplayName(), Url = "/content/category"  },
                    new Function { Id = FunctionCode.CONTENT_EVENT.GetDisplayName(), Name = "Sự kiện", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 2, Url = "/content/event" },
                    new Function { Id = FunctionCode.CONTENT_REVIEW.GetDisplayName(), Name = "Đánh giá", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 3, Url = "/content/review" },
                    new Function { Id = FunctionCode.CONTENT_TICKET.GetDisplayName(), Name = "Vé", ParentId = FunctionCode.CONTENT.GetDisplayName(), SortOrder = 3, Url = "/content/ticket" },

                    new Function { Id = "STATISTIC", Name = "Thống kê", ParentId = null, Url = "/statistic" },

                    new Function { Id = "SYSTEM", Name = "Hệ thống", ParentId = null, Url = "/system" },

                    new Function { Id = "SYSTEM_USER", Name = "Người dùng", ParentId = "SYSTEM", Url = "/system/user" },
                    new Function { Id = "SYSTEM_ROLE", Name = "Nhóm quyền", ParentId = "SYSTEM", Url = "/system/role" },
                    new Function { Id = "SYSTEM_FUNCTION", Name = "Chức năng", ParentId = "SYSTEM", Url = "/system/function" },
                    new Function { Id = "SYSTEM_PERMISSION", Name = "Quyền hạn", ParentId = "SYSTEM", Url = "/system/permission" },
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
                    new Command() { Id = "VIEW", Name = "Xem" },
                    new Command() { Id = "CREATE", Name = "Thêm" },
                    new Command() { Id = "UPDATE", Name = "Sửa" },
                    new Command() { Id = "DELETE", Name = "Xoá" },
                    new Command() { Id = "APPROVE", Name = "Duyệt" },
                });
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
            }
        }

        private async Task SeedCategories()
        {
            if (!_context.Categories.Any())
            {
                var categories = new List<Category>()
                {
                    new Category()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Âm nhạc",
                        Color = "#264653",
                        IconImageId = Guid.NewGuid().ToString(), // TODO: generate icon image
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    },
                    new Category()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Thể thao",
                        Color = "#2a9d8f",
                        IconImageId = Guid.NewGuid().ToString(), // TODO: generate icon image
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    },
                    new Category()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Hội họa",
                        Color = "#e9c46a",
                        IconImageId = Guid.NewGuid().ToString(), // TODO: generate icon image
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    },
                    new Category()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Doanh nghệp",
                        Color = "#f4a261",
                        IconImageId = Guid.NewGuid().ToString(), // TODO: generate icon image
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    },
                    new Category()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Nhiếp ảnh",
                        Color = "#e76f51",
                        IconImageId = Guid.NewGuid().ToString(), // TODO: generate icon image
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    }
                };

                _context.Categories.AddRange(categories);
            }
        }

        private async Task SeedEvents()
        {
            var users = _userManager.Users;

            var fakerEvent = new Faker<Event>()
                .RuleFor(e => e.Id, _ => Guid.NewGuid().ToString())
                .RuleFor(e => e.CreatorId, f => f.PickRandom<User>(users).Id)
                .RuleFor(e => e.Name, f => f.Commerce.ProductName())
                .RuleFor(e => e.Description, f => f.Commerce.ProductDescription())
                .RuleFor(e => e.Promotion, f => f.Random.Double(0.0, 1.0))
                .RuleFor(e => e.CreatedAt, _ => DateTime.Now)
                .RuleFor(e => e.UpdatedAt, _ => DateTime.Now);

            var fakerLocation = new Faker<Location>()
                .RuleFor(l => l.Id, _ => Guid.NewGuid().ToString())
                .RuleFor(l => l.City, f => f.Address.City())
                .RuleFor(l => l.District, f => f.Address.State())
                .RuleFor(l => l.Street, f => f.Address.StreetAddress())
                .RuleFor(l => l.LongitudeX, f => f.Address.Longitude())
                .RuleFor(l => l.LatitudeY, f => f.Address.Latitude());

            var fakerTicketType = new Faker<TicketType>()
                .RuleFor(t => t.Id, _ => Guid.NewGuid().ToString())
                .RuleFor(t => t.Name, f => f.Commerce.ProductMaterial())
                .RuleFor(t => t.Quantity, f => f.Random.Int(0, 1000))
                .RuleFor(t => t.Price, f => f.Random.Long(0, 100000000));

            var fakerEmailContent = new Faker<EmailContent>()
                .RuleFor(e => e.Id, _ => Guid.NewGuid().ToString())
                .RuleFor(e => e.Content, _ => "<p>Dear attendee,<br>Thank you for attending our event! We hope you found it informative and enjoyable<br>Best regards,<br>EventHub</p>");

            for (int eventIndex = 0; eventIndex < MAX_EVENTS_QUANTITY; eventIndex++)
            {
                var eventStartTime = DateTime.Now.Subtract(TimeSpan.FromDays(new Faker().Random.Number(0, 60)));
                var eventEndTime = DateTime.Now.Add(TimeSpan.FromDays(new Faker().Random.Number(1, 60)));
                var eventItem = fakerEvent.Generate();
                eventItem.StartTime = eventStartTime;
                eventItem.EndTime = eventEndTime;
                if (eventItem.StartTime <= DateTime.UtcNow && DateTime.UtcNow <= eventItem.EndTime)
                    eventItem.Status = EventStatus.OPENING;
                else if (DateTime.UtcNow < eventItem.StartTime)
                    eventItem.Status = EventStatus.UPCOMING;
                else
                    eventItem.Status = EventStatus.CLOSED;

                #region Location
                var location = fakerLocation.Generate();
                _context.Locations.Add(location);
                eventItem.LocationId = location.Id;
                #endregion

                #region Albums
                // TODO: Generate albums
                #endregion

                #region TicketTypes
                fakerTicketType.RuleFor(t => t.Id, _ => eventItem.Id);
                var ticketTypes = fakerTicketType.Generate(3);
                _context.TicketTypes.AddRange(ticketTypes);
                #endregion

                #region EmailContent
                fakerEmailContent.RuleFor(e => e.Id, _ => eventItem.Id);
                var emailContent = fakerEmailContent.Generate();
                _context.EmailContents.Add(emailContent);
                eventItem.EmailContentId = emailContent.Id;
                #endregion

                _context.Events.Add(eventItem);
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
                    .RuleFor(e => e.UserId, f => f.PickRandom<User>(users).Id)
                    .RuleFor(e => e.CreatedAt, _ => DateTime.Now)
                    .RuleFor(e => e.UpdatedAt, _ => DateTime.Now);

                var reviews = fakerReview.Generate(100000);
                _context.Reviews.AddRange(reviews);
            }
        }
    }
}

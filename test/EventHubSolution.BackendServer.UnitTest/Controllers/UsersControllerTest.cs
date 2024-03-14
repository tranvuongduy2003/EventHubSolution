using EventHubSolution.BackendServer.Controllers;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.Moq;
using Moq;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.UnitTest.Controllers
{
    public class UsersControllerTest
    {
        private readonly Mock<UserManager<User>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private ApplicationDbContext _context;
        private Mock<IFileStorageService> _mockFileService;

        private List<User> _userSources = new List<User>()
        {
            new User("1", "test1", "Test 1", "test1@gmail.com", "00111", DateTime.Now),
            new User("2", "test2", "Test 2", "test2@gmail.com", "00111", DateTime.Now),
            new User("3", "test3", "Test 3", "test3@gmail.com", "00111", DateTime.Now),
            new User("4", "test4", "Test 4", "test4@gmail.com", "00111", DateTime.Now)
        };

        private PaginationFilter _filter = new PaginationFilter
        {
            order = PageOrder.ASC,
            page = 1,
            search = "",
            size = 10,
            takeAll = true,
        };

        public UsersControllerTest()
        {
            var userStore = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(userStore.Object, null, null, null, null, null, null, null, null);

            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(roleStore.Object, null, null, null, null);

            _context = new InMemoryDbContextFactory().GetApplicationDbContext();

            _mockFileService = new Mock<IFileStorageService>();
        }

        [Fact]
        public void ShouldCreateInstance_NotNull_Success()
        {
            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            Assert.NotNull(usersController);
        }

        [Fact]
        public async void PostUser_ValidInput_Success()
        {
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            var result = await usersController.PostUser(new UserCreateRequest()
            {
                UserName = "test"
            });

            Assert.NotNull(result);
            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async void PostUser_ValidInput_Failed()
        {
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Failed(new IdentityError[] { }));

            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            var result = await usersController.PostUser(new UserCreateRequest()
            {
                UserName = "test"
            });

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async void GetUsers_HasData_ReturnSuccess()
        {
            _mockUserManager.Setup(x => x.Users).Returns(_userSources.AsQueryable().BuildMock());
            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);

            usersController.ControllerContext = new ControllerContext();
            usersController.ControllerContext.HttpContext = new DefaultHttpContext();

            var result = await usersController.GetUsers(_filter);
            var okResult = result as OkObjectResult;
            var userVms = okResult.Value as Pagination<UserVm>;

            Assert.IsType<OkObjectResult>(result);
            Assert.True(userVms.Items.Count() > 0);
            Assert.Equal(JsonConvert.SerializeObject(userVms.Metadata), usersController.Response.Headers["X-Pagination"]);
        }

        [Fact]
        public async void GetUsers_ThrowException_Failed()
        {
            _mockUserManager.Setup(x => x.Users).Throws<Exception>();

            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);

            await Assert.ThrowsAnyAsync<Exception>(async () => await usersController.GetUsers(_filter));
        }

        [Fact]
        public async void GetById_HasData_ReturnSuccess()
        {
            var user = new User()
            {
                UserName = "test1",
                AvatarId = "avatarId"
            };
            var roles = new List<string>
            {
                "test"
            };
            var avatar = new FileStorage
            {
                Id = "avatarId",
                FileName = "avatar",
                FilePath = "/avatar.png",
                FileSize = 1024,
                FileType = "png"
            };

            _context.FileStorages.Add(avatar);
            await _context.SaveChangesAsync();
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.AddToRolesAsync(user, roles)).ReturnsAsync(It.IsAny<IdentityResult>());
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);

            var result = await usersController.GetById("test1");
            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var userVm = okResult.Value as UserVm;

            Assert.Equal("test1", userVm.UserName);
        }

        [Fact]
        public async void GetById_ThrowException_Failed()
        {
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).Throws<Exception>();

            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);

            await Assert.ThrowsAnyAsync<Exception>(async () => await usersController.GetById("test1"));
        }

        [Fact]
        public async void PutUser_ValidInput_Success()
        {
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new User()
            {
                UserName = "test"
            });

            _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            var result = await usersController.PutUser("test", new UserCreateRequest()
            {
                UserName = "test2"
            });

            Assert.NotNull(result);
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async void PutUser_ValidInput_Failed()
        {
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new User()
            {
                UserName = "test2"
            });

            _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Failed(new IdentityError[] { }));
            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            var result = await usersController.PutUser("test", new UserCreateRequest()
            {
                UserName = "test2"
            });

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async void DeleteUser_ValidInput_Success()
        {
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new User()
            {
                UserName = "test1"
            });

            _mockUserManager.Setup(x => x.DeleteAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            var result = await usersController.DeleteUser("test");
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async void DeleteUser_ValidInput_Failed()
        {
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new User()
            {
                UserName = "test1"
            });

            _mockUserManager.Setup(x => x.DeleteAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Failed(new IdentityError[] { }));
            var usersController = new UsersController(_mockUserManager.Object, _mockRoleManager.Object, _context, _mockFileService.Object);
            var result = await usersController.DeleteUser("test");
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}

using EventHubSolution.ViewModels.Constants;
using EventHubSolution.BackendServer.Controllers;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.Moq;
using Moq;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.UnitTest.Controllers
{
    public class RolesControllerTest
    {
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private ApplicationDbContext _context;

        private List<IdentityRole> _roleSources = new List<IdentityRole>()
        {
            new IdentityRole("test1"),
            new IdentityRole("test2"),
            new IdentityRole("test3"),
            new IdentityRole("test4")
        };

        private PaginationFilter _filter = new PaginationFilter
        {
            order = PageOrder.ASC,
            page = 1,
            search = "",
            size = 10,
            takeAll = true,
        };

        public RolesControllerTest()
        {
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(roleStore.Object, null, null, null, null);
            _context = new InMemoryDbContextFactory().GetApplicationDbContext();
        }

        [Fact]
        public void ShouldCreateInstance_NotNull_Success()
        {
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            Assert.NotNull(rolesController);
        }

        [Fact]
        public async void PostRole_ValidInput_Success()
        {
            _mockRoleManager.Setup(x => x.CreateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.PostRole(new RoleCreateRequest()
            {
                Id = "test",
                Name = "test",
            });

            Assert.NotNull(result);
            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async void PostRole_ValidInput_Failed()
        {
            _mockRoleManager.Setup(x => x.CreateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Failed(new IdentityError[] { }));
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.PostRole(new RoleCreateRequest()
            {
                Id = "test",
                Name = "test",
            });

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async void GetRoles_HasData_ReturnSuccess()
        {
            _mockRoleManager.Setup(x => x.Roles)
                .Returns(_roleSources.AsQueryable().BuildMock());
            var rolesController = new RolesController(_mockRoleManager.Object, _context);

            rolesController.ControllerContext = new ControllerContext();
            rolesController.ControllerContext.HttpContext = new DefaultHttpContext();

            var result = await rolesController.GetRoles(_filter);
            var okResult = result as OkObjectResult;
            var roleVms = okResult.Value as Pagination<RoleVm>;

            Assert.IsType<OkObjectResult>(result);
            Assert.True(roleVms.Items.Count() > 0);
            Assert.Equal(JsonConvert.SerializeObject(roleVms.Metadata), rolesController.Response.Headers["X-Pagination"]);
        }

        [Fact]
        public async void GetRoles_ThrowException_Failed()
        {
            _mockRoleManager.Setup(x => x.Roles).Throws<Exception>();

            var rolesController = new RolesController(_mockRoleManager.Object, _context);

            await Assert.ThrowsAnyAsync<Exception>(async () => await rolesController.GetRoles(_filter));
        }

        [Fact]
        public async void GetById_HasData_ReturnSuccess()
        {
            _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(new IdentityRole()
                {
                    Id = "test1",
                    Name = "test1",
                });
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.GetById("test1");
            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var roleVm = okResult.Value as RoleVm;

            Assert.Equal("test1", roleVm.Name);
        }

        [Fact]
        public async void GetById_ThrowException_Failed()
        {
            _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).Throws<Exception>();

            var rolesController = new RolesController(_mockRoleManager.Object, _context);

            await Assert.ThrowsAnyAsync<Exception>(async () => await rolesController.GetById("test1"));
        }

        [Fact]
        public async void PutRole_ValidInput_Success()
        {
            _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new IdentityRole()
            {
                Id = "test",
                Name = "test",
            });

            _mockRoleManager.Setup(x => x.UpdateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.PutRole("test", new RoleCreateRequest()
            {
                Id = "test",
                Name = "test",
            });

            Assert.NotNull(result);
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async void PutRole_ValidInput_Failed()
        {
            _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new IdentityRole()
            {
                Id = "test",
                Name = "test",
            });

            _mockRoleManager.Setup(x => x.UpdateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Failed(new IdentityError[] { }));
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.PutRole("test", new RoleCreateRequest()
            {
                Id = "test",
                Name = "test",
            });

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async void DeleteRole_ValidInput_Success()
        {
            _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new IdentityRole()
            {
                Id = "test",
                Name = "test",
            });

            _mockRoleManager.Setup(x => x.DeleteAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.DeleteRole("test");
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async void DeleteRole_ValidInput_Failed()
        {
            _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(new IdentityRole()
            {
                Id = "test",
                Name = "test",
            });

            _mockRoleManager.Setup(x => x.DeleteAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Failed(new IdentityError[] { }));
            var rolesController = new RolesController(_mockRoleManager.Object, _context);
            var result = await rolesController.DeleteRole("test");
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}

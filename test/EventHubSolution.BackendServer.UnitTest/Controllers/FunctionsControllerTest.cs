using EventHubSolution.BackendServer.Controllers;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.General;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.UnitTest.Controllers
{
    public class FunctionsControllerTest
    {
        private ApplicationDbContext _context;

        private PaginationFilter _filter = new PaginationFilter
        {
            order = PageOrder.ASC,
            page = 1,
            search = "",
            size = 10,
            takeAll = true,
        };

        public FunctionsControllerTest()
        {
            _context = new InMemoryDbContextFactory().GetApplicationDbContext();
        }

        [Fact]
        public void Should_Create_Instance_Not_Null_Success()
        {
            var functionsController = new FunctionsController(_context);
            Assert.NotNull(functionsController);
        }

        [Fact]
        public async void PostFunction_ValidInput_Success()
        {
            var functionsController = new FunctionsController(_context);
            var result = await functionsController.PostFunction(new FunctionCreateRequest()
            {
                Id = "PostFunction_ValidInput_Success",
                ParentId = null,
                Name = "PostFunction_ValidInput_Success",
                SortOrder = 5,
                Url = "/PostFunction_ValidInput_Success",
            });
            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async void PostFunction_ValidInput_Failed()
        {
            _context.Functions.AddRange(new List<Function>()
            {
                new Function()
                {
                    Id = "PostFunction_ValidInput_Failed",
                    ParentId = null,
                    Name = "PostFunction_ValidInput_Failed",
                    SortOrder = 1,
                    Url = "/PostFunction_ValidInput_Failed",
                },
            });
            await _context.SaveChangesAsync();

            var functionsController = new FunctionsController(_context);
            var result = await functionsController.PostFunction(new FunctionCreateRequest()
            {
                Id = "PostFunction_ValidInput_Failed",
                ParentId = null,
                Name = "PostFunction_ValidInput_Failed",
                SortOrder = 5,
                Url = "/PostFunction_ValidInput_Failed",
            });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async void GetFunctions_HasData_ReturnSuccess()
        {
            _context.Functions.AddRange(new List<Function>()
            {
                new Function()
                {
                    Id = "GetFunctionsPaging_NoFilter_ReturnSuccess1",
                    ParentId = null,
                    Name = "GetFunctionsPaging_NoFilter_ReturnSuccess1",
                    SortOrder = 1,
                    Url = "/GetFunctionsPaging_NoFilter_ReturnSuccess1",
                },
                new Function()
                {
                    Id = "GetFunctionsPaging_NoFilter_ReturnSuccess2",
                    ParentId = null,
                    Name = "GetFunctionsPaging_NoFilter_ReturnSuccess2",
                    SortOrder = 2,
                    Url = "/GetFunctionsPaging_NoFilter_ReturnSuccess2",
                },
                new Function()
                {
                    Id = "GetFunctionsPaging_NoFilter_ReturnSuccess3",
                    ParentId = null,
                    Name = "GetFunctionsPaging_NoFilter_ReturnSuccess3",
                    SortOrder = 3,
                    Url = "/GetFunctionsPaging_NoFilter_ReturnSuccess3",
                },
                new Function()
                {
                    Id = "GetFunctionsPaging_NoFilter_ReturnSuccess4",
                    ParentId = null,
                    SortOrder = 4,
                    Url = "/GetFunctionsPaging_NoFilter_ReturnSuccess4",
                }
            });
            await _context.SaveChangesAsync();

            var functionsController = new FunctionsController(_context);

            functionsController.ControllerContext = new ControllerContext();
            functionsController.ControllerContext.HttpContext = new DefaultHttpContext();

            var result = await functionsController.GetFunctions(_filter);
            var okResult = result as OkObjectResult;
            var functionVms = okResult.Value as Pagination<FunctionVm>;

            Assert.IsType<OkObjectResult>(result);
            Assert.True(functionVms.Items.Count() > 0);
            Assert.Equal(JsonConvert.SerializeObject(functionVms.Metadata), functionsController.Response.Headers["X-Pagination"]);
        }

        [Fact]
        public async void GetById_HasData_ReturnSuccess()
        {
            _context.Functions.AddRange(new List<Function>()
            {
                new Function()
                {
                    Id = "GetById_HasData_ReturnSuccess",
                    ParentId = null,
                    Name = "GetById_HasData_ReturnSuccess",
                    SortOrder = 1,
                    Url = "/GetById_HasData_ReturnSuccess",
                }
            });
            await _context.SaveChangesAsync();

            var functionsController = new FunctionsController(_context);
            var result = await functionsController.GetById("GetById_HasData_ReturnSuccess");
            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);

            var functionVm = okResult.Value as FunctionVm;

            Assert.Equal("GetById_HasData_ReturnSuccess", functionVm.Id);
        }

        [Fact]
        public async void PutFunction_ValidInput_Success()
        {
            _context.Functions.AddRange(new List<Function>()
            {
                new Function()
                {
                    Id = "PutFunction_ValidInput_Success",
                    ParentId = null,
                    Name = "PutFunction_ValidInput_Success",
                    SortOrder = 1,
                    Url = "/PutFunction_ValidInput_Success",
                }
            });
            await _context.SaveChangesAsync();

            var functionsController = new FunctionsController(_context);
            var result = await functionsController.PutFunction("PutFunction_ValidInput_Success", new FunctionCreateRequest()
            {
                Id = "PutFunction_ValidInput_Success6",
                ParentId = null,
                Name = "PutFunction_ValidInput_Success6",
                SortOrder = 6,
                Url = "/PutFunction_ValidInput_Success6",
            });
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async void PutFunction_ValidInput_Failed()
        {
            var functionsController = new FunctionsController(_context);
            var result = await functionsController.PutFunction("PutFunction_ValidInput_Failed", new FunctionCreateRequest()
            {
                Id = "PutFunction_ValidInput_Failed6",
                ParentId = null,
                Name = "PutFunction_ValidInput_Failed6",
                SortOrder = 6,
                Url = "/PutFunction_ValidInput_Failed6",
            });

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async void DeleteFunction_ValidInput_Success()
        {
            _context.Functions.AddRange(new List<Function>()
            {
                new Function()
                {
                    Id = "DeleteFunction_ValidInput_Success",
                    ParentId = null,
                    Name = "DeleteFunction_ValidInput_Success",
                    SortOrder = 1,
                    Url = "/DeleteFunction_ValidInput_Success",
                }
            });
            await _context.SaveChangesAsync();

            var functionsController = new FunctionsController(_context);
            var result = await functionsController.DeleteFunction("DeleteFunction_ValidInput_Success");
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async void DeleteFunction_ValidInput_Failed()
        {
            var functionsController = new FunctionsController(_context);
            var result = await functionsController.DeleteFunction("DeleteFunction_ValidInput_Failed");
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}

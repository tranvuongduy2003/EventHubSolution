using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    public class CategoriesController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileService;

        public CategoriesController(ApplicationDbContext db, IFileStorageService fileService)
        {
            _db = db;
            _fileService = fileService;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostCategory([FromForm] CategoryCreateRequest request)
        {
            var category = new Category()
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Color = request.Color,
            };

            //TODO: Upload file and assign category.ImageColorId to new FileStorage's Id
            FileStorage iconFileStorage = await _fileService.SaveFileToFileStorage(request.IconImage);
            category.IconImageId = iconFileStorage.Id;

            _db.Categories.Add(category);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { id = category.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.VIEW)]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories([FromQuery] PaginationFilter filter)
        {
            var categories = _db.Categories.ToList();
            if (filter.search != null)
            {
                categories = categories.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            categories = filter.order switch
            {
                PageOrder.ASC => categories.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => categories.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => categories
            };

            var metadata = new Metadata(categories.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                categories = categories.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var categoryVms = (from _category in categories
                               join _fileStorage in _db.FileStorages
                               on _category.IconImageId equals _fileStorage.Id
                               into joinedCategories
                               from _joinedCategory in joinedCategories
                               select new CategoryVm
                               {
                                   Id = _category.Id,
                                   Name = _category.Name,
                                   Color = _category.Color,
                                   IconImage = _joinedCategory?.FilePath,
                                   CreatedAt = _category.CreatedAt,
                                   UpdatedAt = _category.UpdatedAt
                               }).ToList();

            var pagination = new Pagination<CategoryVm>
            {
                Items = categoryVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _db.Categories.FindAsync(id);

            if (category == null)
                return NotFound(new ApiNotFoundResponse(""));

            var categoryIconImage = await _db.FileStorages.FindAsync(category.IconImageId);

            var categoryVm = new CategoryVm()
            {
                Id = category.Id,
                Name = category.Name,
                Color = category.Color,
                IconImage = categoryIconImage.FilePath,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };

            return Ok(categoryVm);
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutCategory(int id, [FromForm] CategoryCreateRequest request)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new ApiNotFoundResponse(""));

            category.Name = category.Name;
            category.Color = category.Color;

            //TODO: Upload file and assign category.ImageColorId to new FileStorage's Id
            FileStorage iconFileStorage = await _fileService.SaveFileToFileStorage(request.IconImage);
            category.IconImageId = iconFileStorage.Id;

            _db.Categories.Update(category);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new ApiNotFoundResponse(""));

            var categoryIconImage = await _db.FileStorages.FindAsync(category.IconImageId);

            _db.Categories.Remove(category);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                var categoryvm = new CategoryVm()
                {
                    Id = category.Id,
                    Name = category.Name,
                    Color = category.Color,
                    IconImage = categoryIconImage.FilePath,
                    CreatedAt = category.CreatedAt,
                    UpdatedAt = category.UpdatedAt
                };
                return Ok(categoryvm);
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}

using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.BackendServer.Services.Interfaces;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileService;
        private readonly ICacheService _cacheService;

        public CategoriesController(ApplicationDbContext db, IFileStorageService fileService, ICacheService cacheService)
        {
            _db = db;
            _fileService = fileService;
            _cacheService = cacheService;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.CREATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostCategory([FromForm] CategoryCreateRequest request)
        {
            var category = new Category()
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Color = request.Color,
            };

            //TODO: Upload file and assign category.ImageColorId to new FileStorage's Id
            FileStorageVm iconFileStorage = await _fileService.SaveFileToFileStorageAsync(request.IconImage, FileContainer.CATEGORIES);
            category.IconImageId = iconFileStorage.Id;

            var addedCategory = _db.Categories.Add(category);

            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<Category>($"{CacheKey.CATEGORY}{addedCategory.Entity.Id}", addedCategory.Entity, expiryTime);

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
        public async Task<IActionResult> GetCategories([FromQuery] PaginationFilter filter)
        {
            // Check cache data
            var categories = new List<Category>();
            var cacheCategories = _cacheService.GetData<IEnumerable<Category>>(CacheKey.CATEGORIES);
            if (cacheCategories != null && cacheCategories.Count() > 0)
                categories = cacheCategories.ToList();
            else
                categories = await _db.Categories.ToListAsync();
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<IEnumerable<Category>>(CacheKey.CATEGORIES, categories, expiryTime);

            var fileStorages = await _fileService.GetListFileStoragesAsync();

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
                               join _fileStorage in fileStorages
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

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            // Check cache data
            Category category = null;
            var cacheCategory = _cacheService.GetData<Category>($"{CacheKey.CATEGORY}{id}");
            if (cacheCategory != null)
                category = cacheCategory;
            else
                category = await _db.Categories.FindAsync(id);
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<Category>($"{CacheKey.CATEGORY}{id}", category, expiryTime);

            if (category == null)
                return NotFound(new ApiNotFoundResponse(""));

            var categoryIconImage = await _fileService.GetFileByFileIdAsync(category.IconImageId);

            var categoryVm = new CategoryVm()
            {
                Id = category.Id,
                Name = category.Name,
                Color = category.Color,
                IconImage = categoryIconImage.FilePath,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };

            return Ok(new ApiOkResponse(categoryVm));
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_CATEGORY, CommandCode.UPDATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutCategory(string id, [FromForm] CategoryCreateRequest request)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new ApiNotFoundResponse(""));

            category.Name = category.Name;
            category.Color = category.Color;

            //TODO: Upload file and assign category.ImageColorId to new FileStorage's Id
            FileStorageVm iconFileStorage = await _fileService.SaveFileToFileStorageAsync(request.IconImage, FileContainer.CATEGORIES);
            category.IconImageId = iconFileStorage.Id;

            var updatedCategory = _db.Categories.Update(category);
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<Category>($"{CacheKey.CATEGORY}{updatedCategory.Entity.Id}", updatedCategory.Entity, expiryTime);
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

            var categoryIconImage = await _fileService.GetFileByFileIdAsync(category.IconImageId);

            _db.Categories.Remove(category);
            _cacheService.RemoveData($"{CacheKey.CATEGORY}{category.Id}");
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
                return Ok(new ApiOkResponse(categoryvm));
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}

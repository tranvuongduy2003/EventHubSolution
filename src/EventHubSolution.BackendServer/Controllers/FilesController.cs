using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IFileStorageService _fileStorage;

        public FilesController(IFileStorageService fileStorage)
        {
            _fileStorage = fileStorage;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostFile([FromQuery][Required] string container, [FromForm] FileUploadRequest request)
        {
            var azureFile = await _fileStorage.SaveFileToFileStorageAsync(request.File, container);

            return Ok(new ApiOkResponse(azureFile));
        }

        [HttpGet]
        public async Task<IActionResult> GetFiles([FromQuery] PaginationFilter filter)
        {
            var fileStorages = await _fileStorage.GetListFileStoragesAsync();

            if (filter.search != null)
            {
                fileStorages = fileStorages.Where(c => c.FileName.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            fileStorages = filter.order switch
            {
                PageOrder.ASC => fileStorages.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => fileStorages.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => fileStorages
            };

            var metadata = new Metadata(fileStorages.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                fileStorages = fileStorages.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<FileStorageVm>
            {
                Items = fileStorages.ToList(),
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFileById(string id)
        {
            var file = await _fileStorage.GetFileByFileIdAsync(id);

            if (file == null)
                return NotFound();

            return Ok(new ApiOkResponse(file));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFile(string id)
        {
            var file = await _fileStorage.GetFileByFileIdAsync(id);

            if (file == null)
                return NotFound();

            var deletedFile = await _fileStorage.DeleteFileByIdAsync(id);

            if (deletedFile == null)
                return BadRequest();

            return Ok(new ApiOkResponse(deletedFile));
        }
    }
}

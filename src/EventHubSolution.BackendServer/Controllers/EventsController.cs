using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Contents;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventHubSolution.BackendServer.Controllers
{
    public class EventsController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileService;
        private readonly UserManager<User> _userManager;

        public EventsController(ApplicationDbContext db, IFileStorageService fileService, UserManager<User> userManager)
        {
            _db = db;
            _fileService = fileService;
            _userManager = userManager;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostEvent([FromForm] EventCreateRequest request)
        {
            var eventData = new Event()
            {
                Id = Guid.NewGuid().ToString(),
                CreatorId = request.CreatorId,
                Name = request.Name,
                Description = request.Description,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Promotion = request.Promotion,
            };

            //TODO: Upload cover image file
            FileStorage coverImageFileStorage = await _fileService.SaveFileToFileStorage(request.CoverImage);
            eventData.CoverImageId = coverImageFileStorage.Id;

            //TODO: Create email content
            var emailContent = new EmailContent()
            {
                Id = Guid.NewGuid().ToString(),
                EventId = eventData.Id,
                Content = request.EmailContent.Content
            };
            //TODO: Upload email attachments file
            request.EmailContent.Attachments.ForEach(async attachment =>
            {
                FileStorage attachmentFileStorage = await _fileService.SaveFileToFileStorage(attachment);
                var emailAttachment = new EmailAttachment()
                {
                    AttachmentId = attachmentFileStorage.Id,
                    EmailContentId = emailContent.Id,
                };
                _db.EmailAttachments.Add(emailAttachment);
            });
            _db.EmailContents.Add(emailContent);
            eventData.EmailContentId = emailContent.Id;

            //TODO: Create email location
            var location = new Location()
            {
                Id = Guid.NewGuid().ToString(),
                Street = request.Location.Street,
                District = request.Location.District,
                City = request.Location.City,
                LatitudeY = request.Location.LatitudeY,
                LongitudeX = request.Location.LongitudeX,
            };
            _db.Locations.Add(location);
            eventData.LocationId = location.Id;

            //TODO: Create ticket types
            request.TicketTypes.ForEach(type =>
            {
                var ticketType = new TicketType()
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = eventData.Id,
                    Name = type.Name,
                    Price = type.Price,
                    Quantity = type.Quantity
                };
                _db.TicketTypes.Add(ticketType);
            });

            //TODO: Create event categories
            request.CategoryIds.ForEach(categoryId =>
            {
                var eventCategory = new EventCategory()
                {
                    CategoryId = categoryId,
                    EventId = eventData.Id,
                };
                _db.EventCategories.Add(eventCategory);
            });

            _db.Events.Add(eventData);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { id = eventData.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        //[HttpGet]
        //[ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        //public async Task<IActionResult> GetEvents([FromQuery] PaginationFilter filter)
        //{
        //    var eventDatas = _db.Events.ToList();
        //    if (filter.search != null)
        //    {
        //        eventDatas = eventDatas.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
        //    }

        //    eventDatas = filter.order switch
        //    {
        //        PageOrder.ASC => eventDatas.OrderBy(c => c.SortOrder).ToList(),
        //        PageOrder.DESC => eventDatas.OrderByDescending(c => c.SortOrder).ToList(),
        //        _ => eventDatas
        //    };

        //    var metadata = new Metadata(eventDatas.Count(), filter.page, filter.size, filter.takeAll);

        //    if (filter.takeAll == false)
        //    {
        //        eventDatas = eventDatas.Skip((filter.page - 1) * filter.size)
        //            .Take(filter.size).ToList();
        //    }

        //    var eventDataVms = eventDatas.Select(f => new EventVm()
        //    {
        //        Id = f.Id,
        //        Name = f.Name,
        //        Url = f.Url,
        //        SortOrder = f.SortOrder,
        //        ParentId = f.ParentId,
        //    }).ToList();

        //    var pagination = new Pagination<EventVm>
        //    {
        //        Items = eventDataVms,
        //        Metadata = metadata,
        //    };

        //    Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

        //    return Ok(pagination);
        //}

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse(""));

            var eventDataVm = new EventDetailVm()
            {
                Id = eventData.Id,
                CreatorId = eventData.CreatorId,
                Name = eventData.Name,
                Description = eventData.Description,
                LocationId = eventData.LocationId,
                StartTime = eventData.StartTime,
                EndTime = eventData.EndTime,
                Promotion = eventData.Promotion,
                NumberOfFavourites = eventData.NumberOfFavourites,
                NumberOfShares = eventData.NumberOfShares,
                NumberOfSoldTickets = eventData.NumberOfSoldTickets,
                Status = eventData.Status,
                CreatedAt = eventData.CreatedAt,
                UpdatedAt = eventData.UpdatedAt,
            };

            //TODO: Get event's ticket types
            var ticketTypes = _db.TicketTypes.Where(t => t.EventId == eventData.Id).Select(t => new TicketTypeVm
            {
                Id = t.Id,
                EventId = eventData.Id,
                Name = t.Name,
                NumberOfSoldTickets = t.NumberOfSoldTickets,
                Quantity = t.Quantity,
                Price = t.Price
            }).ToList();
            eventDataVm.TicketTypes = ticketTypes;

            //TODO: Get event's categories
            var catogories = _db.Categories
                .Join(_db.FileStorages, _category => _category.IconImageId, _fileStorage => _fileStorage.Id, (_category, _fileStorage) => new CategoryVm
                {
                    Id = _category.Id,
                    Color = _category.Color,
                    IconImage = _fileStorage.FilePath,
                    Name = _category.Name,
                    CreatedAt = _category.CreatedAt,
                    UpdatedAt = _category.UpdatedAt,
                })
                .Join(_db.EventCategories, _categoryVm => _categoryVm.Id, _eventCategory => _eventCategory.CategoryId, (_categoryVm, _eventCategory) => new
                {
                    _categoryVm,
                    _eventCategory
                })
                .Where(joindCategory => joindCategory._eventCategory.EventId == eventData.Id)
                .Select(joindCategory => joindCategory._categoryVm)
                .ToList();
            eventDataVm.Categories = catogories;

            //TODO: Get event's email content
            var emailContentVm = _db.EmailAttachments
                .Join(_db.EmailContents, _attachment => _attachment.EmailContentId, _emailContent => _emailContent.Id, (_attachment, _emailContent) => new
                {
                    Id = _emailContent.Id,
                    EventId = _emailContent.Id,
                    Content = _emailContent.Content,
                    AttachmentId = _attachment.AttachmentId
                })
                .Join(_db.FileStorages, _emailContent => _emailContent.AttachmentId, _fileStorage => _fileStorage.Id, (_emailContent, _fileStorage) => new
                {
                    EmailContent = _emailContent,
                    FileStorage = new FileStorageVm
                    {
                        Id = _fileStorage.Id,
                        FileSize = _fileStorage.FileSize,
                        FileName = _fileStorage.FileName,
                        FilePath = _fileStorage.FilePath,
                        FileType = _fileStorage.FileType,
                        CreatedAt = _fileStorage.CreatedAt,
                        UpdatedAt = _fileStorage.UpdatedAt,
                    }
                })
                .GroupBy(joinedEmailContent => joinedEmailContent.EmailContent)
                .Select(groupedEmailContent => new EmailContentVm
                {
                    Id = groupedEmailContent.Key.Id,
                    Content = groupedEmailContent.Key.Content,
                    EventId = groupedEmailContent.Key.EventId,
                    Attachments = groupedEmailContent.Select(ge => ge.FileStorage).ToList()
                })
                .FirstOrDefault(a => a.EventId == eventData.Id);
            eventDataVm.EmailContent = emailContentVm;

            //TODO: Get event's location
            var location = _db.Locations.Find(eventData.LocationId);
            var locationVm = new LocationVm()
            {
                Id = location.Id,
                City = location.City,
                District = location.District,
                Street = location.Street,
                LatitudeY = location.LatitudeY,
                LongitudeX = location.LongitudeX,
            };
            eventDataVm.Location = locationVm;

            //TODO: Get event's cover image
            var coverImage = _db.FileStorages.Find(eventData.CoverImageId);
            eventDataVm.CoverImage = coverImage?.FilePath;

            //TODO: Get event's creator
            var creator = await _userManager.FindByIdAsync(eventData.CreatorId);
            var avatar = _db.FileStorages.Find(creator.AvatarId);
            var creatorVm = new CreatorVm()
            {
                Id = creator.Id,
                Dob = creator.Dob,
                Email = creator.Email,
                FullName = creator.FullName,
                Gender = creator.Gender,
                PhoneNumber = creator.PhoneNumber,
                UserName = creator.UserName,
                Avatar = avatar.FilePath
            };
            eventDataVm.Creator = creatorVm;

            return Ok(eventDataVm);
        }

        //[HttpPut("{id}")]
        //[ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.UPDATE)]
        //[ApiValidationFilter]
        //public async Task<IActionResult> PutEvent(string id, [FromBody] EventCreateRequest request)
        //{
        //    var eventData = await _db.Events.FindAsync(id);
        //    if (eventData == null)
        //        return NotFound(new ApiNotFoundResponse(""));

        //    eventData.Name = eventData.Name;
        //    eventData.Url = eventData.Url;
        //    eventData.SortOrder = eventData.SortOrder;
        //    eventData.ParentId = eventData.ParentId;

        //    _db.Events.Update(eventData);
        //    var result = await _db.SaveChangesAsync();

        //    if (result > 0)
        //    {
        //        return NoContent();
        //    }
        //    return BadRequest(new ApiBadRequestResponse(""));
        //}

        //[HttpDelete("{id}")]
        //[ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.DELETE)]
        //public async Task<IActionResult> DeleteEvent(string id)
        //{
        //    var eventData = await _db.Events.FindAsync(id);
        //    if (eventData == null)
        //        return NotFound(new ApiNotFoundResponse(""));

        //    _db.Events.Remove(eventData);
        //    var result = await _db.SaveChangesAsync();

        //    if (result > 0)
        //    {
        //        var eventDatavm = new EventVm()
        //        {
        //            Id = eventData.Id,
        //            Name = eventData.Name,
        //            Url = eventData.Url,
        //            SortOrder = eventData.SortOrder,
        //            ParentId = eventData.ParentId,
        //        };
        //        return Ok(eventDatavm);
        //    }
        //    return BadRequest(new ApiBadRequestResponse(""));
        //}
    }
}

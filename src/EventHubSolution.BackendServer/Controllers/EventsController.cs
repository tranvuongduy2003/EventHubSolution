using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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

            //TODO: Create event location
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

        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetEvents([FromQuery] PaginationFilter filter)
        {
            var eventDatas = _db.Events.ToList();
            if (filter.search != null)
            {
                eventDatas = eventDatas.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            eventDatas = filter.order switch
            {
                PageOrder.ASC => eventDatas.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => eventDatas.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => eventDatas
            };

            var metadata = new Metadata(eventDatas.Count(), filter.page, filter.size, filter.takeAll);

            var eventDataVms = eventDatas
                    .Join(_db.FileStorages, _event => _event.CoverImageId, _fileStorage => _fileStorage.Id, (_event, _fileStorage) => new EventVm
                    {
                        Id = _event.Id,
                        Name = _event.Name,
                        Description = _event.Description,
                        CoverImageId = _event.CoverImageId,
                        CoverImage = _fileStorage.FilePath,
                        CreatorId = _event.CreatorId,
                        LocationId = _event.LocationId,
                        StartTime = _event.StartTime,
                        EndTime = _event.EndTime,
                        NumberOfFavourites = _event.NumberOfFavourites,
                        NumberOfShares = _event.NumberOfShares,
                        NumberOfSoldTickets = _event.NumberOfSoldTickets,
                        Promotion = _event.Promotion,
                        Status = _event.Status,
                        CreatedAt = _event.CreatedAt,
                        UpdatedAt = _event.UpdatedAt
                    })
                    .Join(_userManager.Users, _eventVm => _eventVm.CreatorId, _user => _user.Id, (_eventVm, _user) =>
                    {
                        _eventVm.CreatorName = _user.UserName;
                        return _eventVm;
                    })
                    .Join(_db.Locations, _eventVm => _eventVm.LocationId, _location => _location.Id, (_eventVm, _location) =>
                    {
                        _eventVm.LocationString = $"{_location.Street}, {_location.District}, {_location.City}";
                        return _eventVm;
                    })
                    .Join(
                        _db.EventCategories.Join(
                            _db.Categories.Join(_db.FileStorages, _category => _category.IconImageId, _fileStorage => _fileStorage.Id, (_category, _fileStorage) => new CategoryVm
                            {
                                Id = _category.Id,
                                Color = _category.Color,
                                IconImage = _fileStorage.FilePath,
                                Name = _category.Name,
                                CreatedAt = _category.CreatedAt,
                                UpdatedAt = _category.UpdatedAt,
                            }).DefaultIfEmpty(),
                            _eventCategory => _eventCategory.CategoryId,
                            _categoryVm => _categoryVm.Id,
                            (_eventCategory, _categoryVm) => new
                            {
                                EventId = _eventCategory.EventId,
                                CategoryId = _categoryVm.Id,
                                Color = _categoryVm.Color,
                                IconImage = _categoryVm.IconImage,
                                Name = _categoryVm.Name,
                                CreatedAt = _categoryVm.CreatedAt,
                                UpdatedAt = _categoryVm.UpdatedAt,
                            }
                        ),
                        _eventVm => _eventVm.Id,
                        _joinedEventCategory => _joinedEventCategory.EventId,
                        (_eventVm, _joinedEventCategory) => new
                        {
                            _eventVm,
                            _joinedEventCategory
                        }
                    )
                    .GroupBy(joinedEvent => joinedEvent._eventVm)
                    .Select(groupedEvent =>
                    {
                        var eventVm = groupedEvent.Key;
                        eventVm.Categories = groupedEvent.Select(e => new CategoryVm
                        {
                            Id = e._joinedEventCategory.CategoryId,
                            Color = e._joinedEventCategory.Color,
                            Name = e._joinedEventCategory.Name,
                            IconImage = e._joinedEventCategory.IconImage,
                            CreatedAt = e._joinedEventCategory.CreatedAt,
                            UpdatedAt = e._joinedEventCategory.UpdatedAt,
                        }).ToList();
                        return eventVm;
                    })
                    .ToList();

            if (filter.takeAll == false)
            {
                eventDataVms = eventDataVms.Skip((filter.page - 1) * filter.size).Take(filter.size).ToList();
            }

            var pagination = new Pagination<EventVm>
            {
                Items = eventDataVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

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

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutEvent(string id, [FromBody] EventCreateRequest request)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse(""));

            eventData.CreatorId = request.CreatorId;
            eventData.Name = request.Name;
            eventData.Description = request.Description;
            eventData.StartTime = request.StartTime;
            eventData.EndTime = request.EndTime;
            eventData.Promotion = request.Promotion;

            //TODO: Update cover image
            FileStorage coverImageFileStorage = await _fileService.SaveFileToFileStorage(request.CoverImage);
            eventData.CoverImageId = coverImageFileStorage.Id;

            //TODO: Update email content
            var emailContent = await _db.EmailContents.FindAsync(eventData.EmailContentId);
            emailContent.Content = request.EmailContent.Content;
            _db.EmailContents.Update(emailContent);
            //TODO: Update email attachments file
            request.EmailContent.Attachments.ForEach(async attachment =>
            {
                FileStorage attachmentFileStorage = await _fileService.SaveFileToFileStorage(attachment);
                var emailAttachment = await _db.EmailAttachments.FirstOrDefaultAsync(e => e.EmailContentId == emailContent.Id);
                emailAttachment.AttachmentId = attachmentFileStorage.Id;
                _db.EmailAttachments.Update(emailAttachment);
            });

            //TODO: Udpate event location
            var location = await _db.Locations.FindAsync(eventData.LocationId);
            location.Street = request.Location.Street;
            location.District = request.Location.District;
            location.City = request.Location.City;
            location.LatitudeY = request.Location.LatitudeY;
            location.LongitudeX = request.Location.LongitudeX;
            _db.Locations.Update(location);

            //TODO: Update ticket types
            request.TicketTypes.ForEach(async type =>
            {
                var ticketType = await _db.TicketTypes.FirstOrDefaultAsync(t => t.Name == type.Name);
                ticketType.Name = type.Name;
                ticketType.Price = type.Price;
                ticketType.Quantity = type.Quantity;
                _db.TicketTypes.Update(ticketType);
            });

            //TODO: Update event categories
            var eventCategories = _db.EventCategories.Where(e => e.EventId == eventData.Id);
            _db.EventCategories.RemoveRange(eventCategories);
            request.CategoryIds.ForEach(categoryId =>
            {
                var eventCategory = new EventCategory()
                {
                    CategoryId = categoryId,
                    EventId = eventData.Id,
                };
                _db.EventCategories.Add(eventCategory);
            });

            _db.Events.Update(eventData);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteEvent(string id)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse(""));

            //TODO: Delete email content
            var emailContent = await _db.EmailContents.FindAsync(eventData.EmailContentId);
            _db.EmailContents.Remove(emailContent);

            //TODO: Delete event location
            var location = await _db.Locations.FindAsync(eventData.LocationId);
            _db.Locations.Remove(location);

            //TODO: Delete ticket types
            var ticketTypes = _db.TicketTypes.Where(t => t.EventId == eventData.Id);
            _db.TicketTypes.RemoveRange(ticketTypes);

            //TODO: Delete event categories
            var eventCategories = _db.EventCategories.Where(t => t.EventId == eventData.Id);
            _db.EventCategories.RemoveRange(eventCategories);

            _db.Events.Remove(eventData);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return Ok();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}

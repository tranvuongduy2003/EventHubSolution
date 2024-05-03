using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.BackendServer.Services.Interfaces;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileService;
        private readonly UserManager<User> _userManager;
        private readonly ICacheService _cacheService;

        public EventsController(ApplicationDbContext db, IFileStorageService fileService, UserManager<User> userManager, ICacheService cacheService)
        {
            _db = db;
            _fileService = fileService;
            _userManager = userManager;
            _cacheService = cacheService;
        }

        #region Events
        [HttpPost]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
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
                Promotion = request.Promotion ?? 0.0,
                IsPrivate = request.IsPrivate,
                EventCycleType = request.EventCycleType,
                EventPaymentType = request.EventPaymentType,
            };

            var user = await _userManager.FindByIdAsync(request.CreatorId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.CreatorId} is not found"));

            //TODO: Upload cover image file
            FileStorageVm coverImageFileStorage = await _fileService.SaveFileToFileStorageAsync(request.CoverImage, FileContainer.EVENTS);
            eventData.CoverImageId = coverImageFileStorage.Id;

            //TODO: Create email content
            if (request.EmailContent != null)
            {
                var emailContent = new EmailContent()
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = request.EmailContent.Content,
                    EventId = eventData.Id
                };
                _db.EmailContents.Add(emailContent);

                //TODO: Upload email attachments file
                if (request.EmailContent.Attachments != null && request.EmailContent.Attachments.Count > 0)
                {
                    foreach (var attachment in request.EmailContent.Attachments)
                    {
                        FileStorageVm attachmentFileStorage = await _fileService.SaveFileToFileStorageAsync(attachment, FileContainer.EVENTS);
                        var emailAttachment = new EmailAttachment()
                        {
                            AttachmentId = attachmentFileStorage.Id,
                            EmailContentId = emailContent.Id,
                        };
                        _db.EmailAttachments.Add(emailAttachment);
                    }
                }
            }


            //TODO: Create event location
            var location = new Location()
            {
                Id = Guid.NewGuid().ToString(),
                EventId = eventData.Id,
                Street = request.Location.Street,
                District = request.Location.District,
                City = request.Location.City,
            };
            _db.Locations.Add(location);

            if (request.EventPaymentType == EventPaymentType.PAID)
            {
                //TODO: Create ticket types
                foreach (var type in request.TicketTypes)
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
                }
            }

            //TODO: Create event categories
            foreach (var categoryId in request.CategoryIds)
            {
                var eventCategory = new EventCategory()
                {
                    CategoryId = categoryId,
                    EventId = eventData.Id,
                };
                _db.EventCategories.Add(eventCategory);
            }

            _db.Events.Add(eventData);

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                user.NumberOfCreatedEvents += 1;
                await _userManager.UpdateAsync(user);
                await _db.SaveChangesAsync();

                return CreatedAtAction(nameof(PostEvent), new ApiCreatedResponse(new
                {
                    Id = eventData.Id
                }));
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents([FromQuery] EventPaginationFilter filter)
        {
            // Check cache data
            var eventVms = new List<EventVm>();
            var cacheEventVms = _cacheService.GetData<IEnumerable<EventVm>>(CacheKey.EVENTS);
            if (cacheEventVms != null && cacheEventVms.Count() > 0)
                eventVms = cacheEventVms.ToList();
            else
            {
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                var eventCategories = (from _eventCategory in _db.EventCategories.ToList()
                                       join _categoryVm in (from _category in _db.Categories.ToList()
                                                            join _fileStorage in fileStorages
                                                            on _category.IconImageId equals _fileStorage.Id
                                                            into joinedCategories
                                                            from _joinedCategory in joinedCategories.DefaultIfEmpty()
                                                            select new CategoryVm
                                                            {
                                                                Id = _category.Id,
                                                                Color = _category.Color,
                                                                IconImage = _joinedCategory != null ? _joinedCategory.FilePath : "",
                                                                Name = _category.Name
                                                            })
                                       on _eventCategory.CategoryId equals _categoryVm.Id
                                       into joinedEventCategories
                                       from _joinedEventCategory in joinedEventCategories.DefaultIfEmpty()
                                       select new
                                       {
                                           EventId = _eventCategory.EventId,
                                           CategoryId = _eventCategory.CategoryId,
                                           CategoryVm = _joinedEventCategory,
                                       }).ToList();

                var joinedEventVms = (from _event in _db.Events.ToList()
                                      join _fileStorage in fileStorages
                                      on _event.CoverImageId equals _fileStorage.Id
                                      into joinedCoverImageEvents
                                      from _joinedCoverImageEvent in joinedCoverImageEvents.DefaultIfEmpty()
                                      join _location in _db.Locations.ToList()
                                      on _event.Id equals _location.EventId
                                      into joinedLocationEvents
                                      from _joinedLocationEvent in joinedLocationEvents.DefaultIfEmpty()
                                      join _user in (from u in _userManager.Users.ToList()
                                                     join f in fileStorages
                                                         on u.AvatarId equals f.Id
                                                         into UsersWithAvatar
                                                     from uwa in UsersWithAvatar.DefaultIfEmpty()
                                                     select new
                                                     {
                                                         Id = u.Id,
                                                         FullName = u.FullName,
                                                         Avatar = uwa?.FilePath,
                                                     })
                                      on _event.CreatorId equals _user.Id
                                      into joinedCreatorEvents
                                      from _joinedCreatorEvent in joinedCreatorEvents.DefaultIfEmpty()
                                      select new EventVm
                                      {
                                          Id = _event.Id,
                                          Name = _event.Name,
                                          CreatorName = _joinedCreatorEvent?.FullName,
                                          CreatorAvatar = _joinedCreatorEvent?.Avatar,
                                          Description = _event.Description,
                                          CoverImage = _joinedCoverImageEvent?.FilePath,
                                          StartTime = _event.StartTime,
                                          EndTime = _event.EndTime,
                                          Promotion = _event.Promotion ?? 0.0,
                                          IsPrivate = _event.IsPrivate,
                                          IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
                                          EventCycleType = _event.EventCycleType,
                                          EventPaymentType = _event.EventPaymentType,
                                          Status = _event.Status,
                                          LocationString = _joinedLocationEvent != null ? $"{_joinedLocationEvent?.Street}, {_joinedLocationEvent?.District}, {_joinedLocationEvent?.City}" : "",
                                          CreatedAt = _event.CreatedAt,
                                          UpdatedAt = _event.UpdatedAt
                                      }).ToList();

                var joinedTicketTypeEventVms = (from _eventVm in joinedEventVms
                                                join _ticketType in _db.TicketTypes.ToList()
                                                on _eventVm.Id equals _ticketType.EventId
                                                into joinedTicketTypeEvents
                                                from _joinedTicketTypeEvent in joinedTicketTypeEvents.DefaultIfEmpty()
                                                select new
                                                {
                                                    _eventVm,
                                                    _joinedTicketTypeEvent
                                                })
                                    .GroupBy(joinedTicketTypeEvent => joinedTicketTypeEvent._eventVm)
                                    .AsEnumerable()
                                    .Select(groupedEventVm =>
                                    {
                                        EventVm eventVm = groupedEventVm.Key;
                                        eventVm.PriceRange = new PriceRangeVm
                                        {
                                            StartRange = groupedEventVm.Min(e => e._joinedTicketTypeEvent != null ? e._joinedTicketTypeEvent.Price : 1000000000),
                                            EndRange = groupedEventVm.Max(e => e._joinedTicketTypeEvent != null ? e._joinedTicketTypeEvent.Price : 0)
                                        };
                                        return eventVm;
                                    })
                                    .DistinctBy(e => e.Id)
                                    .ToList();

                eventVms = (from _eventVm in joinedTicketTypeEventVms
                            join _eventCategory in eventCategories
                            on _eventVm.Id equals _eventCategory.EventId
                            into joinedCategoryEvents
                            from _joinedCategoryEvent in joinedCategoryEvents.DefaultIfEmpty()
                            select new
                            {
                                _eventVm,
                                _joinedCategoryEvent
                            })
                                    .GroupBy(joinedEvent => joinedEvent._eventVm)
                                    .Select(groupedEvent =>
                                    {
                                        var eventVm = groupedEvent.Key;
                                        eventVm.Categories = groupedEvent
                                                                .Where(e => e._joinedCategoryEvent != null)
                                                                .Select(e => e._joinedCategoryEvent.CategoryVm)
                                                                .ToList();
                                        return eventVm;
                                    })
                                    .DistinctBy(e => e.Id)
                                    .ToList();


                // Set expiry time
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<IEnumerable<EventVm>>(CacheKey.EVENTS, eventVms, expiryTime);
            }

            var metadata = new EventMetadata(
                eventVms.Count(),
                filter.page,
                filter.size,
                filter.takeAll,
                eventVms.Count(e => !e.IsPrivate),
                eventVms.Count(e => e.IsPrivate),
                eventVms.Count(e => (bool)e.IsTrash)
            );

            if (filter.search != null)
            {
                eventVms = eventVms.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            eventVms = filter.order switch
            {
                PageOrder.ASC => eventVms.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => eventVms.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => eventVms
            };

            switch (filter.type)
            {
                case EventType.OPENING:
                    eventVms = eventVms.Where(e => e.StartTime <= DateTime.UtcNow && DateTime.UtcNow <= e.EndTime).ToList();
                    break;
                case EventType.UPCOMING:
                    eventVms = eventVms.Where(e => DateTime.UtcNow < e.StartTime).ToList();
                    break;
                case EventType.CLOSED:
                    eventVms = eventVms.Where(e => e.EndTime < DateTime.UtcNow).ToList();
                    break;
            }

            if (!filter.location.IsNullOrEmpty())
            {
                eventVms = eventVms.Where(e => e.LocationString.Split(", ").Any(str => filter.location.ToLower().Contains(str.ToLower()))).ToList();
            }

            if (!filter.categoryIds.IsNullOrEmpty())
            {
                eventVms = eventVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e => filter.priceRange.StartRange <= e.PriceRange.StartRange && filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
            }

            switch (filter.eventPrivacy)
            {
                case EventPrivacy.PUBLIC:
                    eventVms = eventVms.Where(c => !c.IsPrivate).ToList();
                    break;
                case EventPrivacy.PRIVATE:
                    eventVms = eventVms.Where(c => c.IsPrivate).ToList();
                    break;
                case EventPrivacy.TRASH:
                    eventVms = eventVms.Where(c => c.IsTrash).ToList();
                    break;
                default:
                    break;
            }

            if (filter.takeAll == false)
            {
                eventVms = eventVms.Skip((filter.page - 1) * filter.size).Take(filter.size).ToList();
            }

            var pagination = new Pagination<EventVm, EventMetadata>
            {
                Items = eventVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventById(string id)
        {
            // Check cache data
            EventDetailVm eventDataVm = null;
            var cacheEvent = _cacheService.GetData<EventDetailVm>($"{CacheKey.EVENT}{id}");
            if (cacheEvent != null)
                eventDataVm = cacheEvent;
            else
            {
                var eventData = await _db.Events.FindAsync(id);
                if (eventData == null)
                    return NotFound(new ApiNotFoundResponse($"Event with id {id} is not existed."));
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                eventDataVm = new EventDetailVm()
                {
                    Id = eventData.Id,
                    CreatorId = eventData.CreatorId,
                    Name = eventData.Name,
                    Description = eventData.Description,
                    StartTime = eventData.StartTime,
                    EndTime = eventData.EndTime,
                    Promotion = eventData.Promotion ?? 0.0,
                    EventCycleType = eventData.EventCycleType,
                    EventPaymentType = eventData.EventPaymentType,
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
                var catogories = _db.Categories.ToList()
                    .Join(fileStorages, _category => _category.IconImageId, _fileStorage => _fileStorage.Id, (_category, _fileStorage) => new CategoryVm
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
                var emailContentVm = _db.EmailAttachments.ToList()
                    .Join(_db.EmailContents, _attachment => _attachment.EmailContentId, _emailContent => _emailContent.Id, (_attachment, _emailContent) => new
                    {
                        Id = _emailContent.Id,
                        EventId = _emailContent.Id,
                        Content = _emailContent.Content,
                        AttachmentId = _attachment.AttachmentId
                    })
                    .Join(fileStorages, _emailContent => _emailContent.AttachmentId, _fileStorage => _fileStorage.Id, (_emailContent, _fileStorage) => new
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
                var location = _db.Locations.FirstOrDefault(l => l.EventId == eventData.Id);
                var locationVm = new LocationVm()
                {
                    Id = location.Id,
                    City = location.City,
                    District = location.District,
                    Street = location.Street,
                };
                eventDataVm.Location = locationVm;

                //TODO: Get event's cover image
                var coverImage = await _fileService.GetFileByFileIdAsync(eventData.CoverImageId);
                eventDataVm.CoverImage = coverImage?.FilePath;

                //TODO: Get event's creator
                var creator = await _userManager.FindByIdAsync(eventData.CreatorId);
                var avatar = await _fileService.GetFileByFileIdAsync(creator.AvatarId);
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
            }

            return Ok(new ApiOkResponse(eventDataVm));
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.UPDATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutEvent(string id, [FromForm] EventCreateRequest request)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {id} is not existed."));

            eventData.CreatorId = request.CreatorId;
            eventData.Name = request.Name;
            eventData.Description = request.Description;
            eventData.StartTime = request.StartTime;
            eventData.EndTime = request.EndTime;
            eventData.Promotion = request.Promotion;

            //TODO: Update cover image
            FileStorageVm coverImageFileStorage = await _fileService.SaveFileToFileStorageAsync(request.CoverImage, FileContainer.EVENTS);
            eventData.CoverImageId = coverImageFileStorage.Id;

            //TODO: Update email content
            var emailContent = _db.EmailContents.FirstOrDefault(ec => ec.EventId == eventData.Id);
            emailContent.Content = request.EmailContent.Content;
            _db.EmailContents.Update(emailContent);
            //TODO: Update email attachments file
            foreach (var attachment in request.EmailContent.Attachments)
            {
                FileStorageVm attachmentFileStorage = await _fileService.SaveFileToFileStorageAsync(attachment, FileContainer.EVENTS);
                var emailAttachment = await _db.EmailAttachments.FirstOrDefaultAsync(e => e.EmailContentId == emailContent.Id);
                emailAttachment.AttachmentId = attachmentFileStorage.Id;
                _db.EmailAttachments.Update(emailAttachment);
            }

            //TODO: Udpate event location
            var location = _db.Locations.FirstOrDefault(l => l.EventId == eventData.Id);
            location.Street = request.Location.Street;
            location.District = request.Location.District;
            location.City = request.Location.City;
            _db.Locations.Update(location);

            //TODO: Update ticket types
            foreach (var type in request.TicketTypes)
            {
                var ticketType = await _db.TicketTypes.FirstOrDefaultAsync(t => t.Name == type.Name);
                ticketType.Name = type.Name;
                ticketType.Price = type.Price;
                ticketType.Quantity = type.Quantity;
                _db.TicketTypes.Update(ticketType);
            }

            //TODO: Update event categories
            var eventCategories = _db.EventCategories.Where(e => e.EventId == eventData.Id);
            _db.EventCategories.RemoveRange(eventCategories);
            foreach (var categoryId in request.CategoryIds)
            {
                var eventCategory = new EventCategory()
                {
                    CategoryId = categoryId,
                    EventId = eventData.Id,
                };
                _db.EventCategories.Add(eventCategory);
            }

            _db.Events.Update(eventData);
            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.EVENT}{id}");

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteEvent(string id)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {id} is not existed."));

            //TODO: Delete email content
            var emailContent = _db.EmailContents.FirstOrDefault(ec => ec.EventId == eventData.Id);
            _db.EmailContents.Remove(emailContent);

            //TODO: Delete event location
            var location = _db.Locations.FirstOrDefault(l => l.EventId == eventData.Id);
            _db.Locations.Remove(location);

            //TODO: Delete ticket types
            var ticketTypes = _db.TicketTypes.Where(t => t.EventId == eventData.Id);
            _db.TicketTypes.RemoveRange(ticketTypes);

            //TODO: Delete event categories
            var eventCategories = _db.EventCategories.Where(t => t.EventId == eventData.Id);
            _db.EventCategories.RemoveRange(eventCategories);

            _db.Events.Remove(eventData);

            var user = await _userManager.FindByIdAsync(eventData.CreatorId);
            user.NumberOfCreatedEvents -= 1;

            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.EVENT}{id}");

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
        #endregion

        #region Reviews
        [HttpPost("{eventId}/reviewVms")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostReview(string eventId, [FromBody] ReviewCreateRequest request)
        {
            var dbEvent = await _db.Reviews.FindAsync(eventId);
            if (dbEvent == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            var review = new Review()
            {
                Id = Guid.NewGuid().ToString(),
                EventId = eventId,
                Content = request.Content,
                UserId = request.UserId,
                Rate = request.Rate,
            };
            var addedReview = _db.Reviews.Add(review);
            var result = await _db.SaveChangesAsync();

            var reviewVm = new ReviewVm
            {
                Id = addedReview.Entity.Id,
                EventId = addedReview.Entity.EventId,
                Content = addedReview.Entity.Content,
                UserId = addedReview.Entity.UserId,
                Rate = addedReview.Entity.Rate,
                CreatedAt = addedReview.Entity.CreatedAt,
                UpdatedAt = addedReview.Entity.UpdatedAt,
            };

            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<ReviewVm>($"{CacheKey.REVIEW}{reviewVm.Id}", reviewVm, expiryTime);

            if (result > 0)
            {
                return CreatedAtAction(nameof(PostReview), reviewVm, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet("{eventId}/reviewVms")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.VIEW)]
        public async Task<IActionResult> GetReviews(string eventId, [FromQuery] PaginationFilter filter)
        {
            var dbEvent = await _db.Reviews.FindAsync(eventId);
            if (dbEvent == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            // Check cache data
            var reviewVms = new List<ReviewVm>();
            var cacheReviewVms = _cacheService.GetData<IEnumerable<ReviewVm>>(CacheKey.REVIEWS);
            if (cacheReviewVms != null && cacheReviewVms.Count() > 0)
                reviewVms = cacheReviewVms.ToList();
            else
            {
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                var users = (from _user in _userManager.Users.ToList()
                             join _fileStorage in fileStorages
                             on _user.AvatarId equals _fileStorage.Id
                             into joinedUsers
                             from _joinedUser in joinedUsers
                             select new UserVm
                             {
                                 Id = _user.Id,
                                 Email = _user.Email,
                                 FullName = _user.FullName,
                                 Avatar = _joinedUser.FilePath,
                             });

                reviewVms = (from _review in _db.Reviews.ToList()
                             join _event in _db.Events.ToList() on _review.EventId equals _event.Id
                             join _user in users on _review.UserId equals _user.Id
                             select new ReviewVm
                             {
                                 Id = _review.Id,
                                 EventId = _review.EventId,
                                 EventName = _event.Name,
                                 UserId = _review.UserId,
                                 UserAvatar = _user.Avatar,
                                 UserName = _user.FullName,
                                 Content = _review.Content,
                                 Rate = _review.Rate,
                                 CreatedAt = _review.CreatedAt,
                                 UpdatedAt = _review.UpdatedAt,
                             }).ToList();
            }

            reviewVms = reviewVms.Where(r => r.EventId == eventId).ToList();

            var metadata = new Metadata(reviewVms.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.search != null)
            {
                reviewVms = reviewVms.Where(c => c.Content.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            reviewVms = filter.order switch
            {
                PageOrder.ASC => reviewVms.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => reviewVms.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => reviewVms
            };

            if (filter.takeAll == false)
            {
                reviewVms = reviewVms.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<ReviewVm, Metadata>
            {
                Items = reviewVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{eventId}/reviewVms/{reviewId}")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.VIEW)]
        public async Task<IActionResult> GetReviewById(string eventId, string reviewId)
        {// Check cache data
            ReviewVm reviewVm = null;
            var cacheReviewVm = _cacheService.GetData<ReviewVm>($"{CacheKey.REVIEW}{reviewId}");
            if (cacheReviewVm != null)
                reviewVm = cacheReviewVm;
            else
            {
                var review = await _db.Reviews.FindAsync(reviewId);
                if (review == null)
                    return NotFound(new ApiNotFoundResponse($"Review with id {reviewId} is not existed."));
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                var users = _userManager.Users.Join(fileStorages, _user => _user.AvatarId, _fileStorage => _fileStorage.Id, (_user, _fileStorage) => new UserVm
                {
                    Id = _user.Id,
                    Email = _user.Email,
                    FullName = _user.FullName,
                    Avatar = _fileStorage.FilePath,
                });
                var reviewUser = users.FirstOrDefault(u => u.Id == review.UserId);

                var dbEvent = await _db.Events.FindAsync(review.EventId);

                reviewVm = new ReviewVm()
                {
                    Id = review.Id,
                    EventId = review.EventId,
                    EventName = dbEvent?.Name,
                    UserId = review.UserId,
                    UserAvatar = reviewUser?.Avatar,
                    UserName = reviewUser?.FullName,
                    Content = review.Content,
                    Rate = review.Rate,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt,
                };

                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<ReviewVm>($"{CacheKey.REVIEW}{reviewVm.Id}", reviewVm, expiryTime);
            }

            return Ok(new ApiOkResponse(reviewVm));
        }

        [HttpPut("{eventId}/reviewVms/{reviewId}")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutReview(string eventId, string reviewId, [FromBody] ReviewCreateRequest request)
        {
            var review = await _db.Reviews.FindAsync(reviewId);
            if (review == null)
                return NotFound(new ApiNotFoundResponse($"Review with id {reviewId} is not existed."));

            review.EventId = request.EventId;
            review.UserId = request.UserId;
            review.Content = request.Content;
            review.Rate = request.Rate;

            var updatedReview = _db.Reviews.Update(review);
            var result = await _db.SaveChangesAsync();

            var reviewVm = new ReviewVm
            {
                Id = updatedReview.Entity.Id,
                EventId = updatedReview.Entity.EventId,
                Content = updatedReview.Entity.Content,
                UserId = updatedReview.Entity.UserId,
                Rate = updatedReview.Entity.Rate,
                CreatedAt = updatedReview.Entity.CreatedAt,
                UpdatedAt = updatedReview.Entity.UpdatedAt,
            };

            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<ReviewVm>($"{CacheKey.REVIEW}{reviewVm.Id}", reviewVm, expiryTime);

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{eventId}/reviewVms/{reviewId}")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteReview(string eventId, string reviewId)
        {
            var review = await _db.Reviews.FindAsync(reviewId);
            if (review == null)
                return NotFound(new ApiNotFoundResponse($"Review with id {reviewId} is not existed."));

            _db.Reviews.Remove(review);
            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.REVIEW}{reviewId}");

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
        #endregion

        #region Favourite Events
        [HttpPost("{eventId}/favourites/subscribe")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostCreateFavouriteEvent(string eventId, [FromBody] FavouriteEventCreateRequest request)
        {
            var dbEvent = await _db.Events.FindAsync(request.EventId);
            if (dbEvent == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {request.EventId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not existed."));

            var dbFavouriteEvent = await _db.FavouriteEvents.FindAsync(request.EventId, request.UserId);
            if (dbFavouriteEvent != null)
                return BadRequest(new ApiBadRequestResponse($"User has subscribed this event before"));

            var favouriteEvent = new FavouriteEvent
            {
                EventId = request.EventId,
                UserId = request.UserId,
            };

            _db.FavouriteEvents.Add(favouriteEvent);

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                user.NumberOfFavourites += 1;
                await _userManager.UpdateAsync(user);
                dbEvent.NumberOfFavourites += 1;
                _db.Events.Update(dbEvent);
                await _db.SaveChangesAsync();

                return Ok(new ApiOkResponse());
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpPost("{eventId}/favourites/unsubscribe")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.DELETE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostRemoveFavouriteEvent(string eventId, [FromBody] FavouriteEventCreateRequest request)
        {
            var dbEvent = await _db.Events.FindAsync(request.EventId);
            if (dbEvent == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {request.EventId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not existed."));

            var dbFavouriteEvent = await _db.FavouriteEvents.FindAsync(request.EventId, request.UserId);
            if (dbFavouriteEvent == null)
                return NotFound(new ApiNotFoundResponse($"User has not subscribed this event before"));

            _db.FavouriteEvents.Remove(dbFavouriteEvent);

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                user.NumberOfFavourites -= 1;
                await _userManager.UpdateAsync(user);
                dbEvent.NumberOfFavourites -= 1;
                _db.Events.Update(dbEvent);
                await _db.SaveChangesAsync();

                return Ok(new ApiOkResponse());
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }
        #endregion
    }
}

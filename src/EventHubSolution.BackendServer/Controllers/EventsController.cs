﻿using EventHubSolution.BackendServer.Authorization;
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
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;

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
        private readonly ITokenService _tokenService;

        public EventsController(ApplicationDbContext db, IFileStorageService fileService, UserManager<User> userManager, ICacheService cacheService, ITokenService tokenService)
        {
            _db = db;
            _fileService = fileService;
            _userManager = userManager;
            _cacheService = cacheService;
            _tokenService = tokenService;
        }

        #region Events
        [HttpPost]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostEvent([FromForm] EventCreateRequest request)
        {
            var eventData = await _db.Events.FirstOrDefaultAsync(e => e.Name.ToLower().Equals(request.Name.ToLower()));
            if (eventData != null)
                return BadRequest(new ApiBadRequestResponse($"Event {request.Name} already existed!"));

            eventData = new Data.Entities.Event()
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
                Location = request.Location
            };

            var user = await _userManager.FindByIdAsync(request.CreatorId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.CreatorId} is not found"));

            //TODO: Upload cover image file
            FileStorageVm coverImageFileStorage = await _fileService.SaveFileToFileStorageAsync(request.CoverImage, $"{FileContainer.EVENTS}/{eventData.Id}");
            eventData.CoverImageId = coverImageFileStorage.Id;

            //TODO: Upload event sub image files
            if (request.EventSubImages != null && request.EventSubImages.Any())
            {
                foreach (var file in request.EventSubImages)
                {
                    FileStorageVm subImageVm = await _fileService.SaveFileToFileStorageAsync(file, $"{FileContainer.EVENTS}/{eventData.Id}");
                    var eventSubImage = new EventSubImage()
                    {
                        Id = Guid.NewGuid().ToString(),
                        EventId = eventData.Id,
                        ImageId = subImageVm.Id
                    };
                    await _db.EventSubImages.AddAsync(eventSubImage);
                }
            }

            //TODO: Create email content
            if (request.EmailContent != null)
            {
                var emailContent = new EmailContent()
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = request.EmailContent.Content,
                    EventId = eventData.Id
                };
                await _db.EmailContents.AddAsync(emailContent);

                //TODO: Upload email attachments file
                if (request.EmailContent.Attachments != null && request.EmailContent.Attachments.Count > 0)
                {
                    foreach (var attachment in request.EmailContent.Attachments)
                    {
                        FileStorageVm attachmentFileStorage = await _fileService.SaveFileToFileStorageAsync(attachment, $"{FileContainer.EVENTS}/{eventData.Id}");
                        var emailAttachment = new EmailAttachment()
                        {
                            AttachmentId = attachmentFileStorage.Id,
                            EmailContentId = emailContent.Id,
                        };
                        await _db.EmailAttachments.AddAsync(emailAttachment);
                    }
                }
            }

            //TODO: Create ticket types
            if (request.EventPaymentType == EventPaymentType.PAID && request.TicketTypes != null && request.TicketTypes.Any())
            {
                foreach (var type in request.TicketTypes)
                {
                    var deserializedType = JsonConvert.DeserializeObject<TicketTypeCreateRequest>(type);
                    var ticketType = new TicketType()
                    {
                        Id = Guid.NewGuid().ToString(),
                        EventId = eventData.Id,
                        Name = deserializedType.Name,
                        Price = deserializedType.Price,
                        Quantity = deserializedType.Quantity
                    };
                    await _db.TicketTypes.AddAsync(ticketType);
                }
            }

            //TODO: Create event categories
            if (request.CategoryIds != null && request.CategoryIds.Any())
            {
                foreach (var categoryId in request.CategoryIds)
                {
                    var eventCategory = new EventCategory()
                    {
                        CategoryId = categoryId,
                        EventId = eventData.Id,
                    };
                    await _db.EventCategories.AddAsync(eventCategory);
                }
            }

            //TODO: Create event reasons
            if (request.Reasons != null && request.Reasons.Any())
            {
                foreach (var reason in request.Reasons)
                {
                    var reasonEntity = new Reason()
                    {
                        Id = Guid.NewGuid().ToString(),
                        EventId = eventData.Id,
                        Name = reason,
                    };
                    await _db.Reasons.AddAsync(reasonEntity);
                }
            }

            await _db.Events.AddAsync(eventData);

            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData(CacheKey.EVENTS);

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
                                       from _joinedEventCategory in joinedEventCategories
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
                                          CoverImage = _joinedCoverImageEvent != null && _joinedCoverImageEvent.FilePath != null ? _joinedCoverImageEvent.FilePath : "",
                                          StartTime = _event.StartTime,
                                          EndTime = _event.EndTime,
                                          Promotion = _event.Promotion ?? 0.0,
                                          IsPrivate = _event.IsPrivate,
                                          IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
                                          EventCycleType = _event.EventCycleType,
                                          EventPaymentType = _event.EventPaymentType,
                                          Status = _event.StartTime > DateTime.UtcNow ? EventStatus.UPCOMING : _event.EndTime < DateTime.UtcNow ? EventStatus.CLOSED : EventStatus.OPENING,
                                          Location = _event.Location,
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
                                                        .Where(e => e._joinedCategoryEvent?.CategoryVm != null)
                                                        .Select(e => e._joinedCategoryEvent.CategoryVm)
                                                        .ToList();
                                return eventVm;
                            })
                            .DistinctBy(e => e.Id)
                            .ToList();

                eventVms = (from _eventVm in eventVms
                            join _review in _db.Reviews.ToList()
                            on _eventVm.Id equals _review.EventId
                            into joinedReviewEvents
                            from _joinedReviewEvent in joinedReviewEvents.DefaultIfEmpty()
                            select new
                            {
                                _eventVm,
                                _joinedReviewEvent
                            })
                            .GroupBy(joinedEvent => joinedEvent._eventVm)
                            .Select(groupedEvent =>
                            {
                                var eventVm = groupedEvent.Key;
                                var reviews = groupedEvent.Where(e => e._joinedReviewEvent != null).Select(r => r._joinedReviewEvent).ToList();
                                eventVm.AverageRating = reviews.Count <= 0 ? 0 : reviews.Sum(r => r.Rate) / reviews.Count;
                                return eventVm;
                            })
                            .DistinctBy(e => e.Id)
                            .ToList();


                // Set expiry time
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<IEnumerable<EventVm>>(CacheKey.EVENTS, eventVms, expiryTime);
            }

            if (!filter.search.IsNullOrEmpty())
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
                eventVms = eventVms.Where(e => e.Location.ToLower().Contains(filter.location.ToLower())).ToList();
            }

            if (filter.categoryIds != null && filter.categoryIds.Count > 0 && !filter.categoryIds.Contains(null))
            {
                eventVms = eventVms.Where(e => e.Categories.Where(c => c != null && filter.categoryIds.Contains(c.Id)).Any()).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e => filter.priceRange.StartRange <= e.PriceRange.StartRange && filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
            }

            if (filter.rates != null)
            {
                eventVms = eventVms.Where(e => filter.rates.Where(r => (r - 0.5) < e.AverageRating && e.AverageRating <= (r + 0.5)).Any()).ToList();
            }

            switch (filter.eventPrivacy)
            {
                case EventPrivacy.PUBLIC:
                    eventVms = eventVms.Where(e => !e.IsPrivate).ToList();
                    break;
                case EventPrivacy.PRIVATE:
                    eventVms = eventVms.Where(e => e.IsPrivate).ToList();
                    break;
            }

            var metadata = new EventMetadata(
                eventVms.Count(),
                filter.page,
                filter.size,
                filter.takeAll,
                eventVms.Count(e => !e.IsPrivate),
                eventVms.Count(e => e.IsPrivate)
            );

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
                if (eventData == null || eventData.IsTrash == true)
                    return NotFound(new ApiNotFoundResponse($"Event with id {id} is not existed."));
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                var reviews = await _db.Reviews.Where(r => r.EventId == eventData.Id).ToListAsync();
                var averageRating = reviews.Count <= 0 ? 0 : reviews.Sum(r => r.Rate) / reviews.Count;

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
                    IsPrivate = eventData.IsPrivate,
                    IsTrash = eventData.IsTrash,
                    AverageRating = averageRating,
                    Location = eventData.Location,
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

                //TODO: Get event's category ids
                var catogoryIds = _db.EventCategories
                    .Where(_eventCategory => _eventCategory.EventId == eventData.Id)
                    .Select(_eventCategory => _eventCategory.CategoryId)
                    .ToList();
                eventDataVm.CategoryIds = catogoryIds;

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

                //TODO: Get event's sub images
                var subImages = _db.EventSubImages.ToList()
                    .Join(fileStorages, _subImage => _subImage.ImageId, _fileStorage => _fileStorage.Id, (_subImage, _fileStorage) => new
                    {
                        Id = _subImage.Id,
                        ImagePath = _fileStorage.FilePath,
                        EventId = _subImage.EventId
                    })
                    .Where(image => image.EventId.Equals(id))
                    .Select(image => image.ImagePath)
                    .ToList();
                eventDataVm.SubImages = subImages;

                //TODO: Get event's reasons
                var reasons = _db.Reasons.Where(reason => reason.EventId.Equals(id)).Select(reason => reason.Name).ToList();
                eventDataVm.Reasons = reasons;

                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData($"{CacheKey.EVENT}{id}", eventDataVm, expiryTime);
            }

            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }

            if (eventDataVm.IsTrash == true && (user == null || user.Id != eventDataVm.CreatorId))
                return Forbid("Access denied!");
            if (eventDataVm.IsPrivate == true && (user == null || user.Id != eventDataVm.CreatorId))
                return Forbid("Access denied!");

            if (user != null)
            {
                var favouriteEvent = await _db.FavouriteEvents.FirstOrDefaultAsync(f => f.EventId.Equals(eventDataVm.Id) && f.UserId.Equals(user.Id));
                eventDataVm.IsFavourite = favouriteEvent != null;
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
            if (eventData == null || eventData.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {id} is not existed."));

            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (user == null || user.Id != eventData.CreatorId)
                return Forbid("Access denied!");

            var sameNameEvent = await _db.Events.FirstOrDefaultAsync(e => e.Name.ToLower().Equals(request.Name.ToLower()) && e.Id != eventData.Id);
            if (sameNameEvent != null)
                return BadRequest(new ApiBadRequestResponse($"Event {request.Name} already existed!"));

            var creator = await _userManager.FindByIdAsync(request.CreatorId);
            if (creator == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.CreatorId} is not found"));

            eventData.CreatorId = request.CreatorId;
            eventData.Name = request.Name;
            eventData.Description = request.Description;
            eventData.StartTime = request.StartTime;
            eventData.EndTime = request.EndTime;
            eventData.Promotion = request.Promotion ?? 0.0;
            eventData.Location = request.Location;
            eventData.IsPrivate = request.IsPrivate;
            eventData.EventCycleType = request.EventCycleType;
            eventData.EventPaymentType = request.EventPaymentType;

            //TODO: Update cover image
            await _fileService.DeleteFileByIdAsync(eventData.CoverImageId);
            FileStorageVm coverImageFileStorage = await _fileService.SaveFileToFileStorageAsync(request.CoverImage, $"{FileContainer.EVENTS}/{eventData.Id}");
            eventData.CoverImageId = coverImageFileStorage.Id;

            //TODO: Update event sub images
            var eventSubImages = _db.EventSubImages.Where(t => t.EventId == eventData.Id).ToList();
            foreach (var image in eventSubImages)
                await _fileService.DeleteFileByIdAsync(image.Id);
            _db.EventSubImages.RemoveRange(eventSubImages);
            if (request.EventSubImages != null && request.EventSubImages.Any())
            {
                foreach (var file in request.EventSubImages)
                {
                    FileStorageVm subImageVm = await _fileService.SaveFileToFileStorageAsync(file, $"{FileContainer.EVENTS}/{eventData.Id}");
                    var eventSubImage = new EventSubImage()
                    {
                        Id = Guid.NewGuid().ToString(),
                        EventId = eventData.Id,
                        ImageId = subImageVm.Id
                    };
                    await _db.EventSubImages.AddAsync(eventSubImage);
                }
            }

            //TODO: Update email content
            var emailContent = await _db.EmailContents.FirstOrDefaultAsync(ec => ec.EventId == eventData.Id);
            if (request.EmailContent != null && emailContent != null)
            {
                emailContent.Content = request.EmailContent.Content;
                _db.EmailContents.Update(emailContent);

                var emailAttachments = _db.EmailAttachments.Where(e => e.EmailContentId == emailContent.Id).ToList();
                foreach (var attachment in emailAttachments)
                    await _fileService.DeleteFileByIdAsync(attachment.AttachmentId);
                _db.EmailAttachments.RemoveRange(emailAttachments);
                if (request.EmailContent.Attachments != null && request.EmailContent.Attachments.Count > 0)
                {
                    //TODO: Update email attachments file
                    foreach (var attachment in request.EmailContent.Attachments)
                    {
                        FileStorageVm attachmentFileStorage = await _fileService.SaveFileToFileStorageAsync(attachment, $"{FileContainer.EVENTS}/{eventData.Id}");
                        var emailAttachment = new EmailAttachment()
                        {
                            AttachmentId = attachmentFileStorage.Id,
                            EmailContentId = emailContent.Id,
                        };
                        await _db.EmailAttachments.AddAsync(emailAttachment);
                    }
                }
            }
            else if (request.EmailContent == null && emailContent != null)
            {
                var emailAttachments = _db.EmailAttachments.Where(e => e.EmailContentId == emailContent.Id).ToList();
                foreach (var attachment in emailAttachments)
                    await _fileService.DeleteFileByIdAsync(attachment.AttachmentId);
                _db.EmailAttachments.RemoveRange(emailAttachments);

                _db.EmailContents.Remove(emailContent);
            }
            else if (request.EmailContent != null && emailContent == null)
            {
                var newEmailContent = new EmailContent()
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = request.EmailContent.Content,
                    EventId = eventData.Id
                };
                await _db.EmailContents.AddAsync(newEmailContent);

                //TODO: Upload email attachments file
                if (request.EmailContent.Attachments != null && request.EmailContent.Attachments.Count > 0)
                {
                    foreach (var attachment in request.EmailContent.Attachments)
                    {
                        FileStorageVm attachmentFileStorage = await _fileService.SaveFileToFileStorageAsync(attachment, $"{FileContainer.EVENTS}/{eventData.Id}");
                        var emailAttachment = new EmailAttachment()
                        {
                            AttachmentId = attachmentFileStorage.Id,
                            EmailContentId = newEmailContent.Id,
                        };
                        await _db.EmailAttachments.AddAsync(emailAttachment);
                    }
                }
            }

            //TODO: Update ticket types
            var eventTicketTypes = _db.TicketTypes.Where(t => t.EventId == eventData.Id);
            _db.TicketTypes.RemoveRange(eventTicketTypes);
            if (request.EventPaymentType == EventPaymentType.PAID)
            {
                //TODO: Update ticket types
                foreach (var type in request.TicketTypes)
                {
                    var deserializedType = JsonConvert.DeserializeObject<TicketTypeCreateRequest>(type);
                    var ticketType = new TicketType()
                    {
                        Id = Guid.NewGuid().ToString(),
                        EventId = eventData.Id,
                        Name = deserializedType.Name,
                        Price = deserializedType.Price,
                        Quantity = deserializedType.Quantity
                    };
                    await _db.TicketTypes.AddAsync(ticketType);
                }
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
                await _db.EventCategories.AddAsync(eventCategory);
            }

            //TODO: Update event reasons
            var reasons = _db.Reasons.Where(e => e.EventId == eventData.Id);
            _db.Reasons.RemoveRange(reasons);
            if (request.Reasons != null && request.Reasons.Any())
            {
                foreach (var reason in request.Reasons)
                {
                    var reasonEntity = new Reason()
                    {
                        Id = Guid.NewGuid().ToString(),
                        EventId = eventData.Id,
                        Name = reason,
                    };
                    await _db.Reasons.AddAsync(reasonEntity);
                }
            }

            _db.Events.Update(eventData);
            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.EVENT}{id}");
            _cacheService.RemoveData(CacheKey.EVENTS);

            if (result > 0)
            {
                return Ok(new ApiOkResponse("Update event successfully!"));
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

            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (user == null || user.Id != eventData.CreatorId)
                return Forbid("Access denied!");

            //TODO: Delete cover image
            await _fileService.DeleteFileByIdAsync(eventData.CoverImageId);

            //TODO: Update event sub images
            var eventSubImages = _db.EventSubImages.Where(t => t.EventId == eventData.Id).ToList();
            foreach (var image in eventSubImages)
                await _fileService.DeleteFileByIdAsync(image.Id);
            _db.EventSubImages.RemoveRange(eventSubImages);

            //TODO: Delete email content
            var emailContent = await _db.EmailContents.FirstOrDefaultAsync(ec => ec.EventId == eventData.Id);
            if (emailContent != null)
            {
                var emailAttachments = _db.EmailAttachments.Where(e => e.EmailContentId == emailContent.Id).ToList();
                foreach (var attachment in emailAttachments)
                    await _fileService.DeleteFileByIdAsync(attachment.AttachmentId);
                _db.EmailAttachments.RemoveRange(emailAttachments);
                _db.EmailContents.Remove(emailContent);
            }

            //TODO: Delete ticket types
            var ticketTypes = _db.TicketTypes.Where(t => t.EventId == eventData.Id);
            if (ticketTypes != null && ticketTypes.Count() > 0)
                _db.TicketTypes.RemoveRange(ticketTypes);

            //TODO: Delete event categories
            var eventCategories = _db.EventCategories.Where(t => t.EventId == eventData.Id);
            if (eventCategories != null && eventCategories.Count() > 0)
                _db.EventCategories.RemoveRange(eventCategories);

            //TODO: Delete reasons
            var reasons = _db.Reasons.Where(e => e.EventId == eventData.Id);
            if (reasons != null && reasons.Count() > 0)
                _db.Reasons.RemoveRange(reasons);

            _db.Events.Remove(eventData);

            var creator = await _userManager.FindByIdAsync(eventData.CreatorId);
            creator.NumberOfCreatedEvents -= 1;

            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.EVENT}{id}");
            _cacheService.RemoveData(CacheKey.EVENTS);

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpPatch("move-to-trash")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.DELETE)]
        public async Task<IActionResult> PatchMoveEventToTrash([Required] List<string> ids)
        {
            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (user == null)
                return Forbid("Access denied!");

            var events = _db.Events
                .Join(ids, _event => _event.Id, id => id, (_event, id) => _event)
                .Where(e => e.IsTrash == false && e.CreatorId.Equals(user.Id));

            var result = await events.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsTrash, true));

            user.NumberOfCreatedEvents -= events.Count();
            await _userManager.UpdateAsync(user);

            await _db.SaveChangesAsync();

            ids.ForEach(id => _cacheService.RemoveData($"{CacheKey.EVENT}{id}"));
            _cacheService.RemoveData(CacheKey.EVENTS);

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpPatch("recover")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.UPDATE)]
        public async Task<IActionResult> PatchRecoverTrashEvent([Required] List<string> ids)
        {
            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (user == null)
                return Forbid("Access denied!");

            var events = _db.Events
                .Join(ids, _event => _event.Id, id => id, (_event, id) => _event)
                .Where(e => e.IsTrash == true && e.CreatorId.Equals(user.Id));

            var result = await events.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsTrash, false));

            user.NumberOfCreatedEvents += events.Count();
            await _userManager.UpdateAsync(user);

            await _db.SaveChangesAsync();

            ids.ForEach(id => _cacheService.RemoveData($"{CacheKey.EVENT}{id}"));
            _cacheService.RemoveData(CacheKey.EVENTS);

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpPatch("move-to-private")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.DELETE)]
        public async Task<IActionResult> PatchMoveEventToPrivate([Required] List<string> ids)
        {
            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (user == null)
                return Forbid("Access denied!");

            var events = _db.Events
                .Join(ids, _event => _event.Id, id => id, (_event, id) => _event)
                .Where(e => e.IsPrivate == false && e.IsTrash == false && e.CreatorId.Equals(user.Id));

            var result = await events.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsPrivate, true));

            await _db.SaveChangesAsync();

            ids.ForEach(id => _cacheService.RemoveData($"{CacheKey.EVENT}{id}"));
            _cacheService.RemoveData(CacheKey.EVENTS);

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpPatch("move-to-public")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.UPDATE)]
        public async Task<IActionResult> PatchMoveEventToPublic([Required] List<string> ids)
        {
            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (user == null)
                return Forbid("Access denied!");

            var events = _db.Events
                .Join(ids, _event => _event.Id, id => id, (_event, id) => _event)
                .Where(e => e.IsPrivate == true && e.IsTrash == false && e.CreatorId.Equals(user.Id));

            var result = await events.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsPrivate, false));

            await _db.SaveChangesAsync();

            ids.ForEach(id => _cacheService.RemoveData($"{CacheKey.EVENT}{id}"));
            _cacheService.RemoveData(CacheKey.EVENTS);

            if (result > 0)
            {
                return Ok(new ApiOkResponse());
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
        #endregion

        #region Reviews
        [HttpPost("{eventId}/reviews")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostReview(string eventId, [FromBody] ReviewCreateRequest request)
        {
            var dbEvent = await _db.Events.FindAsync(eventId);
            if (dbEvent == null || dbEvent.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not found"));

            var review = new Data.Entities.Review()
            {
                Id = Guid.NewGuid().ToString(),
                EventId = eventId,
                Content = request.Content,
                UserId = request.UserId,
                Rate = request.Rate,
            };
            var addedReview = await _db.Reviews.AddAsync(review);
            var result = await _db.SaveChangesAsync();

            var eventCoverImage = await _fileService.GetFileByFileIdAsync(dbEvent.CoverImageId);
            var userAvatar = await _fileService.GetFileByFileIdAsync(user.AvatarId);

            var reviewVm = new ReviewVm
            {
                Id = addedReview.Entity.Id,
                EventId = addedReview.Entity.EventId,
                EventCoverImage = eventCoverImage?.FilePath ?? "",
                EventName = dbEvent.Name,
                FullName = user.FullName,
                Email = user?.Email,
                UserAvatar = userAvatar?.FilePath ?? "",
                Content = addedReview.Entity.Content,
                UserId = addedReview.Entity.UserId,
                Rate = addedReview.Entity.Rate,
                CreatedAt = addedReview.Entity.CreatedAt,
                UpdatedAt = addedReview.Entity.UpdatedAt,
            };

            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<ReviewVm>($"{CacheKey.REVIEW}{reviewVm.Id}", reviewVm, expiryTime);
            _cacheService.RemoveData(CacheKey.REVIEWS);

            if (result > 0)
            {
                return CreatedAtAction(nameof(PostReview), reviewVm, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet("{eventId}/reviews")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.VIEW)]
        public async Task<IActionResult> GetReviews(string eventId, [FromQuery] PaginationFilter filter)
        {
            var dbEvent = await _db.Events.FindAsync(eventId);
            if (dbEvent == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            User user = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                user = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (dbEvent.IsTrash == true && (user == null || user.Id != dbEvent.CreatorId))
                return Forbid("Access denied!");
            if (dbEvent.IsPrivate == true && (user == null || user.Id != dbEvent.CreatorId))
                return Forbid("Access denied!");

            // Check cache data
            var reviewVms = new List<ReviewVm>();
            var cacheReviewVms = _cacheService.GetData<IEnumerable<ReviewVm>>(CacheKey.REVIEWS);
            if (cacheReviewVms != null && cacheReviewVms.Count() > 0)
                reviewVms = cacheReviewVms.ToList();
            else
            {
                var eventCoverImage = await _fileService.GetFileByFileIdAsync(dbEvent.CoverImageId);

                var fileStorages = await _fileService.GetListFileStoragesAsync();

                var users = (from _user in _userManager.Users.ToList()
                             join _fileStorage in fileStorages
                             on _user.AvatarId equals _fileStorage.Id
                             into joinedUsers
                             from _joinedUser in joinedUsers.DefaultIfEmpty()
                             select new UserVm
                             {
                                 Id = _user.Id,
                                 Email = _user.Email,
                                 FullName = _user.FullName,
                                 Avatar = _joinedUser != null && _joinedUser.FilePath != null ? _joinedUser.FilePath : null,
                             }); ;

                reviewVms = (from _review in _db.Reviews.ToList()
                             join _event in _db.Events.ToList()
                             on _review.EventId equals _event.Id
                             join _user in users on _review.UserId equals _user.Id
                             select new ReviewVm
                             {
                                 Id = _review.Id,
                                 EventId = _review.EventId,
                                 EventName = _event.Name,
                                 EventCoverImage = eventCoverImage?.FilePath ?? "",
                                 UserId = _review.UserId,
                                 UserAvatar = _user.Avatar,
                                 Email = _user?.Email,
                                 FullName = _user.FullName,
                                 Content = _review.Content,
                                 Rate = _review.Rate,
                                 CreatedAt = _review.CreatedAt,
                                 UpdatedAt = _review.UpdatedAt,
                             }).ToList();

                // Set expiry time
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<IEnumerable<ReviewVm>>(CacheKey.REVIEWS, reviewVms, expiryTime);
            }

            reviewVms = reviewVms.Where(r => r.EventId == eventId).ToList();

            if (!filter.search.IsNullOrEmpty())
            {
                reviewVms = reviewVms.Where(c => c.Content.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            reviewVms = filter.order switch
            {
                PageOrder.ASC => reviewVms.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => reviewVms.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => reviewVms
            };

            var metadata = new Metadata(reviewVms.Count(), filter.page, filter.size, filter.takeAll);

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

        [HttpGet("{eventId}/reviews/{reviewId}")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.VIEW)]
        public async Task<IActionResult> GetReviewById(string eventId, string reviewId)
        {
            // Check cache data
            ReviewVm reviewVm = null;
            var cacheReviewVm = _cacheService.GetData<ReviewVm>($"{CacheKey.REVIEW}{reviewId}");
            if (cacheReviewVm != null)
                reviewVm = cacheReviewVm;
            else
            {
                var review = await _db.Reviews.FindAsync(reviewId);
                if (review == null)
                    return NotFound(new ApiNotFoundResponse($"Review with id {reviewId} is not existed."));

                var dbEvent = await _db.Events.FindAsync(review.EventId);
                var eventCoverImage = await _fileService.GetFileByFileIdAsync(dbEvent.CoverImageId);

                var user = await _userManager.FindByIdAsync(review.UserId);
                var userAvatar = await _fileService.GetFileByFileIdAsync(user.AvatarId);

                reviewVm = new ReviewVm()
                {
                    Id = review.Id,
                    EventId = review.EventId,
                    EventName = dbEvent?.Name,
                    EventCoverImage = eventCoverImage?.FilePath ?? "",
                    UserId = review.UserId,
                    UserAvatar = userAvatar?.FilePath,
                    FullName = user?.FullName,
                    Email = user?.Email,
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

        [HttpPut("{eventId}/reviews/{reviewId}")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutReview(string eventId, string reviewId, [FromBody] ReviewCreateRequest request)
        {
            var review = await _db.Reviews.FindAsync(reviewId);
            if (review == null)
                return NotFound(new ApiNotFoundResponse($"Review with id {reviewId} is not existed."));

            var dbEvent = await _db.Events.FindAsync(request.EventId);
            if (dbEvent == null || dbEvent.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not found"));

            User creator = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                creator = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (creator == null || (creator.Id != dbEvent.CreatorId && creator.Id != review.UserId) || user.Id != review.UserId)
                return Forbid("Access denied");

            review.EventId = request.EventId;
            review.UserId = request.UserId;
            review.Content = request.Content;
            review.Rate = request.Rate;

            var updatedReview = _db.Reviews.Update(review);
            var result = await _db.SaveChangesAsync();

            var eventCoverImage = await _fileService.GetFileByFileIdAsync(dbEvent.CoverImageId);
            var avatar = await _fileService.GetFileByFileIdAsync(user.AvatarId);

            var reviewVm = new ReviewVm
            {
                Id = updatedReview.Entity.Id,
                EventId = updatedReview.Entity.EventId,
                EventCoverImage = eventCoverImage?.FilePath ?? "",
                Content = updatedReview.Entity.Content,
                UserId = updatedReview.Entity.UserId,
                Email = user?.Email,
                EventName = dbEvent?.Name,
                FullName = user?.FullName,
                UserAvatar = avatar?.FilePath,
                Rate = updatedReview.Entity.Rate,
                CreatedAt = updatedReview.Entity.CreatedAt,
                UpdatedAt = updatedReview.Entity.UpdatedAt,
            };

            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<ReviewVm>($"{CacheKey.REVIEW}{reviewVm.Id}", reviewVm, expiryTime);
            _cacheService.RemoveData(CacheKey.REVIEWS);

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{eventId}/reviews/{reviewId}")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteReview(string eventId, string reviewId)
        {
            var review = await _db.Reviews.FindAsync(reviewId);
            if (review == null)
                return NotFound(new ApiNotFoundResponse($"Review with id {reviewId} is not existed."));

            var dbEvent = await _db.Events.FindAsync(review.EventId);
            if (dbEvent == null || dbEvent.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            User creator = null;
            var accessToken = Request.Headers[HeaderNames.Authorization].ToString().Replace("Bearer ", "");
            if (!accessToken.IsNullOrEmpty())
            {
                var principal = _tokenService.GetPrincipalFromToken(accessToken);
                creator = await _userManager.FindByIdAsync(principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
            }
            if (creator == null || (creator.Id != dbEvent.CreatorId && creator.Id != review.UserId))
                return Forbid("Access denied");

            _db.Reviews.Remove(review);
            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.REVIEW}{reviewId}");
            _cacheService.RemoveData(CacheKey.REVIEWS);

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
            if (dbEvent == null || dbEvent.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {request.EventId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not existed."));

            var dbFavouriteEvent = await _db.FavouriteEvents.FirstOrDefaultAsync(f => f.EventId.Equals(request.EventId) && f.UserId.Equals(request.UserId));
            if (dbFavouriteEvent != null)
                return BadRequest(new ApiBadRequestResponse($"User has subscribed this event before"));

            var favouriteEvent = new FavouriteEvent
            {
                EventId = request.EventId,
                UserId = request.UserId,
            };

            _db.FavouriteEvents.Add(favouriteEvent);

            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.EVENT}{dbEvent.Id}");

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
            if (dbEvent == null || dbEvent.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {request.EventId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not existed."));

            var dbFavouriteEvent = await _db.FavouriteEvents.FirstOrDefaultAsync(f => f.EventId.Equals(request.EventId) && f.UserId.Equals(request.UserId));
            if (dbFavouriteEvent == null)
                return NotFound(new ApiNotFoundResponse($"User has not subscribed this event before"));

            _db.FavouriteEvents.Remove(dbFavouriteEvent);

            var result = await _db.SaveChangesAsync();

            _cacheService.RemoveData($"{CacheKey.EVENT}{dbEvent.Id}");

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

        #region Conversations
        [HttpGet("{eventId}/conversations")]
        [ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetConversationsByEventId(string eventId, [FromQuery] PaginationFilter filter)
        {
            var dbEvent = await _db.Events.FindAsync(eventId);
            if (dbEvent == null || dbEvent.IsTrash == true)
                return NotFound(new ApiNotFoundResponse($"Event with id {eventId} is not existed."));

            var fileStorages = await _fileService.GetListFileStoragesAsync();
            var conversationVms = (from conversation in _db.Conversations.ToList()
                                   join eventItem in (from eventEntity in _db.Events.ToList()
                                                      join file in fileStorages
                                                      on eventEntity.CoverImageId equals file.Id
                                                      into joinedEvents
                                                      from joinedEvent in joinedEvents.DefaultIfEmpty()
                                                      select new
                                                      {
                                                          Id = eventEntity.Id,
                                                          CoverImage = joinedEvent != null && joinedEvent.FilePath != null ? joinedEvent.FilePath : "",
                                                          Name = eventEntity.Name
                                                      })
                                   on conversation.EventId equals eventItem.Id
                                   join userItem in (from userEntity in _userManager.Users.ToList()
                                                     join file in fileStorages
                                                     on userEntity.AvatarId equals file.Id
                                                     into joinedUsers
                                                     from joinedUser in joinedUsers.DefaultIfEmpty()
                                                     select new
                                                     {
                                                         Id = userEntity.Id,
                                                         Avatar = joinedUser != null && joinedUser.FilePath != null ? joinedUser.FilePath : "",
                                                         FullName = userEntity.FullName
                                                     })
                                   on conversation.UserId equals userItem.Id
                                   join message in _db.Messages.ToList()
                                   on conversation.LastMessageId equals message.Id
                                   into joinedMessageConversations
                                   from joinedMessage in joinedMessageConversations.DefaultIfEmpty()
                                   where conversation.EventId == eventId
                                   select new ConversationVm
                                   {
                                       Id = conversation.Id,
                                       EventId = conversation.EventId,
                                       Event = new ConversationEventVm
                                       {
                                           Name = eventItem.Name,
                                           CoverImage = eventItem.CoverImage
                                       },
                                       HostId = conversation.HostId,
                                       UserId = conversation.UserId,
                                       User = new ConversationUserVm
                                       {
                                           Avatar = userItem.Avatar,
                                           FullName = userItem.FullName
                                       },
                                       LastMessage = joinedMessage != null ? new ConversationLastMessageVm
                                       {
                                           Content = joinedMessage.Content,
                                           SenderId = joinedMessage.UserId,
                                       } : null,
                                       CreatedAt = conversation.CreatedAt,
                                       UpdatedAt = conversation.UpdatedAt
                                   }).ToList();


            if (!filter.search.IsNullOrEmpty())
            {
                conversationVms = conversationVms.Where(c => c.Event.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            conversationVms = filter.order switch
            {
                PageOrder.ASC => conversationVms.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => conversationVms.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => conversationVms
            };

            var metadata = new Metadata(conversationVms.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                conversationVms = conversationVms.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<ConversationVm>
            {
                Items = conversationVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }
        #endregion

        //#region Payments
        //[HttpGet("{id}/payments")]
        //[ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.VIEW)]
        //public async Task<IActionResult> GetPaymentsByEventId(string id, [FromQuery] PaymentPaginationFilter filter)
        //{
        //    var eventData = await _db.Events.FindAsync(id);
        //    if (eventData == null || eventData.IsTrash == true)
        //        return NotFound(new ApiNotFoundResponse($"Event with id {id} does not exist!"));

        //    var payments = _db.Payments.Where(p => p.EventId.Equals(id)).ToList();

        //    if (filter.search != null)
        //    {
        //        payments = payments.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
        //                                       c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
        //                                       c.CustomerPhone.ToLower().Contains(filter.search.ToLower())).ToList();
        //    }

        //    payments = filter.order switch
        //    {
        //        PageOrder.ASC => payments.OrderBy(c => c.CreatedAt).ToList(),
        //        PageOrder.DESC => payments.OrderByDescending(c => c.CreatedAt).ToList(),
        //        _ => payments
        //    };

        //    payments = payments.Where(p => p.Status.Equals(filter.status)).ToList();

        //    var metadata = new Metadata(payments.Count(), filter.page, filter.size, filter.takeAll);

        //    if (filter.takeAll == false)
        //    {
        //        payments = payments.Skip((filter.page - 1) * filter.size)
        //            .Take(filter.size).ToList();
        //    }

        //    var paymentVms = payments.Select(p => new PaymentVm()
        //    {
        //        Id = p.Id,
        //        CustomerName = p.CustomerName,
        //        CustomerEmail = p.CustomerEmail,
        //        CustomerPhone = p.CustomerPhone,
        //        Discount = p.Discount,
        //        Status = p.Status,
        //        EventId = p.EventId,
        //        PaymentMethod = (PaymentMethod)p.PaymentMethod,
        //        PaymentSessionId = p.PaymentSessionId,
        //        TicketQuantity = p.TicketQuantity,
        //        TotalPrice = p.TotalPrice,
        //        UserId = p.UserId,
        //        PaymentIntentId = p.PaymentIntentId,
        //        CreatedAt = p.CreatedAt,
        //        UpdatedAt = p.UpdatedAt
        //    }).ToList();

        //    var pagination = new Pagination<PaymentVm>
        //    {
        //        Items = paymentVms,
        //        Metadata = metadata,
        //    };

        //    Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

        //    return Ok(new ApiOkResponse(pagination));
        //}
        //#endregion

        //#region Tickets
        //[HttpGet("{id}/tickets")]
        //[ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.VIEW)]
        //public async Task<IActionResult> GetTicketsByEventId(string id, [FromQuery] PaginationFilter filter)
        //{
        //    var eventData = await _db.Events.FindAsync(id);
        //    if (eventData == null || eventData.IsTrash == true)
        //        return NotFound(new ApiNotFoundResponse($"Event with id {id} does not exist!"));

        //    var tickets = _db.Tickets.Where(t => t.EventId == id).ToList();

        //    if (!filter.search.IsNullOrEmpty())
        //    {
        //        tickets = tickets.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
        //                                     c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
        //                                     c.CustomerPhone.ToLower().Contains(filter.search.ToLower())
        //        ).ToList();
        //    }

        //    tickets = filter.order switch
        //    {
        //        PageOrder.ASC => tickets.OrderBy(c => c.CreatedAt).ToList(),
        //        PageOrder.DESC => tickets.OrderByDescending(c => c.CreatedAt).ToList(),
        //        _ => tickets
        //    };

        //    var metadata = new Metadata(tickets.Count(), filter.page, filter.size, filter.takeAll);

        //    if (filter.takeAll == false)
        //    {
        //        tickets = tickets.Skip((filter.page - 1) * filter.size)
        //            .Take(filter.size).ToList();
        //    }

        //    var ticketVms = tickets.Select(t => new TicketVm()
        //    {
        //        Id = t.Id,
        //        EventId = t.EventId,
        //        CustomerName = t.CustomerName,
        //        CustomerPhone = t.CustomerPhone,
        //        CustomerEmail = t.CustomerEmail,
        //        Status = t.Status,
        //        PaymentId = t.PaymentId,
        //        TicketTypeId = t.TicketTypeId,
        //        CreatedAt = t.CreatedAt,
        //        UpdatedAt = t.UpdatedAt
        //    }).ToList();

        //    var pagination = new Pagination<TicketVm>
        //    {
        //        Items = ticketVms,
        //        Metadata = metadata,
        //    };

        //    Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

        //    return Ok(new ApiOkResponse(pagination));
        //}
        //#endregion
    }
}

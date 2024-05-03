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
using Microsoft.OpenApi.Extensions;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileService;
        private readonly ICacheService _cacheService;

        public UsersController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db, IFileStorageService fileService, ICacheService cacheService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _fileService = fileService;
            _cacheService = cacheService;
        }

        #region Users

        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.CREATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostUser([FromForm] UserCreateRequest request)
        {
            var user = new User()
            {
                Id = Guid.NewGuid().ToString(),
                UserName = request.UserName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Dob = request.Dob,
                FullName = request.FullName,
                Gender = request.Gender,
                Bio = request.Bio,
                Status = UserStatus.ACTIVE
            };

            var userVm = new UserVm()
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Dob = user.Dob,
                FullName = user.FullName,
                Gender = user.Gender,
                Bio = user.Bio,
                NumberOfCreatedEvents = user.NumberOfCreatedEvents,
                NumberOfFavourites = user.NumberOfFavourites,
                NumberOfFolloweds = user.NumberOfFolloweds,
                NumberOfFollowers = user.NumberOfFollowers,
                Status = user.Status,
                Roles = new List<string> { UserRole.CUSTOMER.GetDisplayName(), UserRole.ORGANIZER.GetDisplayName() },
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            //TODO: Upload avatar image
            if (request.Avatar != null)
            {
                var avatarImage = await _fileService.SaveFileToFileStorageAsync(request.Avatar, FileContainer.USERS);
                user.AvatarId = avatarImage.Id;
                userVm.Avatar = avatarImage.FilePath;
            }

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRolesAsync(user, new List<string> { UserRole.CUSTOMER.GetDisplayName(), UserRole.ORGANIZER.GetDisplayName() });

                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<UserVm>($"{CacheKey.USER}{userVm.Id}", userVm, expiryTime);

                return CreatedAtAction(nameof(PostUser), userVm, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(result));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.VIEW)]
        public async Task<IActionResult> GetUsers([FromQuery] PaginationFilter filter)
        {
            // Check cache data
            var userVms = new List<UserVm>();
            var cacheUserVms = _cacheService.GetData<IEnumerable<UserVm>>(CacheKey.USERS);
            if (cacheUserVms != null && cacheUserVms.Count() > 0)
                userVms = cacheUserVms.ToList();
            else
            {
                var users = _userManager.Users.ToList();

                var fileStorages = await _fileService.GetListFileStoragesAsync();

                userVms = (from u in users
                           join f in fileStorages
                               on u.AvatarId equals f.Id
                               into UsersWithAvatar
                           from uwa in UsersWithAvatar.DefaultIfEmpty()
                           select new UserVm
                           {
                               Id = u.Id,
                               UserName = u.UserName,
                               Email = u.Email,
                               PhoneNumber = u.PhoneNumber,
                               Dob = u.Dob,
                               FullName = u.FullName,
                               Gender = u.Gender,
                               Bio = u.Bio,
                               NumberOfCreatedEvents = u.NumberOfCreatedEvents,
                               NumberOfFavourites = u.NumberOfFavourites,
                               NumberOfFolloweds = u.NumberOfFolloweds,
                               NumberOfFollowers = u.NumberOfFollowers,
                               Status = u.Status,
                               Avatar = uwa?.FilePath,
                               CreatedAt = u.CreatedAt,
                               UpdatedAt = u.UpdatedAt
                           }).ToList();
            }

            var metadata = new Metadata(userVms.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.search != null)
            {
                userVms = userVms.Where(u =>
                    u.FullName.ToLower().Contains(filter.search.ToLower()) ||
                    u.Email.ToLower().Contains(filter.search.ToLower()) ||
                    u.UserName.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            userVms = filter.order switch
            {
                PageOrder.ASC => userVms.OrderBy(u => u.CreatedAt).ToList(),
                PageOrder.DESC => userVms.OrderByDescending(u => u.CreatedAt).ToList(),
                _ => userVms
            };

            if (filter.takeAll == false)
            {
                userVms = userVms.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<UserVm>
            {
                Items = userVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.VIEW)]
        public async Task<IActionResult> GetUserById(string id)
        {
            UserVm userVm = null;
            var cacheUserVm = _cacheService.GetData<UserVm>($"{CacheKey.USER}{id}");
            if (cacheUserVm != null)
                userVm = cacheUserVm;
            else
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound(new ApiNotFoundResponse(""));

                var avatar = await _fileService.GetFileByFileIdAsync(user.AvatarId);

                var roles = await _userManager.GetRolesAsync(user);

                userVm = new UserVm()
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Dob = user.Dob,
                    FullName = user.FullName,
                    Gender = user.Gender,
                    Bio = user.Bio,
                    NumberOfCreatedEvents = user.NumberOfCreatedEvents,
                    NumberOfFavourites = user.NumberOfFavourites,
                    NumberOfFolloweds = user.NumberOfFolloweds,
                    NumberOfFollowers = user.NumberOfFollowers,
                    Status = user.Status,
                    Avatar = avatar.FilePath,
                    Roles = roles.ToList(),
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                };

                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<UserVm>($"{CacheKey.USER}{userVm.Id}", userVm, expiryTime);
            }

            return Ok(new ApiOkResponse(userVm));
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.UPDATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutUser(string id, [FromForm] UserUpdateRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new ApiNotFoundResponse(""));

            user.UserName = request.UserName;
            user.Email = request.Email;
            user.PhoneNumber = request.PhoneNumber;
            user.Dob = request.Dob;
            user.FullName = request.FullName;
            user.Gender = request.Gender;
            user.Bio = request.Bio;

            var userVm = new UserVm()
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Dob = user.Dob,
                FullName = user.FullName,
                Gender = user.Gender,
                Bio = user.Bio,
                NumberOfCreatedEvents = user.NumberOfCreatedEvents,
                NumberOfFavourites = user.NumberOfFavourites,
                NumberOfFolloweds = user.NumberOfFolloweds,
                NumberOfFollowers = user.NumberOfFollowers,
                Status = user.Status,
                Roles = new List<string> { UserRole.CUSTOMER.GetDisplayName(), UserRole.ORGANIZER.GetDisplayName() },
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            //TODO: Upload avatar image
            if (request.Avatar != null)
            {
                var avatarImage = await _fileService.SaveFileToFileStorageAsync(request.Avatar, FileContainer.USERS);
                user.AvatarId = avatarImage.Id;
                userVm.Avatar = avatarImage.FilePath;
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<UserVm>($"{CacheKey.USER}{userVm.Id}", userVm, expiryTime);

                return Ok(userVm);
            }

            return BadRequest(new ApiBadRequestResponse(result));
        }

        [HttpPatch("{id}/change-password")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PatchUserPassword(string id, [FromBody] UserPasswordChangeRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new ApiNotFoundResponse(""));

            var result = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);

            if (result.Succeeded)
            {
                return NoContent();
            }

            return BadRequest(new ApiBadRequestResponse(result));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new ApiNotFoundResponse(""));

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                var userVm = new UserVm()
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Dob = user.Dob,
                    FullName = user.FullName,
                    Gender = user.Gender,
                    Bio = user.Bio,
                    NumberOfCreatedEvents = user.NumberOfCreatedEvents,
                    NumberOfFavourites = user.NumberOfFavourites,
                    NumberOfFolloweds = user.NumberOfFolloweds,
                    NumberOfFollowers = user.NumberOfFollowers,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                };

                _cacheService.RemoveData($"{CacheKey.USER}{userVm.Id}");

                return Ok(new ApiOkResponse(userVm));
            }

            return BadRequest(new ApiBadRequestResponse(result));
        }

        #endregion

        [HttpGet("{userId}/menu")]
        public async Task<IActionResult> GetMenuByUserPermission(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);
            var query = from f in _db.Functions
                        join p in _db.Permissions
                            on f.Id equals p.FunctionId
                        join r in _roleManager.Roles on p.RoleId equals r.Id
                        join a in _db.Commands
                            on p.CommandId equals a.Id
                        where roles.Contains(r.Name) && a.Id == "VIEW"
                        select new FunctionVm
                        {
                            Id = f.Id,
                            Name = f.Name,
                            Url = f.Url,
                            ParentId = f.ParentId,
                            SortOrder = f.SortOrder,
                        };
            var data = await query.Distinct()
                .OrderBy(x => x.ParentId)
                .ThenBy(x => x.SortOrder)
                .ToListAsync();
            return Ok(new ApiOkResponse(data));
        }

        [HttpGet("{userId}/reviews")]
        [ClaimRequirement(FunctionCode.CONTENT_REVIEW, CommandCode.VIEW)]
        public async Task<IActionResult> GetReviewsByUserId(string userId, [FromQuery] PaginationFilter filter)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} is not existed."));

            // Check cache data
            var reviewVms = new List<ReviewVm>();
            var cacheReviewVms = _cacheService.GetData<IEnumerable<ReviewVm>>(CacheKey.REVIEWS);
            if (cacheReviewVms != null && cacheReviewVms.Count() > 0)
                reviewVms = cacheReviewVms.ToList();
            else
            {
                var userAvatar = await _fileService.GetFileByFileIdAsync(user.AvatarId);
                reviewVms = _db.Reviews.Where(r => r.UserId == userId).ToList().Join(_db.Events, _review => _review.EventId, _event => _event.Id,
                    (_review, _event) => new ReviewVm
                    {
                        Id = _review.Id,
                        EventId = _review.EventId,
                        EventName = _event.Name,
                        UserId = _review.UserId,
                        UserAvatar = userAvatar?.FilePath,
                        UserName = user?.FullName,
                        Content = _review.Content,
                        Rate = _review.Rate,
                        CreatedAt = _review.CreatedAt,
                        UpdatedAt = _review.UpdatedAt,
                    }).ToList();
            }

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

            var pagination = new Pagination<ReviewVm>
            {
                Items = reviewVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{userId}/events/favourites")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.VIEW)]
        [ApiValidationFilter]
        public async Task<IActionResult> GetFavoutireEventsByUserId(string userId,
            [FromQuery] EventPaginationFilter filter)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} is not existed."));
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
                                                            Name = _category.Name,
                                                            CreatedAt = _category.CreatedAt,
                                                            UpdatedAt = _category.UpdatedAt,
                                                        })
                                       on _eventCategory.CategoryId equals _categoryVm.Id
                                       into joinedEventCategories
                                   from _joinedEventCategory in joinedEventCategories.DefaultIfEmpty()
                                   select new
                                   {
                                       EventId = _eventCategory.EventId,
                                       CategoryId = _eventCategory.CategoryId,
                                       CategoryVm = _joinedEventCategory,
                                   }
            );

            var eventDatas = _db.FavouriteEvents.ToList()
                .Join(_db.Events.ToList(), _favouriteEvent => _favouriteEvent.EventId, _event => _event.Id,
                    (_favouriteEvent, _event) => new
                    {
                        _favouriteEvent,
                        _event
                    })
                .Where(joinedEvent => joinedEvent._favouriteEvent.UserId == userId)
                .Select(joinedEvent => joinedEvent._event)
                .ToList();

            var joinedEventVms = (from _event in eventDatas
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
                                      CoverImage = _joinedCoverImageEvent.FilePath,
                                      StartTime = _event.StartTime,
                                      EndTime = _event.EndTime,
                                      Promotion = _event.Promotion ?? 0.0,
                                      EventCycleType = _event.EventCycleType,
                                      EventPaymentType = _event.EventPaymentType,
                                      Status = _event.Status,
                                      IsPrivate = _event.IsPrivate,
                                      IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
                                      LocationString = _joinedLocationEvent != null
                                          ? $"{_joinedLocationEvent.Street}, {_joinedLocationEvent.District}, {_joinedLocationEvent.City}"
                                          : "",
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

            var eventVms = (from _eventVm in joinedTicketTypeEventVms
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
                    eventVms = eventVms.Where(e => e.StartTime <= DateTime.UtcNow && DateTime.UtcNow <= e.EndTime)
                        .ToList();
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
                eventVms = eventVms.Where(e =>
                        e.LocationString.Split(", ").Any(str => filter.location.ToLower().Contains(str.ToLower())))
                    .ToList();
            }

            if (!filter.categoryIds.IsNullOrEmpty())
            {
                eventVms = eventVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e =>
                    filter.priceRange.StartRange <= e.PriceRange.StartRange &&
                    filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
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

        [HttpGet("{userId}/events")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.VIEW)]
        [ApiValidationFilter]
        public async Task<IActionResult> GetEventsByUserId(string userId,
            [FromQuery] EventPaginationFilter filter)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} is not existed."));
            // Check cache data
            var eventVms = new List<EventVm>();
            var cacheEventVms = _cacheService.GetData<IEnumerable<EventVm>>($"{CacheKey.EVENTS}{userId}");
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

                var joinedEventVms = (from _event in _db.Events.Where(e => e.CreatorId.Equals(userId)).ToList()
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
                                          CoverImage = _joinedCoverImageEvent.FilePath,
                                          StartTime = _event.StartTime,
                                          EndTime = _event.EndTime,
                                          Promotion = _event.Promotion ?? 0.0,
                                          Status = _event.Status,
                                          IsPrivate = _event.IsPrivate,
                                          IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
                                          EventPaymentType = _event.EventPaymentType,
                                          EventCycleType = _event.EventCycleType,
                                          LocationString = _joinedLocationEvent != null ? $"{_joinedLocationEvent.Street}, {_joinedLocationEvent.District}, {_joinedLocationEvent.City}" : "",
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
                    eventVms = eventVms.Where(e => e.StartTime <= DateTime.UtcNow && DateTime.UtcNow <= e.EndTime)
                        .ToList();
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
                eventVms = eventVms.Where(e =>
                        e.LocationString.Split(", ").Any(str => filter.location.ToLower().Contains(str.ToLower())))
                    .ToList();
            }

            if (!filter.categoryIds.IsNullOrEmpty())
            {
                eventVms = eventVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e =>
                    filter.priceRange.StartRange <= e.PriceRange.StartRange &&
                    filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
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

        #region Followers

        [HttpPost("followers/follow")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostFollowUser([FromBody] FollowerCreateRequest request)
        {
            var follower = await _userManager.FindByIdAsync(request.FollowerId);
            if (follower == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowerId} is not existed."));

            var followed = await _userManager.FindByIdAsync(request.FollowedId);
            if (followed == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowedId} is not existed."));

            var userFollower = await _db.UserFollowers.FindAsync(request.FollowedId, request.FollowedId);
            if (userFollower != null)
                return BadRequest(new ApiBadRequestResponse($"User has been followed before"));


            userFollower = new UserFollower
            {
                FollowerId = request.FollowerId,
                FollowedId = request.FollowedId,
            };
            _db.UserFollowers.Add(userFollower);

            followed.NumberOfFollowers += 1;
            follower.NumberOfFolloweds += 1;

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(PostFollowUser),
                    new { followerId = userFollower.FollowerId, followedId = userFollower.FollowedId }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpPost("followers/unfollow")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.DELETE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostUnFollowUser([FromBody] FollowerCreateRequest request)
        {
            var follower = await _userManager.FindByIdAsync(request.FollowerId);
            if (follower == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowerId} is not existed."));

            var followed = await _userManager.FindByIdAsync(request.FollowedId);
            if (followed == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowedId} is not existed."));

            var userFollower = await _db.UserFollowers.FindAsync(request.FollowedId, request.FollowedId);
            if (userFollower == null)
                return NotFound(new ApiNotFoundResponse($"User has not been followed before"));

            _db.UserFollowers.Remove(userFollower);

            followed.NumberOfFollowers -= 1;
            follower.NumberOfFolloweds -= 1;

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
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
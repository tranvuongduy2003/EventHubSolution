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
        private readonly StripeService _stripeService;

        public UsersController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db, IFileStorageService fileService, ICacheService cacheService, StripeService stripeService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _fileService = fileService;
            _cacheService = cacheService;
            _stripeService = stripeService;
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
                _cacheService.RemoveData(CacheKey.USERS);

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
                var roles = (from _role in _db.Roles
                             join _userRole in _db.UserRoles
                             on _role.Id equals _userRole.RoleId
                             select new
                             {
                                 UserId = _userRole.UserId,
                                 RoleId = _userRole.RoleId,
                                 Role = _role,
                             })
                             .GroupBy(_joinedRole => _joinedRole.UserId)
                             .Select(_groupedRole => new
                             {
                                 UserId = _groupedRole.Key,
                                 Roles = _groupedRole.Select(r => r.Role.Name),
                             });
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                userVms = (from u in users
                           join f in fileStorages
                           on u.AvatarId equals f.Id
                           into UsersWithAvatar
                           from uwa in UsersWithAvatar.DefaultIfEmpty()
                           join _role in roles
                           on u.Id equals _role.UserId
                           into joinedUserRoles
                           from _joinedUserRole in joinedUserRoles.DefaultIfEmpty()
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
                               Roles = _joinedUserRole?.Roles.ToList(),
                               Avatar = uwa?.FilePath,
                               CreatedAt = u.CreatedAt,
                               UpdatedAt = u.UpdatedAt
                           })
                           .ToList();
            }

            if (!filter.search.IsNullOrEmpty())
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

            var metadata = new Metadata(userVms.Count(), filter.page, filter.size, filter.takeAll);

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
                    Avatar = avatar?.FilePath ?? "",
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
                await _fileService.DeleteFileByIdAsync(user.AvatarId);
                var avatarImage = await _fileService.SaveFileToFileStorageAsync(request.Avatar, FileContainer.USERS);
                user.AvatarId = avatarImage.Id;
                userVm.Avatar = avatarImage.FilePath;
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<UserVm>($"{CacheKey.USER}{userVm.Id}", userVm, expiryTime);
                _cacheService.RemoveData(CacheKey.USERS);

                return Ok(new ApiOkResponse(userVm));
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

            await _fileService.DeleteFileByIdAsync(user.AvatarId);

            var userRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, userRoles);

            var result = await _userManager.DeleteAsync(user);
            await _db.SaveChangesAsync();

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
                _cacheService.RemoveData(CacheKey.USERS);

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
                             });

                var events = (from _event in _db.Events.ToList()
                              join _fileStorage in fileStorages
                              on _event.CoverImageId equals _fileStorage.Id
                              into joinedEvents
                              from _joinedEvent in joinedEvents.DefaultIfEmpty()
                              select new EventVm
                              {
                                  Id = _event.Id,
                                  Name = _event.Name,
                                  CoverImage = _joinedEvent != null && _joinedEvent.FilePath != null ? _joinedEvent.FilePath : null,
                              });

                reviewVms = (from _review in _db.Reviews.ToList()
                             join _event in events on _review.EventId equals _event.Id
                             join _user in users on _review.UserId equals _user.Id
                             select new ReviewVm
                             {
                                 Id = _review.Id,
                                 EventId = _review.EventId,
                                 EventName = _event.Name,
                                 EventCoverImage = _event.CoverImage,
                                 UserId = _review.UserId,
                                 UserAvatar = _user.Avatar,
                                 Email = _user.Email,
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

            reviewVms = reviewVms
                .Join(_db.Events.ToList(),
                    _review => _review.EventId,
                    _event => _event.Id,
                    (_review, _event) => new { _review, _event })
                .Where(_joinedReview => _joinedReview._event.CreatorId == user.Id && _joinedReview._event.IsTrash == false)
                .Select(_joinedReview => _joinedReview._review)
                .ToList();


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
                .Where(joinedEvent => joinedEvent._favouriteEvent.UserId == userId && joinedEvent._event.IsTrash == false)
                .Select(joinedEvent => joinedEvent._event)
                .ToList();

            var joinedEventVms = (from _event in eventDatas
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
                                      CoverImage = _joinedCoverImageEvent?.FilePath ?? "",
                                      StartTime = _event.StartTime,
                                      EndTime = _event.EndTime,
                                      Promotion = _event.Promotion ?? 0.0,
                                      EventCycleType = _event.EventCycleType,
                                      EventPaymentType = _event.EventPaymentType,
                                      Status = _event.StartTime > DateTime.UtcNow ? EventStatus.UPCOMING : _event.EndTime < DateTime.UtcNow ? EventStatus.CLOSED : EventStatus.OPENING,
                                      IsPrivate = _event.IsPrivate,
                                      IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
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
                eventVms = eventVms.Where(e => e.Location.ToLower().Contains(filter.location.ToLower())).ToList();
            }

            if (filter.categoryIds != null && filter.categoryIds.Count > 0)
            {
                eventVms = eventVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e =>
                    filter.priceRange.StartRange <= e.PriceRange.StartRange &&
                    filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
            }

            if (filter.rates != null)
            {
                eventVms = eventVms.Where(e => filter.rates.Where(r => (r - 0.5) < e.AverageRating && e.AverageRating <= (r + 0.5)).Any()).ToList();
            }

            var metadata = new EventMetadata(
               eventVms.Count(),
               filter.page,
               filter.size,
               filter.takeAll,
               eventVms.Count(e => !e.IsPrivate),
               eventVms.Count(e => e.IsPrivate)
           );

            switch (filter.eventPrivacy)
            {
                case EventPrivacy.PUBLIC:
                    eventVms = eventVms.Where(c => !c.IsPrivate).ToList();
                    break;
                case EventPrivacy.PRIVATE:
                    eventVms = eventVms.Where(c => c.IsPrivate).ToList();
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

            var joinedEventVms = (from _event in _db.Events.Where(e => e.CreatorId.Equals(userId) && e.IsTrash == false).ToList()
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
                                      CoverImage = _joinedCoverImageEvent?.FilePath ?? "",
                                      StartTime = _event.StartTime,
                                      EndTime = _event.EndTime,
                                      Promotion = _event.Promotion ?? 0.0,
                                      Status = _event.StartTime > DateTime.UtcNow ? EventStatus.UPCOMING : _event.EndTime < DateTime.UtcNow ? EventStatus.CLOSED : EventStatus.OPENING,
                                      IsPrivate = _event.IsPrivate,
                                      IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
                                      EventPaymentType = _event.EventPaymentType,
                                      EventCycleType = _event.EventCycleType,
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
                eventVms = eventVms.Where(e => e.Location.ToLower().Contains(filter.location.ToLower())).ToList();
            }

            if (filter.categoryIds != null && filter.categoryIds.Count > 0)
            {
                eventVms = eventVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e =>
                    filter.priceRange.StartRange <= e.PriceRange.StartRange &&
                    filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
            }

            if (filter.rates != null)
            {
                eventVms = eventVms.Where(e => filter.rates.Where(r => (r - 0.5) < e.AverageRating && e.AverageRating <= (r + 0.5)).Any()).ToList();
            }

            var metadata = new EventMetadata(
                eventVms.Count(),
                filter.page,
                filter.size,
                filter.takeAll,
                eventVms.Count(e => !e.IsPrivate),
                eventVms.Count(e => e.IsPrivate)
            );

            switch (filter.eventPrivacy)
            {
                case EventPrivacy.PUBLIC:
                    eventVms = eventVms.Where(c => !c.IsPrivate).ToList();
                    break;
                case EventPrivacy.PRIVATE:
                    eventVms = eventVms.Where(c => c.IsPrivate).ToList();
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

        [HttpGet("{userId}/events/trash")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.VIEW)]
        [ApiValidationFilter]
        public async Task<IActionResult> GetTrashEventsByUserId(string userId,
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

            var joinedEventVms = (from _event in _db.Events.Where(e => e.CreatorId.Equals(userId) && e.IsTrash == true).ToList()
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
                                      CoverImage = _joinedCoverImageEvent?.FilePath ?? "",
                                      StartTime = _event.StartTime,
                                      EndTime = _event.EndTime,
                                      Promotion = _event.Promotion ?? 0.0,
                                      Status = _event.StartTime > DateTime.UtcNow ? EventStatus.UPCOMING : _event.EndTime < DateTime.UtcNow ? EventStatus.CLOSED : EventStatus.OPENING,
                                      IsPrivate = _event.IsPrivate,
                                      IsTrash = (bool)(_event.IsTrash != null ? _event.IsTrash : false),
                                      EventPaymentType = _event.EventPaymentType,
                                      EventCycleType = _event.EventCycleType,
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
                eventVms = eventVms.Where(e => e.Location.ToLower().Contains(filter.location.ToLower())).ToList();
            }

            if (filter.categoryIds != null && filter.categoryIds.Count > 0)
            {
                eventVms = eventVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventVms = eventVms.Where(e =>
                    filter.priceRange.StartRange <= e.PriceRange.StartRange &&
                    filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
            }

            if (filter.rates != null)
            {
                eventVms = eventVms.Where(e => filter.rates.Where(r => (r - 0.5) < e.AverageRating && e.AverageRating <= (r + 0.5)).Any()).ToList();
            }

            var metadata = new EventMetadata(
                eventVms.Count(),
                filter.page,
                filter.size,
                filter.takeAll,
                eventVms.Count(e => !e.IsPrivate),
                eventVms.Count(e => e.IsPrivate)
            );

            switch (filter.eventPrivacy)
            {
                case EventPrivacy.PUBLIC:
                    eventVms = eventVms.Where(c => !c.IsPrivate).ToList();
                    break;
                case EventPrivacy.PRIVATE:
                    eventVms = eventVms.Where(c => c.IsPrivate).ToList();
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

        [HttpGet("{userId}/conversations-by-user")]
        [ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetConversationsByUserId(string userId, [FromQuery] PaginationFilter filter)
        {
            var fileStorages = await _fileService.GetListFileStoragesAsync();
            var users = from userEntity in _userManager.Users.ToList()
                        join file in fileStorages
                        on userEntity.AvatarId equals file.Id
                        into joinedUsers
                        from joinedUser in joinedUsers.DefaultIfEmpty()
                        select new
                        {
                            Id = userEntity.Id,
                            Avatar = joinedUser != null && joinedUser.FilePath != null ? joinedUser.FilePath : "",
                            FullName = userEntity.FullName
                        };
            var conversationVms = (from conversation in _db.Conversations.ToList()
                                   join eventItem in (from eventEntity in _db.Events.Where(e => e.IsTrash == false).ToList()
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
                                   join userItem in users
                                   on conversation.UserId equals userItem.Id
                                   join host in users
                                   on conversation.HostId equals host.Id
                                   join message in _db.Messages.ToList()
                                   on conversation.LastMessageId equals message.Id
                                   into joinedMessageConversations
                                   from joinedMessage in joinedMessageConversations.DefaultIfEmpty()
                                   where conversation.UserId == userId
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
                                       Host = new ConversationUserVm
                                       {
                                           Avatar = host.Avatar,
                                           FullName = host.FullName
                                       },
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

        [HttpGet("{hostId}/conversations-by-host")]
        [ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetConversationsByHostId(string hostId, [FromQuery] PaginationFilter filter)
        {

            var fileStorages = await _fileService.GetListFileStoragesAsync();
            var users = from userEntity in _userManager.Users.ToList()
                        join file in fileStorages
                        on userEntity.AvatarId equals file.Id
                        into joinedUsers
                        from joinedUser in joinedUsers.DefaultIfEmpty()
                        select new
                        {
                            Id = userEntity.Id,
                            Avatar = joinedUser != null && joinedUser.FilePath != null ? joinedUser.FilePath : "",
                            FullName = userEntity.FullName
                        };
            var conversationVms = (from conversation in _db.Conversations.ToList()
                                   join eventItem in (from eventEntity in _db.Events.Where(e => e.IsTrash == false).ToList()
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
                                   join userItem in users
                                   on conversation.UserId equals userItem.Id
                                   join host in users
                                   on conversation.HostId equals host.Id
                                   join message in _db.Messages.ToList()
                                   on conversation.LastMessageId equals message.Id
                                   into joinedMessageConversations
                                   from joinedMessage in joinedMessageConversations.DefaultIfEmpty()
                                   where conversation.HostId == hostId
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
                                       Host = new ConversationUserVm
                                       {
                                           Avatar = host.Avatar,
                                           FullName = host.FullName
                                       },
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

        #region Followers
        [HttpPost("followers/follow")]
        //[ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostFollowUser([FromBody] FollowerCreateRequest request)
        {
            var follower = await _userManager.FindByIdAsync(request.FollowerId);
            if (follower == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowerId} is not existed."));

            var followed = await _userManager.FindByIdAsync(request.FollowedId);
            if (followed == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowedId} is not existed."));

            var userFollower = await _db.UserFollowers.FindAsync(request.FollowerId, request.FollowedId);
            if (userFollower != null)
                return BadRequest(new ApiBadRequestResponse($"User has been followed before"));

            userFollower = new UserFollower
            {
                FollowerId = request.FollowerId,
                FollowedId = request.FollowedId,
            };
            await _db.UserFollowers.AddAsync(userFollower);
            var result = await _db.SaveChangesAsync();

            followed.NumberOfFollowers += 1;
            follower.NumberOfFolloweds += 1;

            await _userManager.UpdateAsync(follower);
            await _userManager.UpdateAsync(followed);
            await _db.SaveChangesAsync();


            if (result > 0)
            {
                var avatar = await _fileService.GetFileByFileIdAsync(follower.AvatarId);
                var roles = await _userManager.GetRolesAsync(follower);
                var userVm = new UserVm()
                {
                    Id = follower.Id,
                    UserName = follower.UserName,
                    Email = follower.Email,
                    PhoneNumber = follower.PhoneNumber,
                    Dob = follower.Dob,
                    FullName = follower.FullName,
                    Gender = follower.Gender,
                    Bio = follower.Bio,
                    NumberOfCreatedEvents = follower.NumberOfCreatedEvents,
                    NumberOfFavourites = follower.NumberOfFavourites,
                    NumberOfFolloweds = follower.NumberOfFolloweds,
                    NumberOfFollowers = follower.NumberOfFollowers,
                    FollowingIds = _db.UserFollowers.Where(uf => uf.FollowerId == follower.Id).Select(uf => uf.FollowedId).ToList(),
                    Status = follower.Status,
                    Avatar = avatar?.FilePath,
                    Roles = roles.ToList(),
                    CreatedAt = follower.CreatedAt,
                    UpdatedAt = follower.UpdatedAt
                };

                return Ok(new ApiOkResponse(userVm));
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpPost("followers/unfollow")]
        //[ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.DELETE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostUnFollowUser([FromBody] FollowerCreateRequest request)
        {
            var follower = await _userManager.FindByIdAsync(request.FollowerId);
            if (follower == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowerId} is not existed."));

            var followed = await _userManager.FindByIdAsync(request.FollowedId);
            if (followed == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.FollowedId} is not existed."));

            var userFollower = await _db.UserFollowers.FindAsync(request.FollowerId, request.FollowedId);
            if (userFollower == null)
                return NotFound(new ApiNotFoundResponse($"User has not been followed before"));

            _db.UserFollowers.Remove(userFollower);
            var result = await _db.SaveChangesAsync();

            followed.NumberOfFollowers -= 1;
            follower.NumberOfFolloweds -= 1;

            await _userManager.UpdateAsync(follower);
            await _userManager.UpdateAsync(followed);
            await _db.SaveChangesAsync();

            if (result > 0)
            {
                var avatar = await _fileService.GetFileByFileIdAsync(follower.AvatarId);
                var roles = await _userManager.GetRolesAsync(follower);
                var userVm = new UserVm()
                {
                    Id = follower.Id,
                    UserName = follower.UserName,
                    Email = follower.Email,
                    PhoneNumber = follower.PhoneNumber,
                    Dob = follower.Dob,
                    FullName = follower.FullName,
                    Gender = follower.Gender,
                    Bio = follower.Bio,
                    NumberOfCreatedEvents = follower.NumberOfCreatedEvents,
                    NumberOfFavourites = follower.NumberOfFavourites,
                    NumberOfFolloweds = follower.NumberOfFolloweds,
                    NumberOfFollowers = follower.NumberOfFollowers,
                    FollowingIds = _db.UserFollowers.Where(uf => uf.FollowerId == follower.Id).Select(uf => uf.FollowedId).ToList(),
                    Status = follower.Status,
                    Avatar = avatar?.FilePath,
                    Roles = roles.ToList(),
                    CreatedAt = follower.CreatedAt,
                    UpdatedAt = follower.UpdatedAt
                };

                return Ok(new ApiOkResponse(userVm));
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        #endregion

        #region Payments
        [HttpGet("{userId}/payments")]
        //[ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.VIEW)]
        public async Task<IActionResult> GetPaymentsByUserId(string userId, [FromQuery] PaymentPaginationFilter filter)
        {
            var fileStorages = await _fileService.GetListFileStoragesAsync();

            var paymentMethods = (from _paymentMethod in _db.PaymentMethods.ToList()
                                  join _file in fileStorages
                                  on _paymentMethod.MethodLogoId equals _file.Id
                                  into joinedPaymentMethods
                                  from _joinedPaymentMethod in joinedPaymentMethods
                                  select new PaymentMethodVm
                                  {
                                      Id = _paymentMethod.Id,
                                      MethodLogo = _joinedPaymentMethod?.FilePath ?? "",
                                      MethodName = _paymentMethod.MethodName
                                  }).ToList();

            var methods = (from _userPaymentMethod in _db.UserPaymentMethods.ToList()
                           join _paymentMethod in paymentMethods
                           on _userPaymentMethod.MethodId equals _paymentMethod.Id
                           select new UserPaymentMethodVm
                           {
                               Id = _userPaymentMethod.Id,
                               PaymentAccountNumber = _userPaymentMethod.PaymentAccountNumber,
                               UserId = _userPaymentMethod.UserId,
                               MethodId = _paymentMethod.Id,
                               CheckoutContent = _userPaymentMethod.CheckoutContent,
                               Method = new PaymentMethodVm
                               {
                                   Id = _paymentMethod.Id,
                                   MethodName = _paymentMethod.MethodName,
                                   MethodLogo = _paymentMethod.MethodLogo
                               }
                           }).ToList();

            var events = (from _event in _db.Events.ToList()
                          join _file in fileStorages
                          on _event.CoverImageId equals _file.Id
                          into joinedEvents
                          from _joinedEvent in joinedEvents
                          select new
                          {
                              Id = _event.Id,
                              CoverImage = _joinedEvent?.FilePath ?? "",
                              Name = _event.Name,
                              CreatorId = _event.CreatorId,
                          }).ToList();

            var payments = (from _payment in _db.Payments.ToList()
                            join _event in events
                            on _payment.EventId equals _event.Id
                            join _method in methods
                            on _payment.UserPaymentMethodId equals _method.Id
                            select new PaymentVm
                            {
                                Id = _payment.Id,
                                EventId = _event.Id,
                                Event = new PaymentEventVm
                                {
                                    Id = _event.Id,
                                    CoverImage = _event.CoverImage,
                                    Name = _event.Name,
                                    CreatorId = _event.CreatorId
                                },
                                CustomerEmail = _payment.CustomerEmail,
                                CustomerPhone = _payment.CustomerPhone,
                                CustomerName = _payment.CustomerName,
                                Discount = _payment.Discount,
                                Status = _payment.Status,
                                TicketQuantity = _payment.TicketQuantity,
                                TotalPrice = _payment.TotalPrice,
                                UserId = _payment.UserId,
                                UserPaymentMethodId = _payment.UserPaymentMethodId,
                                PaymentMethod = _method,
                                CreatedAt = _payment.CreatedAt,
                                UpdatedAt = _payment.UpdatedAt,
                            })
                            .Where(p => p.UserId == userId)
                            .ToList();

            if (filter.search != null)
            {
                payments = payments.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
                                               c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
                                               c.CustomerPhone.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            payments = filter.order switch
            {
                PageOrder.ASC => payments.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => payments.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => payments
            };

            if (filter.status != PaymentStatus.ALL)
            {
                payments = payments.Where(p => p.Status.Equals(filter.status)).ToList();
            }

            var metadata = new Metadata(payments.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                payments = payments.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<PaymentVm>
            {
                Items = payments,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{creatorId}/payments/creator")]
        //[ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.VIEW)]
        public async Task<IActionResult> GetPaymentsByCreatorId(string creatorId, [FromQuery] PaymentPaginationFilter filter)
        {
            var fileStorages = await _fileService.GetListFileStoragesAsync();

            var paymentMethods = (from _paymentMethod in _db.PaymentMethods.ToList()
                                  join _file in fileStorages
                                  on _paymentMethod.MethodLogoId equals _file.Id
                                  into joinedPaymentMethods
                                  from _joinedPaymentMethod in joinedPaymentMethods
                                  select new PaymentMethodVm
                                  {
                                      Id = _paymentMethod.Id,
                                      MethodLogo = _joinedPaymentMethod?.FilePath ?? "",
                                      MethodName = _paymentMethod.MethodName
                                  }).ToList();

            var methods = (from _userPaymentMethod in _db.UserPaymentMethods.ToList()
                           join _paymentMethod in paymentMethods
                           on _userPaymentMethod.MethodId equals _paymentMethod.Id
                           select new UserPaymentMethodVm
                           {
                               Id = _userPaymentMethod.Id,
                               PaymentAccountNumber = _userPaymentMethod.PaymentAccountNumber,
                               UserId = _userPaymentMethod.UserId,
                               MethodId = _paymentMethod.Id,
                               CheckoutContent = _userPaymentMethod.CheckoutContent,
                               Method = new PaymentMethodVm
                               {
                                   Id = _paymentMethod.Id,
                                   MethodName = _paymentMethod.MethodName,
                                   MethodLogo = _paymentMethod.MethodLogo
                               }
                           }).ToList();

            var events = (from _event in _db.Events.ToList()
                          join _file in fileStorages
                          on _event.CoverImageId equals _file.Id
                          into joinedEvents
                          from _joinedEvent in joinedEvents
                          select new
                          {
                              Id = _event.Id,
                              CoverImage = _joinedEvent?.FilePath ?? "",
                              Name = _event.Name,
                              CreatorId = _event.CreatorId,
                          }).ToList();

            var payments = (from _payment in _db.Payments.ToList()
                            join _event in events
                            on _payment.EventId equals _event.Id
                            join _method in methods
                            on _payment.UserPaymentMethodId equals _method.Id
                            select new PaymentVm
                            {
                                Id = _payment.Id,
                                EventId = _event.Id,
                                Event = new PaymentEventVm
                                {
                                    Id = _event.Id,
                                    CoverImage = _event.CoverImage,
                                    Name = _event.Name,
                                    CreatorId = _event.CreatorId
                                },
                                CustomerEmail = _payment.CustomerEmail,
                                CustomerPhone = _payment.CustomerPhone,
                                CustomerName = _payment.CustomerName,
                                Discount = _payment.Discount,
                                Status = _payment.Status,
                                TicketQuantity = _payment.TicketQuantity,
                                TotalPrice = _payment.TotalPrice,
                                UserId = _payment.UserId,
                                UserPaymentMethodId = _payment.UserPaymentMethodId,
                                PaymentMethod = _method,
                                CreatedAt = _payment.CreatedAt,
                                UpdatedAt = _payment.UpdatedAt,
                            })
                            .Where(p => p.Event.CreatorId == creatorId)
                            .ToList();

            if (filter.search != null)
            {
                payments = payments.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
                                               c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
                                               c.CustomerPhone.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            payments = filter.order switch
            {
                PageOrder.ASC => payments.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => payments.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => payments
            };

            if (filter.status != PaymentStatus.ALL)
            {
                payments = payments.Where(p => p.Status.Equals(filter.status)).ToList();
            }

            var metadata = new Metadata(payments.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                payments = payments.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<PaymentVm>
            {
                Items = payments,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{userId}/payments/accounts")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.VIEW)]
        [ApiValidationFilter]
        public async Task<IActionResult> GetPaymentAccountsByUser(string userId,
            [FromQuery] PaginationFilter filter)
        {
            // Check cache data
            var paymentAccounts = new List<PaymentAccountVm>();
            var cachePaymentAccounts = _cacheService.GetData<IEnumerable<PaymentAccountVm>>($"{CacheKey.PAYMENTACCOUNTS}{userId}");
            if (cachePaymentAccounts != null && cachePaymentAccounts.Count() > 0)
                paymentAccounts = cachePaymentAccounts.ToList();
            else
            {
                var fileStorages = await _fileService.GetListFileStoragesAsync();

                var paymentMethods = (from _paymentMethod in _db.PaymentMethods.ToList()
                                      join _file in fileStorages
                                      on _paymentMethod.MethodLogoId equals _file.Id
                                      into joinedPaymentMethods
                                      from _joinedPaymentMethod in joinedPaymentMethods.DefaultIfEmpty()
                                      select new PaymentMethodVm
                                      {
                                          Id = _paymentMethod.Id,
                                          MethodLogo = _joinedPaymentMethod?.FilePath ?? "",
                                          MethodName = _paymentMethod.MethodName
                                      }).ToList();

                var userPaymentMethods = (from _userPaymentMethod in _db.UserPaymentMethods.ToList()
                                          join _file in fileStorages
                                          on _userPaymentMethod.PaymentAccountQRCodeId equals _file.Id
                                          into joinedUserPaymentMethods
                                          from _joinedUserPaymentMethod in joinedUserPaymentMethods.DefaultIfEmpty()
                                          select new UserPaymentMethodVm
                                          {
                                              Id = _userPaymentMethod.Id,
                                              CheckoutContent = _userPaymentMethod.CheckoutContent,
                                              MethodId = _userPaymentMethod.MethodId,
                                              PaymentAccountNumber = _userPaymentMethod.PaymentAccountNumber,
                                              UserId = _userPaymentMethod.UserId,
                                              PaymentAccountQRCode = _joinedUserPaymentMethod?.FilePath ?? ""
                                          }).ToList();

                paymentAccounts = (from _userPaymentMethod in userPaymentMethods
                                   join _paymentMethod in paymentMethods
                                   on _userPaymentMethod.MethodId equals _paymentMethod.Id
                                   select new PaymentAccountVm
                                   {
                                       Id = _userPaymentMethod.Id,
                                       UserId = userId,
                                       MethodId = _userPaymentMethod.MethodId,
                                       MethodLogo = _paymentMethod.MethodLogo,
                                       MethodName = _paymentMethod.MethodName,
                                       PaymentAccountNumber = _userPaymentMethod.PaymentAccountNumber,
                                       PaymentAccountQRCode = _userPaymentMethod.PaymentAccountQRCode,
                                       CheckoutContent = _userPaymentMethod.CheckoutContent,
                                   }).ToList();

                // Set expiry time
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<IEnumerable<PaymentAccountVm>>($"{CacheKey.PAYMENTACCOUNTS}{userId}", paymentAccounts, expiryTime);
            }


            if (!filter.search.IsNullOrEmpty())
            {
                paymentAccounts = paymentAccounts.Where(c =>
                    c.MethodName.ToLower().Contains(filter.search.ToLower()) ||
                    c.PaymentAccountNumber.ToLower().Contains(filter.search.ToLower()) ||
                    c.CheckoutContent.ToLower().Contains(filter.search.ToLower()))
                    .ToList();
            }

            paymentAccounts = filter.order switch
            {
                PageOrder.ASC => paymentAccounts.OrderBy(c => c.Id).ToList(),
                PageOrder.DESC => paymentAccounts.OrderByDescending(c => c.Id).ToList(),
                _ => paymentAccounts
            };

            var metadata = new Metadata(paymentAccounts.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                paymentAccounts = paymentAccounts.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<PaymentAccountVm>
            {
                Items = paymentAccounts,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpPost("{userId}/payments/accounts")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PostCreatePaymentAccount(string userId,
            [FromForm] CreatePaymentAccountRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not existed."));

            var method = await _db.PaymentMethods.FindAsync(request.MethodId);
            if (method == null)
                return NotFound(new ApiNotFoundResponse($"Method with id {request.MethodId} is not existed."));

            var paymentAccount = new UserPaymentMethod
            {
                Id = Guid.NewGuid().ToString(),
                MethodId = method.Id,
                CheckoutContent = request.CheckoutContent,
                PaymentAccountNumber = request.PaymentAccountNumber,
                UserId = userId,
            };

            if (request.PaymentAccountQRCode != null)
            {
                var paymentAccountQRCode = await _fileService.SaveFileToFileStorageAsync(request.PaymentAccountQRCode, FileContainer.PAYMENTS);
                paymentAccount.PaymentAccountQRCodeId = paymentAccountQRCode.Id;
            }

            await _db.UserPaymentMethods.AddAsync(paymentAccount);

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                _cacheService.RemoveData($"{CacheKey.PAYMENTACCOUNTS}{userId}");

                return CreatedAtAction(nameof(PostCreatePaymentAccount),
                    new { Id = paymentAccount.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse("Failed to create new payment account"));
            }
        }

        [HttpPut("{userId}/payments/accounts/{accountId}")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.UPDATE)]
        [ApiValidationFilter]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutUpdatePaymentAccount(string userId, string accountId,
            [FromForm] CreatePaymentAccountRequest request)
        {
            var account = await _db.UserPaymentMethods.FindAsync(accountId);
            if (account == null)
                return NotFound(new ApiNotFoundResponse($"UserPaymentMethod with id {accountId} is not existed."));

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} is not existed."));

            var method = await _db.PaymentMethods.FindAsync(request.MethodId);
            if (method == null)
                return NotFound(new ApiNotFoundResponse($"Method with id {request.MethodId} is not existed."));


            account.MethodId = method.Id;
            account.CheckoutContent = request.CheckoutContent;
            account.PaymentAccountNumber = request.PaymentAccountNumber;
            account.UserId = userId;

            await _fileService.DeleteFileByIdAsync(account.PaymentAccountQRCodeId);
            if (request.PaymentAccountQRCode != null)
            {
                var paymentAccountQRCode = await _fileService.SaveFileToFileStorageAsync(request.PaymentAccountQRCode, FileContainer.PAYMENTS);
                account.PaymentAccountQRCodeId = paymentAccountQRCode.Id;
            }

            _db.UserPaymentMethods.Update(account);

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                _cacheService.RemoveData($"{CacheKey.PAYMENTACCOUNTS}{userId}");
                return Ok(new ApiOkResponse(new { Id = account.Id }));
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse("Failed to update payment account"));
            }
        }

        [HttpDelete("{userId}/payments/accounts/{accountId}")]
        [ClaimRequirement(FunctionCode.CONTENT_EVENT, CommandCode.DELETE)]
        public async Task<IActionResult> DeletePaymentAccount(string userId, string accountId)
        {
            var account = await _db.UserPaymentMethods.FindAsync(accountId);
            if (account == null)
                return NotFound(new ApiNotFoundResponse($"UserPaymentMethod with id {accountId} is not existed."));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} is not existed."));

            if (user.Id != account.UserId)
                return BadRequest(new ApiBadRequestResponse($"User with id {userId} is not belong to the account."));

            await _fileService.DeleteFileByIdAsync(account.PaymentAccountQRCodeId);

            _db.UserPaymentMethods.Remove(account);

            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                _cacheService.RemoveData($"{CacheKey.PAYMENTACCOUNTS}{userId}");
                return Ok(new ApiOkResponse(new { Id = account.Id }));
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse("Failed to update payment account"));
            }
        }
        #endregion

        #region Tickets
        [HttpGet("{userId}/tickets")]
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.VIEW)]
        public async Task<IActionResult> GetTicketsByUserId(string userId, [FromQuery] PaginationFilter filter)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} does not exist!"));

            var tickets = _db.Tickets.Where(t => t.UserId == userId).ToList();

            if (!filter.search.IsNullOrEmpty())
            {
                tickets = tickets.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
                                             c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
                                             c.CustomerPhone.ToLower().Contains(filter.search.ToLower())
                ).ToList();
            }

            tickets = filter.order switch
            {
                PageOrder.ASC => tickets.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => tickets.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => tickets
            };

            var metadata = new Metadata(tickets.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                tickets = tickets.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var ticketVms = tickets.Select(t => new TicketVm()
            {
                Id = t.Id,
                EventId = t.EventId,
                CustomerName = t.CustomerName,
                CustomerPhone = t.CustomerPhone,
                CustomerEmail = t.CustomerEmail,
                Status = t.Status,
                PaymentId = t.PaymentId,
                TicketTypeId = t.TicketTypeId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            var pagination = new Pagination<TicketVm>
            {
                Items = ticketVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }
        #endregion
    }
}
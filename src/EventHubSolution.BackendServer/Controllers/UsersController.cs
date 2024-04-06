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
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Data;

namespace EventHubSolution.BackendServer.Controllers
{
    public class UsersController : BaseController
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;

        public UsersController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db, IFileStorageService fileStorage)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _fileStorage = fileStorage;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.CREATE)]
        [ApiValidationFilter]
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

            //TODO: Upload avatar image
            if (request.Avatar != null)
            {
                var avatarImage = await _fileStorage.SaveFileToFileStorage(request.Avatar);
                user.AvatarId = avatarImage.Id;
            }

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, request);
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
            var users = _userManager.Users.ToList();
            if (filter.search != null)
            {
                users = users.Where(u => u.FullName.ToLower().Contains(filter.search.ToLower()) || u.Email.ToLower().Contains(filter.search.ToLower()) || u.UserName.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            users = filter.order switch
            {
                PageOrder.ASC => users.OrderBy(u => u.CreatedAt).ToList(),
                PageOrder.DESC => users.OrderByDescending(u => u.CreatedAt).ToList(),
                _ => users
            };

            var metadata = new Metadata(users.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                users = users.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var userVms = (from u in users
                           join f in _db.FileStorages
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

            var pagination = new Pagination<UserVm>
            {
                Items = userVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new ApiNotFoundResponse(""));

            var avatar = await _db.FileStorages.FindAsync(user.AvatarId);

            var roles = await _userManager.GetRolesAsync(user);

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
                Avatar = avatar.FilePath,
                Roles = roles.ToList(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
            return Ok(userVm);
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_USER, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutUser(string id, [FromBody] UserCreateRequest request)
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

            //TODO: Upload avatar image
            if (request.Avatar != null)
            {
                var avatarImage = await _fileStorage.SaveFileToFileStorage(request.Avatar);
                user.AvatarId = avatarImage.Id;
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return NoContent();
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

                return Ok(userVm);
            }
            return BadRequest(new ApiBadRequestResponse(result));
        }

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
            return Ok(data);
        }

        [HttpGet("{userId}/reviews")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetReviewsByUserId(string userId, [FromQuery] PaginationFilter filter)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} is not existed."));

            var reviews = _db.Reviews.Where(r => r.UserId == userId).ToList();
            if (filter.search != null)
            {
                reviews = reviews.Where(c => c.Content.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            reviews = filter.order switch
            {
                PageOrder.ASC => reviews.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => reviews.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => reviews
            };

            var metadata = new Metadata(reviews.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                reviews = reviews.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var userAvatar = await _db.FileStorages.FindAsync(user.AvatarId);
            var reviewVms = reviews.Join(_db.Events, _review => _review.EventId, _event => _event.Id, (_review, _event) => new ReviewVm
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

            var pagination = new Pagination<ReviewVm>
            {
                Items = reviewVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

        [HttpGet("{userId}/events/favourites")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> GetFavoutireEventsByUserId(string userId, [FromQuery] EventPaginationFilter filter)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {userId} is not existed."));

            var eventDatas = _db.FavouriteEvents
                    .Join(_db.Events, _favouriteEvent => _favouriteEvent.EventId, _event => _event.Id, (_favouriteEvent, _event) => new
                    {
                        _favouriteEvent,
                        _event
                    })
                    .Where(joinedEvent => joinedEvent._favouriteEvent.UserId == userId)
                    .Select(joinedEvent => joinedEvent._event)
                    .ToList();

            var eventDataVms = eventDatas
                    .Join(_db.FileStorages, _event => _event.CoverImageId, _fileStorage => _fileStorage.Id, (_event, _fileStorage) => new EventVm
                    {
                        Id = _event.Id,
                        Name = _event.Name,
                        CreatorName = user.FullName,
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
                    .Join(_db.Locations, _eventVm => _eventVm.LocationId, _location => _location.Id, (_eventVm, _location) =>
                    {
                        _eventVm.LocationString = $"{_location.Street}, {_location.District}, {_location.City}";
                        return _eventVm;
                    })
                    .Join(_db.TicketTypes, _eventVm => _eventVm.LocationId, _ticketType => _ticketType.Id, (_eventVm, _ticketType) => new
                    {
                        _eventVm,
                        _ticketType
                    })
                    .GroupBy(joinedEventVm => joinedEventVm._eventVm)
                    .Select(groupedEventVm =>
                    {
                        var eventVm = groupedEventVm.Key;
                        eventVm.PriceRange = new PriceRangeVm
                        {
                            StartRange = groupedEventVm.Min(e => e._ticketType.Price),
                            EndRange = groupedEventVm.Max(e => e._ticketType.Price)
                        };
                        return eventVm;
                    })
                    .DistinctBy(e => e.Id)
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
                    .DistinctBy(e => e.Id)
                    .ToList();

            if (filter.search != null)
            {
                eventDataVms = eventDataVms.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            eventDataVms = filter.order switch
            {
                PageOrder.ASC => eventDataVms.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => eventDataVms.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => eventDataVms
            };

            switch (filter.type)
            {
                case EventType.OPENING:
                    eventDataVms = eventDataVms.Where(e => e.StartTime <= DateTime.UtcNow && DateTime.UtcNow <= e.EndTime).ToList();
                    break;
                case EventType.UPCOMING:
                    eventDataVms = eventDataVms.Where(e => DateTime.UtcNow < e.StartTime).ToList();
                    break;
                case EventType.CLOSED:
                    eventDataVms = eventDataVms.Where(e => e.EndTime < DateTime.UtcNow).ToList();
                    break;
            }

            if (!filter.location.IsNullOrEmpty())
            {
                eventDataVms = eventDataVms.Where(e => e.LocationString.Split(", ").Any(str => filter.location.ToLower().Contains(str.ToLower()))).ToList();
            }

            if (!filter.categoryIds.IsNullOrEmpty())
            {
                eventDataVms = eventDataVms.Where(e => e.Categories.Exists(c => filter.categoryIds.Contains(c.Id))).ToList();
            }

            if (filter.priceRange != null)
            {
                eventDataVms = eventDataVms.Where(e => filter.priceRange.StartRange <= e.PriceRange.StartRange && filter.priceRange.EndRange <= e.PriceRange.EndRange).ToList();
            }

            var metadata = new Metadata(eventDataVms.Count(), filter.page, filter.size, filter.takeAll);

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

        [HttpPost("followers/follow")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
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
                return CreatedAtAction(nameof(GetById), new { followerId = userFollower.FollowerId, followedId = userFollower.FollowedId }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpPost("followers/unfollow")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
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
                return Ok();
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }
    }
}

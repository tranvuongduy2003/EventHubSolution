using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            var userVms = users.Join(_db.FileStorages.DefaultIfEmpty(), u => u.AvatarId, f => f.Id, (u, f) => new UserVm
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
                Avatar = f.FilePath,
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
                return Ok();
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
    }
}

using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _db
            ;

        public MessagesController(IFileStorageService fileStorage, UserManager<User> userManager, ApplicationDbContext db)
        {
            _fileStorage = fileStorage;
            _userManager = userManager;
            _db = db;
        }

        [HttpGet]
        //[ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetMessages([FromQuery] PaginationFilter filter)
        {
            var fileStorages = await _fileStorage.GetListFileStoragesAsync();
            var messageVms = (from message in _db.Messages.ToList()
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
                              on message.UserId equals userItem.Id
                              orderby message.UpdatedAt ascending
                              select new MessageVm
                              {
                                  Id = message.Id,
                                  Content = message.Content,
                                  ConversationId = message.ConversationId,
                                  UserId = message.Id,
                                  User = new ConversationUserVm
                                  {
                                      Avatar = userItem.Avatar,
                                      FullName = userItem.FullName,
                                  },
                                  CreatedAt = message.CreatedAt,
                                  UpdatedAt = message.UpdatedAt
                              }).ToList();


            var metadata = new Metadata(messageVms.Count(), filter.page, filter.size, filter.takeAll);

            if (!filter.search.IsNullOrEmpty())
            {
                messageVms = messageVms.Where(m => m.Content.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            messageVms = filter.order switch
            {
                PageOrder.ASC => messageVms.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => messageVms.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => messageVms
            };

            if (filter.takeAll == false)
            {
                messageVms = messageVms.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var pagination = new Pagination<MessageVm>
            {
                Items = messageVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }
    }
}

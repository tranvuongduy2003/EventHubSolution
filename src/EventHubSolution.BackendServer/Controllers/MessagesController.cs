using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Hubs;
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
        private readonly ApplicationDbContext _db;
        private readonly ChatHub _chatHub;

        public MessagesController(IFileStorageService fileStorage, UserManager<User> userManager, ApplicationDbContext db, ChatHub chatHub)
        {
            _fileStorage = fileStorage;
            _userManager = userManager;
            _db = db;
            _chatHub = chatHub;
        }

        [HttpGet]
        //[ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetMessages([FromQuery] PaginationFilter filter)
        {
            var fileStorages = await _fileStorage.GetListFileStoragesAsync();
            var messageVms = (from _message in _db.Messages.ToList()
                              join _userItem in (from userEntity in _userManager.Users.ToList()
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
                              on _message.UserId equals _userItem.Id
                              join _video in fileStorages
                              on _message.VideoId equals _video.Id into joinedVideoMessages
                              from _joinedVideoMessage in joinedVideoMessages.DefaultIfEmpty()
                              join _image in fileStorages
                              on _message.ImageId equals _image.Id into joinedImageMessages
                              from _joinedImageMessage in joinedImageMessages.DefaultIfEmpty()
                              join _audio in fileStorages
                              on _message.AudioId equals _audio.Id into joinedAudioMessages
                              from _joinedAudioMessage in joinedAudioMessages.DefaultIfEmpty()
                              select new MessageVm
                              {
                                  Id = _message.Id,
                                  Content = _message.Content,
                                  ConversationId = _message.ConversationId,
                                  UserId = _message.Id,
                                  User = new ConversationUserVm
                                  {
                                      Avatar = _userItem.Avatar,
                                      FullName = _userItem.FullName,
                                  },
                                  Video = _joinedVideoMessage?.FilePath,
                                  Image = _joinedImageMessage?.FilePath,
                                  Audio = _joinedAudioMessage?.FilePath,
                                  CreatedAt = _message.CreatedAt,
                                  UpdatedAt = _message.UpdatedAt
                              }).ToList();



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

            var metadata = new Metadata(messageVms.Count(), filter.page, filter.size, filter.takeAll);

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

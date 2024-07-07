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
    public class ConversationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IFileStorageService _fileStorage;

        public ConversationsController(ApplicationDbContext db, UserManager<User> userManager, IFileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
        }

        [HttpGet]
        //[ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetConversations([FromQuery] PaginationFilter filter)
        {
            var fileStorages = await _fileStorage.GetListFileStoragesAsync();
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

            if (filter.takeAll == false)
            {
                conversationVms = conversationVms.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var metadata = new Metadata(conversationVms.Count(), filter.page, filter.size, filter.takeAll);

            var pagination = new Pagination<ConversationVm>
            {
                Items = conversationVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{conversationId}/messages")]
        //[ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.VIEW)]
        public async Task<IActionResult> GetMessagesByConversation(string conversationId, [FromQuery] PaginationFilter filter)
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
                              where _message.ConversationId == conversationId
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

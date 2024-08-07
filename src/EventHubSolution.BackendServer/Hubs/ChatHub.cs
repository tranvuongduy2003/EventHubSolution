﻿using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.WebSockets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalRSwaggerGen.Attributes;
using ILogger = Serilog.ILogger;

namespace EventHubSolution.BackendServer.Hubs
{
    [SignalRHub]
    public class ChatHub : Hub
    {
        private readonly static List<string> _connections = new List<string>();

        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger _logger;

        public ChatHub(ILogger logger, ApplicationDbContext db, UserManager<User> userManager, IFileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task TestConnection()
        {
            _logger.Information("Invoke Test Connection");
            await Clients.All.SendAsync("TestConnection", "Connect successfully!");
        }

        //[ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.CREATE)]
        public async Task JoinChatRoom(JoinChatRoomRequest request)
        {
            var eventData = await _db.Events.FindAsync(request.EventId);
            if (eventData == null)
                throw new Exception($"Event with id {request.EventId} does not exist");

            if (eventData.CreatorId != request.HostId)
                throw new Exception($"Host with id {request.HostId} does not belong to event with id {request.EventId} does not exist");

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                throw new Exception($"User with id {request.UserId} does not exist");

            var host = await _userManager.FindByIdAsync(request.HostId);
            if (host == null)
                throw new Exception($"Host with id {request.HostId} does not exist");

            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.HostId == request.HostId && c.EventId == request.EventId && c.UserId == request.UserId);

            if (conversation == null)
            {
                var newConversation = new Conversation
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = request.EventId,
                    UserId = request.UserId,
                    HostId = request.HostId,
                };

                await _db.Conversations.AddAsync(newConversation);
                await _db.SaveChangesAsync();


                var eventCoverImage = await _fileStorage.GetFileByFileIdAsync(eventData.CoverImageId);
                var userAvatarImage = await _fileStorage.GetFileByFileIdAsync(user.AvatarId);
                var hostAvatarImage = await _fileStorage.GetFileByFileIdAsync(host.AvatarId);

                var conversationVm = new ConversationVm
                {
                    Id = newConversation.Id,
                    EventId = newConversation.EventId,
                    Event = new ConversationEventVm
                    {
                        CoverImage = eventCoverImage?.FilePath ?? "",
                        Name = eventData.Name
                    },
                    HostId = newConversation.HostId,
                    Host = new ConversationUserVm
                    {
                        Avatar = hostAvatarImage?.FilePath ?? "",
                        FullName = host.FullName
                    },
                    UserId = newConversation.UserId,
                    User = new ConversationUserVm
                    {
                        Avatar = userAvatarImage?.FilePath ?? "",
                        FullName = user.FullName
                    },
                    CreatedAt = newConversation.CreatedAt,
                    UpdatedAt = newConversation.UpdatedAt,
                };

                _logger.Information($"JoinChatRoom: Created Context Connection id: {Context.ConnectionId}");
                await Groups.AddToGroupAsync(Context.ConnectionId, newConversation.Id);
                _connections.Add(newConversation.Id);
                await Clients.Group(newConversation.Id).SendAsync("JoinChatRoom", conversationVm, $"{user.FullName} has created  conversation {newConversation.Id}");
            }
            else
            {
                var eventCoverImage = await _fileStorage.GetFileByFileIdAsync(eventData.CoverImageId);
                var userAvatarImage = await _fileStorage.GetFileByFileIdAsync(user.AvatarId);
                var hostAvatarImage = await _fileStorage.GetFileByFileIdAsync(host.AvatarId);

                var conversationVm = new ConversationVm
                {
                    Id = conversation.Id,
                    EventId = conversation.EventId,
                    Event = new ConversationEventVm
                    {
                        CoverImage = eventCoverImage?.FilePath ?? "",
                        Name = eventData.Name
                    },
                    HostId = conversation.HostId,
                    Host = new ConversationUserVm
                    {
                        Avatar = hostAvatarImage?.FilePath ?? "",
                        FullName = host.FullName
                    },
                    UserId = conversation.UserId,
                    User = new ConversationUserVm
                    {
                        Avatar = userAvatarImage?.FilePath ?? "",
                        FullName = user.FullName
                    },
                    CreatedAt = conversation.CreatedAt,
                    UpdatedAt = conversation.UpdatedAt,
                };

                _logger.Information($"JoinChatRoom: Joined Context Connection id: {Context.ConnectionId}");
                if (!_connections.Contains(conversation.Id))
                    await Groups.AddToGroupAsync(Context.ConnectionId, conversation.Id);
                await Clients.Group(conversation.Id).SendAsync("JoinChatRoom", conversationVm, $"{user.FullName} has joined  conversation {conversation.Id}");
            }
        }

        //[ClaimRequirement(FunctionCode.CONTENT_CHAT, CommandCode.CREATE)]
        public async Task SendMessage(SendMessageRequest request)
        {
            _logger.Information($"BEGIN: SendMessage: ${request}");
            var conversation = await _db.Conversations.FindAsync(request.ConversationId);
            if (conversation == null)
                throw new Exception($"Conversation with id {request.ConversationId} does not exist");

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                throw new Exception($"User with id {request.UserId} does not exist");

            var message = new Message
            {
                Id = Guid.NewGuid().ToString(),
                UserId = request.UserId,
                ConversationId = request.ConversationId,
                Content = request.Content,
                EventId = conversation.EventId,
                VideoId = request.VideoId,
                ImageId = request.ImageId,
                AudioId = request.AudioId,
            };

            var messageVm = new MessageVm
            {
                Id = message.Id,
                UserId = message.UserId,
                Content = message.Content,
                ConversationId = message.ConversationId,
                Video = request.VideoUrl,
                Image = request.ImageUrl,
                Audio = request.AudioUrl,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
            };

            await _db.Messages.AddAsync(message);
            await _db.SaveChangesAsync();

            _logger.Information($"SendMessage: Context Connection id: {Context.ConnectionId}");
            if (!_connections.Contains(conversation.Id))
                await Groups.AddToGroupAsync(Context.ConnectionId, conversation.Id);
            await Clients.Group(request.ConversationId).SendAsync("ReceiveMessage", messageVm);
        }
    }
}

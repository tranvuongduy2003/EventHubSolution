using Microsoft.AspNetCore.SignalR;

namespace EventHubSolution.BackendServer.Hubs
{
    public class ChatHub : Hub
    {
        //private readonly ApplicationDbContext _db;

        //public ChatHub(ApplicationDbContext db)
        //{
        //    _db = db;
        //}

        //public async Task JoinChat(UserConnection conn)
        //{
        //    await Clients.All.SendAsync("ReceiveMessage", conn.UserName, $"{conn.UserName} has joined {conn.ChatRoom}");
        //}

        //public async Task JoinSpecificChatRoom(UserConnection conn)
        //{
        //    await Groups.AddToGroupAsync(Context.ConnectionId, conn.ChatRoom);

        //    _db.connections[Context.ConnectionId] = conn;

        //    await Clients.Group(conn.ChatRoom).SendAsync("ReceiveMessage", conn.UserName, $"{conn.UserName} has joined {conn.ChatRoom}");
        //}

        //public async Task SendMessage(string msg)
        //{
        //    if (_shared.connection.TryGetValue(Context.ConnectionId, out UserConnection conn))
        //    {
        //        await Clients.Group(conn.ChatRoom).SendAsync("ReceiveSpecificMessage", conn.UserName, msg);
        //    }
        //}
    }
}

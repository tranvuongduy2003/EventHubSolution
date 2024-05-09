namespace EventHubSolution.ViewModels.WebSockets
{
    public class JoinChatRoomRequest
    {
        public string EventId { get; set; }

        public string HostId { get; set; }

        public string UserId { get; set; }
    }
}

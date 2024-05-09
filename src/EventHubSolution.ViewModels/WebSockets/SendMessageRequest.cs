namespace EventHubSolution.ViewModels.WebSockets
{
    public class SendMessageRequest
    {
        public string UserId { get; set; }
        public string ConversationId { get; set; }
        public string Content { get; set; }
    }
}

namespace EventHubSolution.ViewModels.Contents
{
    public class ConversationVm
    {
        public string Id { get; set; }

        public string EventId { get; set; }

        public ConversationEventVm Event { get; set; }

        public string HostId { get; set; }

        public ConversationUserVm Host { get; set; }

        public string UserId { get; set; }

        public ConversationUserVm User { get; set; }

        public ConversationLastMessageVm? LastMessage { get; set; } = null;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

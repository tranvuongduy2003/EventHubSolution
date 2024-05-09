namespace EventHubSolution.ViewModels.Contents
{
    public class MessageVm
    {
        public string Id { get; set; }

        public string? Content { get; set; }

        public string? Image { get; set; }

        public string? Video { get; set; }

        public string UserId { get; set; }

        public ConversationUserVm User { get; set; }

        public string ConversationId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

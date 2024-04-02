namespace EventHubSolution.ViewModels.Contents
{
    public class ReviewVm
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public string UserName { get; set; }

        public string UserAvatar { get; set; }

        public string EventId { get; set; }

        public string EventName { get; set; }

        public string? Content { get; set; }

        public double Rate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}

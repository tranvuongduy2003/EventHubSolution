namespace EventHubSolution.ViewModels.Contents
{
    public class ReviewCreateRequest
    {
        public string UserId { get; set; }

        public string EventId { get; set; }

        public string? Content { get; set; }

        public double Rate { get; set; }
    }
}

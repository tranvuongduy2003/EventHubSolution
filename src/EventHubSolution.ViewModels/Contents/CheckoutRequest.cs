namespace EventHubSolution.ViewModels.Contents
{
    public class CheckoutRequest
    {
        public string FullName { get; set; }

        public string PhoneNumber { get; set; }

        public string Email { get; set; }

        public string EventId { get; set; }

        public string UserId { get; set; }

        public List<CheckoutItemVm> Items { get; set; } = new();
    }
}

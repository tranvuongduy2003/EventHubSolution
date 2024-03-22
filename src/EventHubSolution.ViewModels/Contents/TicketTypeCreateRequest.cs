namespace EventHubSolution.ViewModels.Contents
{
    public class TicketTypeCreateRequest
    {
        public string EventId { get; set; }

        public string Name { get; set; }

        public int Quantity { get; set; } = 0;

        public decimal Price { get; set; }
    }
}

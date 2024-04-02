namespace EventHubSolution.ViewModels.Contents
{
    public class TicketTypeVm
    {
        public string Id { get; set; }

        public string EventId { get; set; }

        public string Name { get; set; }

        public int Quantity { get; set; } = 0;

        public int? NumberOfSoldTickets { get; set; } = 0;

        public long Price { get; set; }
    }
}

using EventHubSolution.ViewModels.Constants;

namespace EventHubSolution.ViewModels.Contents
{
    public class EventDetailVm
    {
        public string Id { get; set; }

        public string CreatorId { get; set; }

        public CreatorVm Creator { get; set; }

        public string CoverImage { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string LocationId { get; set; }

        public LocationVm Location { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public List<CategoryVm> Categories { get; set; }

        public double Promotion { get; set; } = 0;

        public EventCycleType EventCycleType { get; set; }

        public EventPaymentType EventPaymentType { get; set; }

        public List<TicketTypeVm> TicketTypes { get; set; }

        public EmailContentVm EmailContent { get; set; }

        public int? NumberOfFavourites { get; set; } = 0;

        public int? NumberOfShares { get; set; } = 0;

        public int? NumberOfSoldTickets { get; set; } = 0;

        public EventStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}

using EventHubSolution.ViewModels.Constants;

namespace EventHubSolution.ViewModels.Contents
{
    public class EventVm
    {
        public string Id { get; set; }

        public string CreatorId { get; set; }

        public string CreatorName { get; set; }

        public string CoverImageId { get; set; }

        public string CoverImage { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string LocationId { get; set; }

        public string LocationString { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public List<CategoryVm> Categories { get; set; }

        public double Promotion { get; set; } = 0;

        public int? NumberOfFavourites { get; set; } = 0;

        public int? NumberOfShares { get; set; } = 0;

        public int? NumberOfSoldTickets { get; set; } = 0;

        public EventStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}

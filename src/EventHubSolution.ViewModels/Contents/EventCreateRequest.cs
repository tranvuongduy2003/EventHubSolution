using Microsoft.AspNetCore.Http;

namespace EventHubSolution.ViewModels.Contents
{
    public class EventCreateRequest
    {
        public string CreatorId { get; set; }

        public IFormFile CoverImage { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public LocationCreateRequest Location { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public List<string> CategoryIds { get; set; }

        public double Promotion { get; set; } = 0;

        public List<TicketTypeCreateRequest> TicketTypes { get; set; }

        public EmailContentCreateRequest EmailContent { get; set; }
    }
}

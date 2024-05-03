using EventHubSolution.ViewModels.Constants;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Contents
{
    public class EventCreateRequest
    {
        public string CreatorId { get; set; }

        public IFormFile CoverImage { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Location { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public List<string> CategoryIds { get; set; } = new List<string>();

        public double? Promotion { get; set; } = 0;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventCycleType EventCycleType { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventPaymentType EventPaymentType { get; set; }

        public bool IsPrivate { get; set; }

        public List<TicketTypeCreateRequest>? TicketTypes { get; set; } = new List<TicketTypeCreateRequest>();

        public EmailContentCreateRequest? EmailContent { get; set; }
    }
}

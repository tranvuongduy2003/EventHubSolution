using EventHubSolution.ViewModels.Constants;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Contents;

public class TicketCreateRequest
{
    public string CustomerName { get; set; }

    public string CustomerPhone { get; set; }

    public string CustomerEmail { get; set; }

    public string TicketTypeId { get; set; }

    public string EventId { get; set; }

    public string PaymentId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TicketStatus Status { get; set; }
}
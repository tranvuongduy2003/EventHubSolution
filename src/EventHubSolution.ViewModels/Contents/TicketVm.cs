using System.Text.Json.Serialization;
using EventHubSolution.ViewModels.Constants;

namespace EventHubSolution.ViewModels.Contents;

public class TicketVm
{
    public string Id { get; set; }
    
    public string CustomerName { get; set; }
    
    public string CustomerPhone { get; set; }
    
    public string CustomerEmail { get; set; }
    
    public string TicketTypeId { get; set; }
    
    public string EventId { get; set; }
    
    public string PaymentId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TicketStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
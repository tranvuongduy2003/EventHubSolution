using EventHubSolution.ViewModels.Constants;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Contents;

public class PaymentVm
{
    public string Id { get; set; }

    public string EventId { get; set; }

    public int TicketQuantity { get; set; } = 0;

    public string UserId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerPhone { get; set; }

    public string CustomerEmail { get; set; }

    public decimal TotalPrice { get; set; } = 0;

    public double Discount { get; set; } = 0;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStatus Status { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; }

    public string PaymentIntentId { get; set; }

    public string PaymentSessionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
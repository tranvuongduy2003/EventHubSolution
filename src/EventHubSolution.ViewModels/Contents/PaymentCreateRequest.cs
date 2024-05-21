using EventHubSolution.ViewModels.Constants;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Contents;

public class PaymentCreateRequest
{
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

    public string UserPaymentMethodId { get; set; }

    public string PaymentSessionId { get; set; }
}
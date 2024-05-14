using EventHubSolution.ViewModels.Contents;

namespace EventHubSolution.ViewModels.Stripe
{
    public class CreateStripeSessionRequest
    {
        public string ApprovedUrl { get; set; }
        public string CancelUrl { get; set; }
        public List<PaymentItemVm> Items { get; set; }
    }
}

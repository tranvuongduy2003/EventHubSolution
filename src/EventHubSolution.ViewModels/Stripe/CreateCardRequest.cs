using Stripe;

namespace EventHubSolution.ViewModels.Stripe
{
    public class CreateCardRequest
    {
        public string AccountId { get; set; }
        public CardCreateNestedOptions Options { get; set; }
    }
}

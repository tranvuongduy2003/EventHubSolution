using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.ViewModels.Stripe;
using Stripe;
using Stripe.Checkout;

namespace EventHubSolution.BackendServer.Services
{
    public class StripeService
    {
        private readonly string _apiKey;
        private readonly AccountService _accountService;
        private readonly ExternalAccountService _externalAccountService;
        private readonly SessionService _sessionService;
        private readonly PaymentIntentService _paymentIntentService;
        private readonly PaymentMethodService _paymentMethodService;
        private readonly CardService _cardService;

        public StripeService()
        {
            _apiKey = StripeConfiguration.ApiKey;
            _accountService = new AccountService();
            _externalAccountService = new ExternalAccountService();
            _sessionService = new SessionService();
            _paymentIntentService = new PaymentIntentService();
            _paymentMethodService = new PaymentMethodService();
            _cardService = new CardService();
        }

        public async Task<Account> CreateStripeAccount(User user)
        {
            var options = new AccountCreateOptions
            {
                Country = "US",
                Capabilities = new AccountCapabilitiesOptions
                {
                    BankTransferPayments = new AccountCapabilitiesBankTransferPaymentsOptions()
                    {
                        Requested = true
                    },
                    CardPayments = new AccountCapabilitiesCardPaymentsOptions
                    {
                        Requested = true
                    },
                    Transfers = new AccountCapabilitiesTransfersOptions()
                    {
                        Requested = true
                    }
                },
                BusinessType = "individual",
                Email = user.Email,
                Controller = new AccountControllerOptions
                {
                    RequirementCollection = "application",
                    Fees = new AccountControllerFeesOptions { Payer = "application" },
                    Losses = new AccountControllerLossesOptions { Payments = "application" },
                    StripeDashboard = new AccountControllerStripeDashboardOptions
                    {
                        Type = "none",
                    },
                },
            };
            var account = await _accountService.CreateAsync(options);
            return account;
        }

        public async Task<BankAccount> CreateStripeExternalBankAccount(CreateExternalBankAccountRequest request)
        {
            var options = new ExternalAccountCreateOptions
            {
                ExternalAccount = request.BankAccountOptions,
            };
            var bankAccount = await _externalAccountService.CreateAsync(request.AccountId, options);

            return (BankAccount)bankAccount;
        }

        public async Task<Card> CreateStripeCard(string accountId)
        {
            var options = new ExternalAccountCreateOptions
            {
                ExternalAccount = "tok_visa_debit",
            };

            var card = await _externalAccountService.CreateAsync(accountId, options);

            return (Card)card;
        }

        public async Task<Session> CreateStripeSession(CreateStripeSessionRequest request)
        {
            var options = new SessionCreateOptions
            {
                SuccessUrl = request.ApprovedUrl,
                CancelUrl = request.CancelUrl,
                LineItems = request.Items.Select(item => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = item.TotalPrice,
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"{item.EventName} - {item.TicketTypeName}",
                        },
                    },
                    Quantity = item.Quantity
                }).ToList(),
                Mode = "payment",
            };

            var session = await _sessionService.CreateAsync(options);
            return session;
        }

        public async Task<Account> GetAccount(string accountId) => await _accountService.GetAsync(accountId);

        public async Task<IExternalAccount> GetExternalAccount(string accountId, string bankAccountId) => await _externalAccountService.GetAsync(accountId, bankAccountId);

        public async Task<IExternalAccount> GetBankCard(string accountId, string cardId) => await _externalAccountService.GetAsync(accountId, cardId);

        public async Task<Session> GetSession(string sessionId) => await _sessionService.GetAsync(sessionId);

        public async Task<PaymentIntent> GetPaymentIntent(string paymentIntentId) => await _paymentIntentService.GetAsync(paymentIntentId);

        public async Task<PaymentMethod> GetPaymentMethod(string paymentMethodId) => await _paymentMethodService.GetAsync(paymentMethodId);
    }
}

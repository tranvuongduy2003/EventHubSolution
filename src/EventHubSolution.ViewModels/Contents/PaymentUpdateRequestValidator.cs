using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class PaymentUpdateRequestValidator : AbstractValidator<PaymentUpdateRequest>
    {
        public PaymentUpdateRequestValidator()
        {
            RuleFor(x => x.CustomerName).NotEmpty().WithMessage("CustomerName is required")
                .MaximumLength(100).WithMessage("CustomerName cannot over limit 100 characters");

            RuleFor(x => x.CustomerPhone).NotEmpty().WithMessage("CustomerPhone is required")
                .MaximumLength(100).WithMessage("CustomerPhone cannot over limit 100 characters");

            RuleFor(x => x.CustomerEmail).NotEmpty().WithMessage("CustomerEmail is required")
                .EmailAddress().WithMessage("CustomerEmail is invalid format")
                .MaximumLength(100).WithMessage("CustomerEmail cannot over limit 100 characters");
        }
    }
}

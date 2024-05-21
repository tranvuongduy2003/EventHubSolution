using EventHubSolution.ViewModels.Constants;
using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class UpdatePaymentStatusRequestValidator : AbstractValidator<UpdatePaymentStatusRequest>
    {
        public UpdatePaymentStatusRequestValidator()
        {
            RuleFor(x => x.Status).NotEmpty().WithMessage("Status is required")
                .Must(status => status == PaymentStatus.PAID || status == PaymentStatus.PENDING || status == PaymentStatus.FAILED || status == PaymentStatus.REJECTED).WithMessage("Status is invalid");
        }
    }
}

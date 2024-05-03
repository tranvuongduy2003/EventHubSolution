using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class EmailContentCreateRequestValidator : AbstractValidator<EmailContentCreateRequest>
    {
        public EmailContentCreateRequestValidator()
        {
            RuleFor(x => x.Content).NotEmpty().WithMessage("Email content is required")
                .MaximumLength(5000).WithMessage("Email content cannot over 5000 characters limit");
        }
    }
}

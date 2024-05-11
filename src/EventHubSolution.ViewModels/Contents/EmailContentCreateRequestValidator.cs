using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class EmailContentCreateRequestValidator : AbstractValidator<EmailContentCreateRequest>
    {
        public EmailContentCreateRequestValidator()
        {
            RuleFor(x => x.Content).NotEmpty().WithMessage("Email content is required")
                .MaximumLength(4000).WithMessage("Email content cannot over 4000 characters limit");
        }
    }
}

using FluentValidation;

namespace EventHubSolution.ViewModels.Systems
{
    public class UserValidateRequestValidator : AbstractValidator<UserValidateRequest>
    {
        public UserValidateRequestValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().WithMessage("User name is required");

            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email format is not match");

            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Phone number is required");
        }
    }
}

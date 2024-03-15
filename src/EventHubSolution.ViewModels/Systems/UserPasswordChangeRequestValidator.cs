using FluentValidation;

namespace EventHubSolution.ViewModels.Systems
{
    public class UserPasswordChangeRequestValidator : AbstractValidator<UserPasswordChangeRequest>
    {
        public UserPasswordChangeRequestValidator()
        {
            RuleFor(x => x.OldPassword).NotEmpty().WithMessage("Old password is required");

            RuleFor(x => x.NewPassword).NotEmpty().WithMessage("New password is required")
                .MinimumLength(8).WithMessage("Password has to at least 8 characters")
                .Matches(@"^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{8,}$")
                .WithMessage("Password is not match complexity rules."); ;
        }
    }
}

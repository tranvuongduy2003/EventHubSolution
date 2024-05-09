using FluentValidation;

namespace EventHubSolution.ViewModels.WebSockets
{
    public class JoinChatRoomRequestValidator : AbstractValidator<JoinChatRoomRequest>
    {
        public JoinChatRoomRequestValidator()
        {
            RuleFor(x => x.EventId).NotEmpty().WithMessage("EventId is required")
                .MaximumLength(50).WithMessage("EventId cannot over limit 50 characters");

            RuleFor(x => x.HostId).NotEmpty().WithMessage("HostId is required")
                .MaximumLength(50).WithMessage("HostId cannot over limit 50 characters");

            RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required")
                .MaximumLength(50).WithMessage("UserId cannot over limit 50 characters");
        }
    }
}

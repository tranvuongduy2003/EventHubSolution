using FluentValidation;

namespace EventHubSolution.ViewModels.WebSockets
{
    public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
    {
        public SendMessageRequestValidator()
        {
            RuleFor(x => x.ConversationId).NotEmpty().WithMessage("ConversationId is required")
                   .MaximumLength(50).WithMessage("ConversationId cannot over limit 50 characters");

            RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required")
                    .MaximumLength(50).WithMessage("UserId cannot over limit 50 characters");

            RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required")
                    .MaximumLength(1000).WithMessage("Content cannot over limit 1000 characters");
        }
    }
}

using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class FollowerCreateRequestValidator : AbstractValidator<FollowerCreateRequest>
    {
        public FollowerCreateRequestValidator()
        {
            RuleFor(x => x.FollowerId).NotEmpty().WithMessage("Follower's id is required")
                .MaximumLength(50).WithMessage("Follower's id cannot over limit 50 characters");

            RuleFor(x => x.FollowedId).NotEmpty().WithMessage("Followed's id is required")
                .MaximumLength(50).WithMessage("Followed's id cannot over limit 50 characters");
        }
    }
}

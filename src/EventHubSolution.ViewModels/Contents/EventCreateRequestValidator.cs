using EventHubSolution.ViewModels.Constants;
using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class EventCreateRequestValidator : AbstractValidator<EventCreateRequest>
    {
        public EventCreateRequestValidator()
        {
            RuleFor(x => x.CreatorId).NotEmpty().WithMessage("CreatorId is required")
                .MaximumLength(50).WithMessage("CreatorId cannot over limit 50 characters");

            RuleFor(x => x.CoverImage).NotNull().WithMessage("Cover image is required");

            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required")
                .MaximumLength(100).WithMessage("Name cannot over limit 100 characters");

            RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required")
                .MaximumLength(1000).WithMessage("Description cannot over limit 1000 characters");

            RuleFor(x => x.Location).NotNull().WithMessage("Location is required")
                .SetValidator(new LocationCreateRequestValidator());

            RuleFor(x => x.StartTime).NotEmpty().WithMessage("Start time is required")
                .LessThan(x => x.EndTime)
                .When(x => x.EndTime != null)
                .WithMessage("Start time must be less than end time"); ;

            RuleFor(x => x.EndTime).NotEmpty().WithMessage("End time is required")
                .GreaterThan(x => x.StartTime)
                .When(x => x.StartTime != null)
                .WithMessage("End time must be greater than start time");

            RuleFor(x => x.Promotion)
                .InclusiveBetween(0.0, 1.0)
                .When(x => x.EmailContent != null)
                .WithMessage("Promotion must be in range from 0.0 to 1.0");

            RuleFor(x => x.CategoryIds).NotNull().WithMessage("CategoryIds is required");
            RuleForEach(x => x.CategoryIds).NotNull().WithMessage("Category's id is required")
                .MaximumLength(50).WithMessage("Category's id cannot over limit 50 characters");

            RuleFor(x => x.EventPaymentType).NotNull().WithMessage("EventPaymentType is required");

            RuleFor(x => x.TicketTypes)
                .NotNull()
                .When(x => x.EventPaymentType == EventPaymentType.PAID)
                .WithMessage("TicketTypes is required");
            RuleForEach(x => x.TicketTypes).NotNull().WithMessage("Ticket Type is required")
                .SetValidator(new TicketTypeCreateRequestValidator());

            RuleFor(x => x.IsPrivate).NotNull().WithMessage("IsPrivate is required");

            RuleFor(x => x.EventCycleType).NotNull().WithMessage("EventCycleType is required");

            RuleFor(x => x.EmailContent).SetValidator(new EmailContentCreateRequestValidator())
                .When(x => x.EmailContent != null);
        }
    }
}

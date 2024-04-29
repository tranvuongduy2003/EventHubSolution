using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class LocationCreateRequestValidator : AbstractValidator<LocationCreateRequest>
    {
        public LocationCreateRequestValidator()
        {
            RuleFor(x => x.City).NotEmpty().WithMessage("City is required")
                .MaximumLength(50).WithMessage("City cannot over limit 50 characters");

            RuleFor(x => x.District).NotEmpty().WithMessage("District is required")
                .MaximumLength(50).WithMessage("District cannot over limit 50 characters");

            RuleFor(x => x.Street).NotEmpty().WithMessage("Street is required")
                .MaximumLength(100).WithMessage("Street cannot over limit 100 characters");
        }
    }
}

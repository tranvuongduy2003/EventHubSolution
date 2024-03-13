using FluentValidation;

namespace EventHubSolution.ViewModels.Contents
{
    public class CategoryCreateRequestValidator : AbstractValidator<CategoryCreateRequest>
    {
        public CategoryCreateRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required")
                .MaximumLength(100).WithMessage("Name cannot over limit 100 characters");

            RuleFor(x => x.IconImage).NotEmpty().WithMessage("Icon image is required");

            RuleFor(x => x.Color).NotEmpty().WithMessage("Color is required")
                .MaximumLength(50).WithMessage("Color cannot over limit 50 characters");
        }
    }
}

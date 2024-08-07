﻿using FluentValidation;

namespace EventHubSolution.ViewModels.Systems
{
    public class UserValidateRequestValidator : AbstractValidator<UserValidateRequest>
    {
        public UserValidateRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().WithMessage("Full name is required")
                .MaximumLength(50).WithMessage("Full name cannot over 50 characters limit");

            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email format is not match");

            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Phone number is required");
        }
    }
}

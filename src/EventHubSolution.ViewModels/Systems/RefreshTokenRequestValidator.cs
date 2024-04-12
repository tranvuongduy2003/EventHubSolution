using FluentValidation;

namespace EventHubSolution.ViewModels.Systems;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        
    }
}
namespace EventHubSolution.ViewModels.Systems
{
    public class UserResetPasswordRequest
    {
        public string NewPassword { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
    }
}

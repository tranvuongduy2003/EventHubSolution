namespace EventHubSolution.ViewModels.Systems
{
    public class UserPasswordChangeRequest
    {
        public string UserId { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
}

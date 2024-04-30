namespace EventHubSolution.ViewModels.Systems
{
    public class UserValidateRequest
    {
        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string? UserName { get; set; }
    }
}

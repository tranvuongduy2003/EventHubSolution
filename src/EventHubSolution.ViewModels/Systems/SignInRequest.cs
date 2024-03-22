namespace EventHubSolution.ViewModels.Systems
{
    public class SignInRequest
    {
        //PhoneNumber or Email
        public string Identity { get; set; }
        public string Password { get; set; }
    }
}

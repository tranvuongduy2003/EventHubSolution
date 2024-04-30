using System.ComponentModel;

namespace EventHubSolution.ViewModels.Systems
{
    public class SignInRequest
    {
        //PhoneNumber or Email
        [DefaultValue("admin@gmail.com")]
        public string Identity { get; set; }

        [DefaultValue("Admin@123")]
        public string Password { get; set; }
    }
}

using EventHubSolution.ViewModels.Constants;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Systems
{
    public class UserCreateRequest
    {

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public DateTime? Dob { get; set; }

        public string FullName { get; set; }

        public string Password { get; set; }

        public string? UserName { get; set; }


        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Gender? Gender { get; set; }

        public string? Bio { get; set; }

        public IFormFile? Avatar { get; set; }
    }
}

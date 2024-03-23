using EventHubSolution.ViewModels.Constants;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Contents
{
    public class CreatorVm
    {
        public string Id { get; set; }

        public string UserName { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public DateTime? Dob { get; set; }

        public string FullName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Gender? Gender { get; set; }

        public string? Avatar { get; set; }
    }
}

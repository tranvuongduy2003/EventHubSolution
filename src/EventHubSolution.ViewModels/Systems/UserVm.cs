using EventHubSolution.ViewModels.Constants;
using System.Text.Json.Serialization;

namespace EventHubSolution.ViewModels.Systems
{
    public class UserVm
    {
        public string Id { get; set; }

        public string UserName { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public DateTime? Dob { get; set; }

        public string FullName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Gender? Gender { get; set; }

        public string? Bio { get; set; }

        public string? Avatar { get; set; }

        public UserStatus Status { get; set; }

        public int? NumberOfFollowers { get; set; } = 0;

        public int? NumberOfFolloweds { get; set; } = 0;

        public int? NumberOfFavourites { get; set; } = 0;

        public int? NumberOfCreatedEvents { get; set; } = 0;

        public List<string> Roles { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}

﻿using EventHubSolution.BackendServer.Data.Interfaces;
using EventHubSolution.ViewModels.Constants;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EventHubSolution.BackendServer.Data.Entities
{
    public class User : IdentityUser, IDateTracking
    {
        public User()
        {

        }

        public User(string id, string userName, string fullName, string email, string phoneNumber, DateTime dob)
        {
            Id = id;
            UserName = userName;
            FullName = fullName;
            Email = email;
            PhoneNumber = phoneNumber;
            Dob = dob;
        }

        [MaxLength(50)]
        public string? FullName { get; set; }

        public DateTime? Dob { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Gender? Gender { get; set; }

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? AvatarId { get; set; }

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfFollowers { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfFolloweds { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfFavourites { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfCreatedEvents { get; set; } = 0;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        [ForeignKey("AvatarId")]
        public virtual FileStorage Avatar { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<LabelInUser> LabelInUsers { get; set; } = new List<LabelInUser>();

        [NotMapped]
        public virtual ICollection<UserFollower> Followers { get; set; } = new List<UserFollower>();

        [NotMapped]
        public virtual ICollection<Invitation> Inviters { get; set; } = new List<Invitation>();

        [NotMapped]
        public virtual ICollection<Invitation> Inviteds { get; set; } = new List<Invitation>();

        [NotMapped]
        public virtual ICollection<UserFollower> Followeds { get; set; } = new List<UserFollower>();

        [NotMapped]
        public virtual ICollection<FavouriteEvent> FavouriteEvents { get; set; } = new List<FavouriteEvent>();

        [NotMapped]
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        [NotMapped]
        public virtual ICollection<Event> Events { get; set; } = new List<Event>();

        [NotMapped]
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}

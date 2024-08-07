﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("UserFollowers")]
    [PrimaryKey("FollowerId", "FollowedId")]
    public class UserFollower
    {
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string FollowerId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string FollowedId { get; set; }

        [ForeignKey("FollowerId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User Follower { get; set; } = null!;

        [ForeignKey("FollowedId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User Followed { get; set; } = null!;
    }
}

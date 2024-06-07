using EventHubSolution.BackendServer.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Invitations")]
    [PrimaryKey("InviterId", "InvitedId", "EventId")]
    public class Invitation : IDateTracking
    {
        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string InviterId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string InvitedId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EventId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("InviterId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User Inviter { get; set; } = null!;

        [ForeignKey("InvitedId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User Invited { get; set; } = null!;

        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Event Event { get; set; } = null!;
    }
}

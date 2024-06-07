using EventHubSolution.BackendServer.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Conversations")]
    public class Conversation : IDateTracking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EventId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string HostId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserId { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? LastMessageId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Event Event { get; set; } = null!;

        [ForeignKey("HostId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User Host { get; set; } = null!;

        [ForeignKey("UserId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}

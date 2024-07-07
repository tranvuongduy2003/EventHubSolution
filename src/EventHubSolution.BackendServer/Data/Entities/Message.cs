using EventHubSolution.BackendServer.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Messages")]
    public class Message : IDateTracking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Content { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? ImageId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? VideoId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? AudioId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EventId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string ConversationId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Event Event { get; set; } = null!;

        [ForeignKey("ConversationId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Conversation Conversation { get; set; } = null!;

        [ForeignKey("UserId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User User { get; set; } = null!;

        [ForeignKey("ImageId")]
        public virtual FileStorage? Image { get; set; } = null!;

        [ForeignKey("VideoId")]
        public virtual FileStorage? Video { get; set; } = null!;

        [ForeignKey("AudioId")]
        public virtual FileStorage? Audio { get; set; } = null!;
    }
}

using EventHubSolution.BackendServer.Data.Interfaces;
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

        [MaxLength(1000)]
        [Column(TypeName = "varchar(1000)")]
        public string? Content { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? ImageId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? VideoId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string ConversationId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }


        [NotMapped]
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [NotMapped]
        [ForeignKey("ConversationId")]
        public virtual Conversation Conversation { get; set; } = null!;

        [NotMapped]
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [NotMapped]
        [ForeignKey("ImageId")]
        public virtual FileStorage? Image { get; set; } = null!;

        [NotMapped]
        [ForeignKey("VideoId")]
        public virtual FileStorage? Video { get; set; } = null!;
    }
}

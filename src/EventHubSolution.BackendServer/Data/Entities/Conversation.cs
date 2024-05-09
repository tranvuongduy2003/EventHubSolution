using EventHubSolution.BackendServer.Data.Interfaces;
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

        public string? LastMessageId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [NotMapped]
        [ForeignKey("HostId")]
        public virtual User Host { get; set; } = null!;

        [NotMapped]
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}

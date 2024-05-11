using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("EmailContents")]
    public class EmailContent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Content { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EventId { get; set; }

        [NotMapped]
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<EmailAttachment> EmailAttachments { get; set; } = new List<EmailAttachment>();

        [NotMapped]
        public virtual ICollection<EmailLogger> EmailLoggers { get; set; } = new List<EmailLogger>();
    }
}

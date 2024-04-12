using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("EmailAttachments")]
    [PrimaryKey("EmailContentId", "AttachmentId")]
    public class EmailAttachment
    {
        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EmailContentId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string AttachmentId { get; set; }

        [NotMapped]
        [ForeignKey("EmailContentId")]
        public virtual EmailContent EmailContent { get; set; } = null!;

        [NotMapped]
        [ForeignKey("AttachmentId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual FileStorage Attachment { get; set; } = null!;
    }
}

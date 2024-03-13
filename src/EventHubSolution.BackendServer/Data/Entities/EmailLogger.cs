using EventHubSolution.BackendServer.Data.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("EmailLoggers")]
    public class EmailLogger : IDateTracking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string ReceiverEmail { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string SentEmail { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EmailContentId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("EmailContentId")]
        public virtual EmailContent EmailContent { get; set; } = null!;
    }
}

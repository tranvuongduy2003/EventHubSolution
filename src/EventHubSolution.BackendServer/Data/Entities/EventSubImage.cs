using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("EventSubImages")]
    public class EventSubImage
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
        public string ImageId { get; set; }

        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [ForeignKey("ImageId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual FileStorage Image { get; set; } = null!;
    }
}

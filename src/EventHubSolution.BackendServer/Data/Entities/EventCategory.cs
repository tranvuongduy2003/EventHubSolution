using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("EventCategories")]
    [PrimaryKey("CategoryId", "EventId")]
    public class EventCategory
    {
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string CategoryId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string EventId { get; set; }

        [NotMapped]
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;

        [NotMapped]
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;
    }
}

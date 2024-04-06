using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("EventCategories")]
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

        [ForeignKey("CategoryId")]
        public virtual Category Label { get; set; } = null!;

        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;
    }
}

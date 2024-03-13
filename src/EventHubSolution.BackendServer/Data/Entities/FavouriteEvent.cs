using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("FavouriteEvents")]
    public class FavouriteEvent
    {
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string UserId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string EventId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("TicketTypes")]
    public class TicketType
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
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [Range(0, Double.PositiveInfinity)]
        public int Quantity { get; set; } = 0;

        [Required]
        [Range(0, 1000000000)]
        public long Price { get; set; }

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfSoldTickets { get; set; } = 0;

        [NotMapped]
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}

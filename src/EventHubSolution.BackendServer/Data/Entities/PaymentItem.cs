using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("PaymentItems")]
    public class PaymentItem
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
        public string TicketTypeId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string PaymentId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string Name { get; set; }

        [Required]
        [Range(0, Double.PositiveInfinity)]
        public int Quantity { get; set; } = 0;

        [Required]
        [Range(0, 1000000000)]
        public long TotalPrice { get; set; }

        [Required]
        [Range(0.00, 1.00)]
        public double Discount { get; set; } = 0;

        [NotMapped]
        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Event Event { get; set; } = null!;

        [NotMapped]
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [NotMapped]
        [ForeignKey("PaymentId")]
        public virtual Payment Payment { get; set; } = null!;

        [NotMapped]
        [ForeignKey("TicketTypeId")]
        public virtual TicketType TicketType { get; set; } = null!;
    }
}

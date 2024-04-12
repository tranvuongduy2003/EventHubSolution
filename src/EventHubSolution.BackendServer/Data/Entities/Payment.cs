using EventHubSolution.ViewModels.Constants;
using EventHubSolution.BackendServer.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Payments")]
    public class Payment : IDateTracking
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
        [Range(0, Double.PositiveInfinity)]
        public int TicketQuantity { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; }

        [Required]
        [MaxLength(100)]
        [Phone]
        public string CustomerPhone { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string CustomerEmail { get; set; }

        [Required]
        [Range(0, Double.PositiveInfinity)]
        public decimal TotalPrice { get; set; } = 0;

        [Required]
        [Range(0.00, 1.00)]
        public double Discount { get; set; } = 0;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PaymentStatus Status { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PaymentMethod PaymentMethod { get; set; }

        public string PaymentIntentId { get; set; }

        public string PaymentSession { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Event Event { get; set; } = null!;

        [NotMapped]
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}

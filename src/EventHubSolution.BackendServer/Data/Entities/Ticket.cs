using EventHubSolution.BackendServer.Data.Interfaces;
using EventHubSolution.ViewModels.Constants;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Tickets")]
    public class Ticket : IDateTracking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

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
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string TicketTypeId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EventId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string PaymentId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TicketStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("TicketTypeId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual TicketType TicketType { get; set; } = null!;

        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [ForeignKey("PaymentId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Payment Payment { get; set; } = null!;
    }
}

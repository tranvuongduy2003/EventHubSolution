using EventHubSolution.BackendServer.Data.Interfaces;
using EventHubSolution.ViewModels.Constants;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Events")]
    public class Event : IDateTracking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string CreatorId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string CoverImageId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Location { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Range(0.0, 1.0)]
        public double? Promotion { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfFavourites { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfShares { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfSoldTickets { get; set; } = 0;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventStatus Status { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventCycleType EventCycleType { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventPaymentType EventPaymentType { get; set; }

        public bool IsPrivate { get; set; } = false;

        public bool? IsTrash { get; set; } = false;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        [ForeignKey("CreatorId")]
        public virtual User Creator { get; set; } = null!;

        [NotMapped]
        [ForeignKey("CoverImageId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual FileStorage CoverImage { get; set; } = null!;


        [NotMapped]
        public virtual EmailContent? EmailContent { get; set; }

        [NotMapped]
        public virtual ICollection<EventCategory> EventCategories { get; set; } = new List<EventCategory>();

        [NotMapped]
        public virtual ICollection<EventSubImage> EventSubImages { get; set; } = new List<EventSubImage>();

        [NotMapped]
        public virtual ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();

        [NotMapped]
        public virtual ICollection<LabelInEvent> LabelInEvents { get; set; } = new List<LabelInEvent>();

        [NotMapped]
        public virtual ICollection<FavouriteEvent> FavouriteEvents { get; set; } = new List<FavouriteEvent>();

        [NotMapped]
        public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();

        [NotMapped]
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        [NotMapped]
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        [NotMapped]
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    }
}

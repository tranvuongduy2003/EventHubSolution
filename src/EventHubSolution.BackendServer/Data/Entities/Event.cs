using EventHubSolution.BackendServer.Data.Interfaces;
using EventHubSolution.ViewModels.Constants;
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
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string LocationId { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string CategoryId { get; set; }

        [Range(0.0, 1.0)]
        public double Promotion { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfFavourites { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfShares { get; set; } = 0;

        [Range(0, Double.PositiveInfinity)]
        public int? NumberOfSoldTickets { get; set; } = 0;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }


        [ForeignKey("CreatorId")]
        public virtual User Creator { get; set; } = null!;

        [ForeignKey("CoverImageId")]
        public virtual FileStorage CoverImage { get; set; } = null!;

        [ForeignKey("LocationId")]
        public virtual Location Location { get; set; } = null!;

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
    }
}

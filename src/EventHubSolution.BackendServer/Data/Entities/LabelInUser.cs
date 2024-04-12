using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("LabelInUsers")]
    [PrimaryKey("LabelId", "UserId")]
    public class LabelInUser
    {
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string LabelId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string UserId { get; set; }

        [NotMapped]
        [ForeignKey("LabelId")]
        public virtual Label Label { get; set; } = null!;

        [NotMapped]
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}

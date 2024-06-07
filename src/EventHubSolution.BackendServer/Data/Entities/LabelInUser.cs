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

        [ForeignKey("LabelId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Label Label { get; set; } = null!;

        [ForeignKey("UserId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User User { get; set; } = null!;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Functions")]
    public class Function
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string Name { get; set; }

        [MaxLength(200)]
        [Required]
        public string Url { get; set; }

        [Required]
        public int SortOrder { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? ParentId { get; set; }

        [NotMapped]
        [ForeignKey("ParentId")]
        public virtual Function Parent { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<CommandInFunction> CommandInFunctions { get; set; } = new List<CommandInFunction>();

        [NotMapped]
        public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    }
}

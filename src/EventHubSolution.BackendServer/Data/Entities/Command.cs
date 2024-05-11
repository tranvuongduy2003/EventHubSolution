using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Commands")]
    public class Command
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Name { get; set; }

        [NotMapped]
        public virtual ICollection<CommandInFunction> CommandInFunctions { get; set; } = new List<CommandInFunction>();

        [NotMapped]
        public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    }
}

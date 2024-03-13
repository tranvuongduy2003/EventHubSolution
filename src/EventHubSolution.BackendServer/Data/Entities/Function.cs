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

        [MaxLength(200)]
        [Required]
        public string Name { get; set; }

        [MaxLength(200)]
        [Required]
        public string Url { get; set; }

        [Required]
        public int SortOrder { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? ParentId { get; set; }

        [ForeignKey("ParentId")]
        public virtual Function Parent { get; set; } = null!;
    }
}

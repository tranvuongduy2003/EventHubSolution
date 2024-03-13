using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Labels")]
    public class Label
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [MaxLength(50)]
        public string Name { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("CommandInFunctions")]
    public class CommandInFunction
    {
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string CommandId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string FunctionId { get; set; }

        [ForeignKey("CommandId")]
        public virtual Command Command { get; set; } = null!;

        [ForeignKey("FunctionId")]
        public virtual Function Function { get; set; } = null!;
    }
}

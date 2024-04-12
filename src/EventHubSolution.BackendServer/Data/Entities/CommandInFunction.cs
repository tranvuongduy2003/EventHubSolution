using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("CommandInFunctions")]
    [PrimaryKey("CommandId", "FunctionId")]
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

        [NotMapped]
        [ForeignKey("CommandId")]
        public virtual Command Command { get; set; } = null!;

        [NotMapped]
        [ForeignKey("FunctionId")]
        public virtual Function Function { get; set; } = null!;
    }
}

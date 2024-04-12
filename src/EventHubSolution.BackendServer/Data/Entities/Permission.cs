using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Permissions")]
    [PrimaryKey("FunctionId", "RoleId", "CommandId")]
    public class Permission
    {
        public Permission(string functionId, string roleId, string commandId)
        {
            FunctionId = functionId;
            RoleId = roleId;
            CommandId = commandId;
        }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string FunctionId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string RoleId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string CommandId { get; set; }

        [NotMapped]
        [ForeignKey("FunctionId")]
        public virtual Function Function { get; set; } = null!;

        [NotMapped]
        [ForeignKey("RoleId")]
        public virtual IdentityRole Role { get; set; } = null!;

        [NotMapped]
        [ForeignKey("CommandId")]
        public virtual Command Command { get; set; } = null!;
    }
}

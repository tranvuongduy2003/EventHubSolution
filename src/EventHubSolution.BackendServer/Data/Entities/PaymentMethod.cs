using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("PaymentMethods")]
    public class PaymentMethod
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Column(TypeName = "nvarchar(50)")]
        public string MethodName { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string MethodLogoId { get; set; }

        [ForeignKey("MethodLogoId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual FileStorage MethodLogo { get; set; } = null!;

        public virtual ICollection<UserPaymentMethod> UserPaymentMethods { get; set; } = new List<UserPaymentMethod>();
    }
}

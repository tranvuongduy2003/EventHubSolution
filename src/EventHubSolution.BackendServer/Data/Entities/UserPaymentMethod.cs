using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("UserPaymentMethods")]
    public class UserPaymentMethod
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string MethodId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentAccountNumber { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? PaymentAccountQRCodeId { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string? CheckoutContent { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual User User { get; set; } = null!;

        [ForeignKey("MethodId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual PaymentMethod Method { get; set; } = null!;

        [ForeignKey("PaymentAccountQRCodeId")]
        public virtual FileStorage? PaymentAccountQRCode { get; set; } = null!;

        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}

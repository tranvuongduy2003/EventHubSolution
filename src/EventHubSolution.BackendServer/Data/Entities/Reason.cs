﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("Reasons")]
    public class Reason
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string EventId { get; set; }

        [Required]
        [MaxLength(1000)]
        [Column(TypeName = "nvarchar(1000)")]
        public string Name { get; set; }

        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.ClientSetNull)]
        public virtual Event Event { get; set; } = null!;
    }
}

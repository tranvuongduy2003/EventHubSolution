﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHubSolution.BackendServer.Data.Entities
{
    [Table("LabelInEvents")]
    [PrimaryKey("LabelId", "EventId")]
    public class LabelInEvent
    {
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string LabelId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string EventId { get; set; }

        [NotMapped]
        [ForeignKey("LabelId")]
        public virtual Label Label { get; set; } = null!;

        [NotMapped]
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;
    }
}

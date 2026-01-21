using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database
{
    public class ErrorCatalogEntry
    {
        [Key]
        [MaxLength(100)]
        public string ErrorCode { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        public string CommonCauses { get; set; }

        public string SuggestedSolution { get; set; }

        [MaxLength(50)]
        public string Severity { get; set; } // Low, Medium, High, Critical

        [MaxLength(100)]
        public string Category { get; set; } // Hardware, Protocol, Electrical, Network, etc.
    }
}

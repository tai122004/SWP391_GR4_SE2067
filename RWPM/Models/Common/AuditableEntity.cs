using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.Common
{
    public class AuditableEntity
    {
        [Precision(0)]
        public DateTime CreatedDate { get; set; }
        [MaxLength(30)]
        public string CreatedBy { get; set; } = string.Empty;
        [Precision(0)]
        public DateTime? UpdatedDate { get; set; }
        [MaxLength(30)]
        public string? UpdatedBy { get; set; }
    }
}

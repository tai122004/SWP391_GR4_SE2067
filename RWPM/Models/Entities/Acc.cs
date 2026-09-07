using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.Entities
{
    public class Acc : AuditableEntity, IActivatable
    {
        [Key]
        [MaxLength(30)]
        public string Username { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EnumDataType(typeof(AccountRole))]
        public AccountRole Role { get; set; }

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }
        //public int? DeptCatId { get; set; }
        //public DeptCat.DeptCat? DeptCat {  get; set; }
    }
}

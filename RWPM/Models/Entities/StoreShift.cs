using RWPM.Models.Common;

namespace RWPM.Models.Entities;

public class StoreShift : AuditableEntity, IActivatable
{
    public int StoreId { get; set; }
    public int ShiftId { get; set; }
    public bool IsActive { get; set; } = true;
    public Store Store { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
}

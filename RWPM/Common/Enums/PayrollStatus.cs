namespace RWPM.Common.Enums
{
    public enum PayrollStatus : byte
    {
        Draft = 1,   // Bản nháp / Mới tính (Cho phép tính lại)
        Locked = 2   // Đã chốt (Bất biến, không thể sửa trừ khi Unlock)
    }
}

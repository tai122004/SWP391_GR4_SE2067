namespace RWPM.Common.Enums
{
    public enum PayrollDetailStatus : byte
    {
        Valid = 1,        // Hợp lệ, tính lương thành công
        MissingRate = 2,  // Chưa có đơn giá theo giờ
        HasWarnings = 3   // Có ca chấm công bất thường / thiếu vào ra
    }
}

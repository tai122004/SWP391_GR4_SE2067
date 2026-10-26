using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;

namespace RWPM.Infrastructure.Storage
{
    /// <summary>
    /// Ảnh chấm công (ảnh khuôn mặt) là dữ liệu cá nhân nhạy cảm: lưu NGOÀI wwwroot (App_Data/attendance)
    /// và chỉ xem được qua AttendanceController.Photo có kiểm tra quyền.
    /// Giá trị lưu trong DB: "secure/{tên file}". Đường dẫn cũ "/images/attendance/..." được chuyển dần khi khởi động.
    /// </summary>
    public static class AttendancePhotoStore
    {
        public const string SecurePrefix = "secure/";
        private const string LegacyPrefix = "/images/attendance/";

        public static string PrivateRoot(IWebHostEnvironment env)
        {
            var root = Path.Combine(env.ContentRootPath, "App_Data", "attendance");
            Directory.CreateDirectory(root);
            return root;
        }

        public static async Task<string> SaveAsync(IWebHostEnvironment env, IFormFile photo, string fileName)
        {
            var safeName = Path.GetFileName(fileName);
            var filePath = Path.Combine(PrivateRoot(env), safeName);
            await using (var stream = new FileStream(filePath, FileMode.CreateNew))
            {
                await photo.CopyToAsync(stream);
            }
            return SecurePrefix + safeName;
        }

        /// <summary>Đường dẫn vật lý của ảnh; null nếu giá trị không hợp lệ (chặn path traversal).</summary>
        public static string? ResolvePhysicalPath(IWebHostEnvironment env, string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return null;
            string? name = null, root = null;
            if (storedPath.StartsWith(SecurePrefix, StringComparison.Ordinal))
            {
                name = storedPath[SecurePrefix.Length..];
                root = PrivateRoot(env);
            }
            else if (storedPath.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                name = storedPath[LegacyPrefix.Length..];
                root = Path.Combine(env.WebRootPath, "images", "attendance");
            }
            if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name)) return null;
            var full = Path.Combine(root!, name);
            return File.Exists(full) ? full : null;
        }

        public static string ContentType(string physicalPath) =>
            Path.GetExtension(physicalPath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };

        /// <summary>
        /// Chạy khi khởi động: chuyển ảnh cũ từ wwwroot/images/attendance sang App_Data/attendance và cập nhật DB.
        /// An toàn khi chạy nhiều lần.
        /// </summary>
        public static async Task MigrateLegacyAsync(DefaultDatabaseContext db, IWebHostEnvironment env, ILogger logger)
        {
            var records = await db.AttendanceRecord
                .Where(r => (r.CheckInPhotoPath != null && r.CheckInPhotoPath.StartsWith(LegacyPrefix))
                         || (r.CheckOutPhotoPath != null && r.CheckOutPhotoPath.StartsWith(LegacyPrefix)))
                .ToListAsync();
            if (records.Count == 0) return;

            var moved = 0;
            string? Move(string? stored)
            {
                if (stored == null || !stored.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase)) return stored;
                var name = Path.GetFileName(stored[LegacyPrefix.Length..]);
                var source = Path.Combine(env.WebRootPath, "images", "attendance", name);
                var target = Path.Combine(PrivateRoot(env), name);
                try
                {
                    if (File.Exists(source))
                    {
                        if (!File.Exists(target)) File.Move(source, target);
                        else File.Delete(source);
                    }
                    if (!File.Exists(target)) return stored; // không tìm thấy file -> giữ nguyên giá trị cũ
                    moved++;
                    return SecurePrefix + name;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Cannot move attendance photo {Photo}", stored);
                    return stored;
                }
            }

            foreach (var r in records)
            {
                r.CheckInPhotoPath = Move(r.CheckInPhotoPath);
                r.CheckOutPhotoPath = Move(r.CheckOutPhotoPath);
            }
            await db.SaveChangesAsync();
            logger.LogWarning("Moved {Count} attendance photos out of wwwroot.", moved);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;

namespace RWPM.Infrastructure.Storage
{
    // Quản lý lưu trữ ảnh chấm công vào thư mục bảo mật App_Data (ngoài wwwroot)
    public static class AttendancePhotoStore
    {
        public const string SecurePrefix = "secure/";
        private const string LegacyPrefix = "/images/attendance/";

        // Thư mục lưu ảnh nội bộ: App_Data/attendance
        public static string PrivateRoot(IWebHostEnvironment env)
        {
            var root = Path.Combine(env.ContentRootPath, "App_Data", "attendance");
            Directory.CreateDirectory(root);
            return root;
        }

        // Lưu file ảnh vào App_Data
        public static async Task<string> SaveAsync(IWebHostEnvironment env, IFormFile photo, string fileName)
        {
            var safeName = Path.GetFileName(fileName); // Tránh path traversal
            var filePath = Path.Combine(PrivateRoot(env), safeName);
            await using (var stream = new FileStream(filePath, FileMode.CreateNew))
            {
                await photo.CopyToAsync(stream);
            }
            return SecurePrefix + safeName;
        }

        // Lấy đường dẫn vật lý an toàn từ chuỗi lưu trong database
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

        // Chuyển ảnh cũ từ wwwroot sang App_Data/attendance khi khởi động
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
                    if (!File.Exists(target)) return stored;
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
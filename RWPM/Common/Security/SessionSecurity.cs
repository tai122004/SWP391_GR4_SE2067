using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;

namespace RWPM.Common.Security
{
    /// <summary>
    /// Claim, kiểm tra lại phiên đăng nhập và bắt buộc đổi mật khẩu.
    /// </summary>
    public static class SessionSecurity
    {
        public const string StampClaim = "rwpm:stamp";
        public const string MustChangePasswordClaim = "rwpm:must_change_password";
        private const string ValidatedAtKey = ".rwpm.validated_at";

        /// <summary>Chu kỳ kiểm tra lại tài khoản trong DB (khóa, đổi vai trò, đổi/cấp lại mật khẩu).</summary>
        public static readonly TimeSpan RevalidateInterval = TimeSpan.FromMinutes(1);

        public static string NewStamp() => Guid.NewGuid().ToString("N");

        public static bool MustChangePassword(ClaimsPrincipal user) =>
            user.Identity?.IsAuthenticated == true && user.FindFirst(MustChangePasswordClaim)?.Value == "1";

        public static ClaimsPrincipal BuildPrincipal(Acc account)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, account.Username),
                // Antiforgery gắn token theo NameIdentifier -> token không bị vô hiệu khi stamp/cờ đổi mật khẩu thay đổi
                new Claim(ClaimTypes.NameIdentifier, account.Username),
                new Claim(ClaimTypes.Role, account.Role.ToString()),
                new Claim(StampClaim, account.SecurityStamp ?? string.Empty),
                new Claim(MustChangePasswordClaim, account.MustChangePassword ? "1" : "0")
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        /// <summary>
        /// Gắn vào CookieAuthenticationEvents.OnValidatePrincipal: định kỳ đối chiếu cookie với DB.
        /// Tài khoản bị xóa/khóa, đổi vai trò hoặc đổi mật khẩu (SecurityStamp khác) -> đăng xuất ngay.
        /// </summary>
        public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var username = principal?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                await RejectAsync(context);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (context.Properties.Items.TryGetValue(ValidatedAtKey, out var raw)
                && DateTimeOffset.TryParse(raw, out var last) && now - last < RevalidateInterval)
            {
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<DefaultDatabaseContext>();
            var account = await db.Acc.AsNoTracking()
                .Where(a => a.Username == username)
                .Select(a => new { a.Username, a.IsActive, a.Role, a.SecurityStamp, a.MustChangePassword })
                .FirstOrDefaultAsync();

            var roleClaims = principal!.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();
            var stamp = principal.FindFirst(StampClaim)?.Value ?? string.Empty;
            if (account == null
                || !account.IsActive
                || roleClaims.Count != 1 || roleClaims[0] != account.Role.ToString()
                || stamp != (account.SecurityStamp ?? string.Empty))
            {
                await RejectAsync(context);
                return;
            }

            context.Properties.Items[ValidatedAtKey] = now.ToString("O");
            var mustChange = account.MustChangePassword ? "1" : "0";
            if (principal.FindFirst(MustChangePasswordClaim)?.Value != mustChange)
            {
                // Cờ đổi mật khẩu thay đổi mà phiên vẫn hợp lệ -> làm mới claim
                context.ReplacePrincipal(BuildPrincipal(new Acc
                {
                    Username = account.Username,
                    Role = account.Role,
                    SecurityStamp = account.SecurityStamp,
                    MustChangePassword = account.MustChangePassword
                }));
            }
            context.ShouldRenew = true;
        }

        private static async Task RejectAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Middleware: người dùng phải đổi mật khẩu thì chỉ được vào trang Đổi mật khẩu, đăng xuất, đổi ngôn ngữ.
        /// </summary>
        public static IApplicationBuilder UseForcePasswordChange(this IApplicationBuilder app)
        {
            string[] allowed = { "/acc/changepassword", "/auth/", "/home/setlanguage", "/home/error", "/hub/" };
            return app.Use(async (context, next) =>
            {
                if (MustChangePassword(context.User))
                {
                    var path = (context.Request.Path.Value ?? string.Empty).ToLowerInvariant();
                    var isAllowed = allowed.Any(a => path == a.TrimEnd('/') || path.StartsWith(a.EndsWith('/') ? a : a + "/") || path == a);
                    if (!isAllowed)
                    {
                        var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                                     || context.Request.Headers.Accept.Any(h => h != null && h.Contains("application/json"))
                                     || !HttpMethods.IsGet(context.Request.Method);
                        if (isAjax)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new { success = false, message = RWPM.Resources.Shared.Auth.ForcedChangePassword_Notice });
                            return;
                        }
                        context.Response.Redirect(context.Request.PathBase + "/Acc/ChangePassword");
                        return;
                    }
                }
                await next();
            });
        }
    }
}

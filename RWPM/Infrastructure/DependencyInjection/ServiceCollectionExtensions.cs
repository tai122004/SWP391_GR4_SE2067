using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Localization;
using RWPM.Hubs.Config;
using RWPM.Infrastructure.Data;
using RWPM.Infrastructure.Storage;
using RWPM.Services.Abstraction;
using RWPM.Services.Implementation;

namespace RWPM.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();

            #region Hub
            services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
            services.AddSingleton<IProgressNotificationService, ProgressNotificationService>();
            services.AddSignalR();
            #endregion

            #region DB context and services
            services.AddDbContext<DefaultDatabaseContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection") ?? throw new Exception("Please config the DefaultConnection connection string!")));

            // Register IHttpContextAccessor
            services.AddHttpContextAccessor();
            #endregion

            #region Authentication
            var keysPath = Path.Combine(Directory.GetCurrentDirectory(), "Keys");
            if (!Directory.Exists(keysPath))
            {
                Directory.CreateDirectory(keysPath);
            }

            services.AddDataProtection()
                .SetApplicationName("RWPM")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "RWPM_Auth_Cookie";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;

                    options.ExpireTimeSpan = TimeSpan.FromDays(7); // Duy trì đăng nhập 7 ngày
                    options.SlidingExpiration = true;             // Tự động gia hạn khi người dùng thao tác

                    options.LoginPath = "/Auth/Login";
                    options.AccessDeniedPath = "/Auth/AccessDenied";

                    // Kiểm tra lại tài khoản định kỳ: bị khóa/nghỉ việc, đổi vai trò, đổi mật khẩu -> phiên cũ bị đăng xuất
                    options.Events.OnValidatePrincipal = RWPM.Common.Security.SessionSecurity.ValidatePrincipalAsync;
                });
            services.AddAuthorization();

            #region Antiforgery
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
            });
            #endregion
            #endregion

            #region Web Encoders (Support Vietnamese Unicode)
            services.AddSingleton<System.Text.Encodings.Web.HtmlEncoder>(
                System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
            services.AddSingleton<System.Text.Encodings.Web.JavaScriptEncoder>(
                System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
            #endregion

            #region Localization
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services
                .AddControllersWithViews(options => options.UseLocalizedBindingMessages())
                .AddViewLocalization();
            // Chạy sau DataAnnotationsMetadataProvider -> dịch cả [Required] ngầm định của string không nullable
            services.PostConfigure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
                options.ModelMetadataDetailsProviders.Add(new RWPM.Common.Localization.LocalizedValidationMetadataProvider()));
            #endregion

            services.AddScoped<IFileStorageService, LocalFileSystemStorage>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccService, AccService>();
            services.AddScoped<IStoreService, StoreService>();
            services.AddScoped<IEmployeeService, EmployeeService>();
            services.AddScoped<IImportLogService, ImportLogService>();
            services.AddScoped<IShiftService, ShiftService>();

            services.AddScoped<IAttendanceService, AttendanceService>();
            services.AddScoped<IAttendanceAdjustmentService, AttendanceAdjustmentService>();
            services.AddScoped<IShiftRegistrationService, ShiftRegistrationService>();
            services.AddScoped<IShiftSwapService, ShiftSwapService>();
            services.AddScoped<ILeaveRequestService, LeaveRequestService>();
            services.AddScoped<IPayrollService, PayrollService>();
            services.AddScoped<IPerformanceService, PerformanceService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAvailabilityService, AvailabilityService>();
            services.AddScoped<IShiftDemandService, ShiftDemandService>();

            return services;
        }
    }
}

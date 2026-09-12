using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RWPM.Hubs.Config;
using RWPM.Infrastructure.Data;
//using RWPM.Infrastructure.Services.Abstraction;
//using RWPM.Infrastructure.Services.Implementation;
using RWPM.Infrastructure.Storage;
using RWPM.Services.Abstraction;
using RWPM.Services.Implementation;

namespace RWPM.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();

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
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromDays(1); // cookie expires in 1 day
                    options.SlidingExpiration = false;  // disables renewing expiration on each request

                    options.LoginPath = "/Auth/Login";
                    options.AccessDeniedPath = "/Auth/AccessDenied";
                });
            services.AddAuthorization();

            #region Antiforgery
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
            });
            #endregion
            #endregion

            #region Localization
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services
                .AddControllersWithViews()
                .AddViewLocalization();
            #endregion

            services.AddScoped<IFileStorageService, LocalFileSystemStorage>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccService, AccService>();
            services.AddScoped<IStoreService, StoreService>();
            services.AddScoped<IEmployeeService, EmployeeService>();
            services.AddScoped<IImportLogService, ImportLogService>();
            //services.AddScoped<IIdCounterService, IdCounterService>();

            return services;
        }
    }
}

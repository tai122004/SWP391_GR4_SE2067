using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using RWPM.Hubs;
using RWPM.Infrastructure.Data;
using RWPM.Infrastructure.DependencyInjection;
using RWPM.Infrastructure.Seeder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterServices(builder.Configuration);

var app = builder.Build();

#region Seed admin account
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DefaultDatabaseContext>();
    await dbContext.Database.MigrateAsync();
    // Maintenance/test restarts can preserve all existing data, including attendance demo data.
    if (!builder.Configuration.GetValue<bool>("SkipStartupSeeding"))
    {
    await new AccAdminSeeder().SeedAsync(dbContext);
    await new ShiftSeeder().SeedAsync(dbContext);
    await StoreTimeFixSeeder.FixStoreTimesAsync(dbContext);
    if (app.Environment.IsDevelopment())
    {
        await new PayrollAttendanceDemoSeeder().SeedAsync(dbContext);
    }
    }
}
#endregion

#region Support multiple language
var supportedCultures = new[] { "en", "vi" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.RequestCultureProviders.Clear();
//localizationOptions.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);
#endregion

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

#region Hub
app.MapHub<ApplicationHub>("/hub/notifications");
#endregion

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogWarning("----- Application started -----");

app.Run();

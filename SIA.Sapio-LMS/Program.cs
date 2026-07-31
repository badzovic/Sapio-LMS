using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio.LMS.Data.Seed;
using SIA.Sapio_LMS.Helpers;
using SIA.Sapio_LMS.Services;
using Hangfire;
using Hangfire.SqlServer;
using SIA.Sapio_LMS.Services.InternalUsersSync;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddErrorDescriber<BosnianIdentityErrorDescriber>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// api service builder
builder.Services.AddHttpClient<InternalUsersApiService>();
builder.Services.AddHttpClient<ExternalUsersApiService>();
builder.Services.AddHttpClient<V3ProgramsApiService>();
builder.Services.AddHttpClient<V3MatrixApiService>();
builder.Services.AddScoped<UserTrainingMatrixService>();
builder.Services.AddScoped<AutoTestAssignmentService>();
builder.Services.AddScoped<MaterialPreviewService>();
builder.Services.AddScoped<MaterialPdfService>();
builder.Services.AddScoped<IInternalUsersSyncService,InternalUsersSyncService>();
builder.Services.AddScoped<InternalUsersSyncService>();

builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            SchemaName = "HangFire"
        });
});

builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");

// REDIRECT NA LOGIN
app.MapGet("/", context =>
{
    context.Response.Redirect("/Identity/Account/Login");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    await RoleSeeder.SeedRoles(roleManager);
    await AdminSeeder.SeedAdmin(userManager);
}

RecurringJob.AddOrUpdate<AutoTestAssignmentService>(
    "daily-auto-test-assignments",
    service => service.RunDailyAutoAssignmentsAsync(),
    "30 6 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows()
                ? "Central European Standard Time"
                : "Europe/Sarajevo")
    });

RecurringJob.AddOrUpdate<InternalUsersSyncService>(
    "internal-users-daily-sync",
    service => service.RunDailyInternalUsersSyncAsync(),
    "0 5 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            "Central European Standard Time")
    });

app.Run();
using HealthCoverage.Areas.Identity;
using HealthCoverage.Data;
using HealthCoverage.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// กำหนด URL สำหรับ bind ทุก interface (Docker)
builder.WebHost.UseUrls("http://0.0.0.0:5001");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers().AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null);

// DbContext + IDbContextFactory (factory จำเป็นสำหรับ Blazor Server scoped services)
builder.Services.AddEntityFrameworkNpgsql()
    .AddDbContext<ApplicationDbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")))
    .AddDbContextFactory<ApplicationDbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")),
        ServiceLifetime.Scoped);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<PrbReportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<UserManagementService>();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var app = builder.Build();

// ── Seed default Admin account ────────────────────────────────────────────
await SeedAdminAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseMigrationsEndPoint();
	app.UseDeveloperExceptionPage();
}
else
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// ── Admin seed ────────────────────────────────────────────────────────────
static async Task SeedAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    // Ensure roles exist
    foreach (var role in new[] { "Admin", "Staff" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    // Create default admin if no admin exists yet
    const string adminUser  = "admin";
    const string adminEmail = "admin@bangphai.local";
    const string adminPwd   = "Admin@123";

    if (await userManager.FindByNameAsync(adminUser) is null)
    {
        var user = new IdentityUser { UserName = adminUser, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, adminPwd);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, "Admin");
    }
}

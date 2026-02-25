using IJULR.Web.Data;
using IJULR.Web.Helpers;
using IJULR.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ==================== CLOUD FILE HANDLING - ADD THIS ====================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<FileUrlHelper>();

var storageProvider = builder.Configuration["StorageSettings:Provider"];
if (storageProvider == "R2")
{
    builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
    Console.WriteLine("Using Cloudflare R2 Storage");
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    Console.WriteLine("Using Local File Storage");
}
// ==================== END CLOUD FILE HANDLING ====================

builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IWhatsAppService, TwilioWhatsAppService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ==================== CLOUD FILE API ROUTES - ADD THIS ====================
app.MapControllers();
// ==================== END CLOUD FILE API ROUTES ====================

app.Run();
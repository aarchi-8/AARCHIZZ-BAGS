using AARCHIZZ.Data;
using AARCHIZZ.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = "AARCHIZZ.Cart";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddSingleton<ShoppingCartSessionStore>();
builder.Services.AddScoped<ShoppingCartService>();
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.Configure<StoreOptions>(builder.Configuration.GetSection(StoreOptions.SectionName));
builder.Services.AddSingleton<WhatsAppOrderService>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddScoped<DatabaseCatalogService>();
builder.Services.AddScoped<AdminCredentialValidator>();
builder.Services.AddScoped<OwnerProvisioningService>();
builder.Services.AddSingleton<IProductImageStorage, LocalProductImageStorage>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AARCHIZZ.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var ownerId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var securityStamp = context.Principal?.FindFirst("owner_security_stamp")?.Value;
                if (!int.TryParse(ownerId, out var parsedOwnerId) || string.IsNullOrWhiteSpace(securityStamp))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AARCHIZZDbContext>();
                var owner = await db.OwnerAccounts.SingleOrDefaultAsync(account => account.Id == parsedOwnerId, context.HttpContext.RequestAborted);
                if (owner is null || owner.SingletonMarker != "OWNER" || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(owner.SecurityStamp), Encoding.UTF8.GetBytes(securityStamp)))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("Administrator"));
});

var databaseConnectionString = builder.Configuration.GetConnectionString("AARCHIZZ");
builder.Services.AddDbContext<AARCHIZZDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(databaseConnectionString))
    {
        options.UseSqlServer(databaseConnectionString);
    }
});

var app = builder.Build();

if (args.Any(arg => arg.Equals("--provision-owner", StringComparison.OrdinalIgnoreCase)))
{
    await using var scope = app.Services.CreateAsyncScope();
    var provisioning = scope.ServiceProvider.GetRequiredService<OwnerProvisioningService>();
    return await provisioning.RunAsync();
}

if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    app.Logger.LogWarning("AARCHIZZ SQL Server connection is not configured. The sample catalog remains active; configure ConnectionStrings:AARCHIZZ before using EF Core data access or migrations.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();

var uploadRoot = builder.Configuration["AARCHIZZ_UPLOAD_ROOT"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AARCHIZZ", "uploads");
Directory.CreateDirectory(uploadRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadRoot),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync();
return 0;

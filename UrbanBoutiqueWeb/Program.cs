using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueWeb.Data;
using UrbanBoutiqueWeb.Controllers;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

var connStr = BuildConnectionString(builder.Configuration)
    ?? throw new InvalidOperationException(
        "No database configured. Set DATABASE_URL (Railway) or ConnectionStrings:DefaultConnection.");

// Public-facing URL. Set APP_URL explicitly, or let Railway supply RAILWAY_PUBLIC_DOMAIN.
var appUrl = ResolvePublicUrl(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connStr));

builder.Services.AddDistributedMemoryCache();

var isProd = !builder.Environment.IsDevelopment();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "UrbanBoutique.Session";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isProd ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});

// Honor X-Forwarded-* when deployed behind a reverse proxy (Railway, Nginx, etc.).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Seed database & patch any missing tables (for upgrades from older schema)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Idempotent patch — adds tables EnsureCreated skips when DB already exists.
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Categories"" (
            ""CategoryID"" SERIAL PRIMARY KEY,
            ""Name"" VARCHAR(50) NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Categories_Name"" ON ""Categories"" (""Name"");

        CREATE TABLE IF NOT EXISTS ""Sales"" (
            ""SaleID"" SERIAL PRIMARY KEY,
            ""SaleDate"" TIMESTAMP NOT NULL DEFAULT NOW(),
            ""TotalAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS ""SaleItems"" (
            ""SaleItemID"" SERIAL PRIMARY KEY,
            ""SaleID"" INT NOT NULL REFERENCES ""Sales""(""SaleID"") ON DELETE CASCADE,
            ""VariantID"" INT NOT NULL REFERENCES ""ProductVariants""(""VariantID""),
            ""Quantity"" INT NOT NULL,
            ""Price"" NUMERIC(18,2) NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ""Users"" (
            ""UserID"" SERIAL PRIMARY KEY,
            ""Username"" VARCHAR(50) NOT NULL,
            ""Password"" VARCHAR(255) NOT NULL,
            ""Role"" VARCHAR(20) NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Username"" ON ""Users"" (""Username"");
    ");

    // Admin seeding — environment wins, with safe fallbacks.
    var envAdminUser = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
    var envAdminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
    var adminUser = envAdminUser
                    ?? builder.Configuration["Security:DefaultAdminUsername"]
                    ?? "admin";
    var adminPass = envAdminPass
                    ?? builder.Configuration["Security:DefaultAdminPassword"]
                    ?? "admin123";

    var existingAdmin = db.Users.FirstOrDefault(u => u.Username == adminUser);
    if (existingAdmin == null)
    {
        // User with the configured username doesn't exist yet — create it.
        db.Users.Add(new User {
            Username = adminUser,
            Password = AuthController.HashPassword(adminPass),
            Role = "Admin"
        });
        db.SaveChanges();
        app.Logger.LogInformation("Seeded admin user '{User}' (source: {Source}).",
            adminUser,
            envAdminUser != null ? "ADMIN_USERNAME env" : "default config");
    }
    else
    {
        var shouldReset = false;
        var reason = "";

        // 1. Legacy SHA256 hash — always upgrade to PBKDF2.
        if (!existingAdmin.Password.StartsWith("pbkdf2$"))
        {
            shouldReset = true;
            reason = "legacy hash upgrade";
        }
        // 2. ADMIN_PASSWORD env var explicitly set — always re-seed so that
        //    updating the Railway variable + redeploy actually changes login.
        else if (envAdminPass != null && !AuthController.VerifyPassword(adminPass, existingAdmin.Password))
        {
            shouldReset = true;
            reason = "ADMIN_PASSWORD env changed";
        }
        // 3. ADMIN_FORCE_RESET=true — emergency override.
        else if (string.Equals(Environment.GetEnvironmentVariable("ADMIN_FORCE_RESET"), "true",
                               StringComparison.OrdinalIgnoreCase))
        {
            shouldReset = true;
            reason = "ADMIN_FORCE_RESET=true";
        }

        if (shouldReset)
        {
            existingAdmin.Password = AuthController.HashPassword(adminPass);
            existingAdmin.Role = "Admin";
            db.SaveChanges();
            app.Logger.LogInformation("Reset admin '{User}' password ({Reason}).",
                adminUser, reason);
        }
        else
        {
            app.Logger.LogInformation("Admin '{User}' already up to date.", adminUser);
        }
    }

    if (!db.Categories.Any())
    {
        db.Categories.AddRange(
            new Category { Name = "Clothing" },
            new Category { Name = "Footwear" },
            new Category { Name = "Accessories" }
        );
        db.SaveChanges();
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSession();

// Session-aware redirects.
app.MapGet("/admin", (HttpContext ctx) =>
{
    var role = ctx.Session.GetString("Role");
    ctx.Response.Redirect(role == "Admin" ? "/admin.html" : "/admin-login.html");
    return Task.CompletedTask;
});

app.MapGet("/cashier", (HttpContext ctx) =>
{
    var role = ctx.Session.GetString("Role");
    ctx.Response.Redirect(role == "Sales Staff" || role == "Admin" ? "/cashier.html" : "/login.html");
    return Task.CompletedTask;
});

app.MapGet("/login", (HttpContext ctx) => { ctx.Response.Redirect("/login.html"); return Task.CompletedTask; });
app.MapGet("/admin-login", (HttpContext ctx) => { ctx.Response.Redirect("/admin-login.html"); return Task.CompletedTask; });

// Health check for Railway.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Public runtime config for the frontend.
app.MapGet("/api/config", () => Results.Ok(new {
    appUrl,
    environment = app.Environment.EnvironmentName
}));

app.MapControllers();

// Log the resolved URL on startup.
app.Logger.LogInformation("Urban Boutique ready. Public URL: {AppUrl}", appUrl ?? "(not set)");

app.Run();

// --- Helpers ---
static string? ResolvePublicUrl(IConfiguration config)
{
    // 1. Explicit override
    var explicitUrl = Environment.GetEnvironmentVariable("APP_URL")
                      ?? Environment.GetEnvironmentVariable("PUBLIC_URL")
                      ?? config["Public:AppUrl"];
    if (!string.IsNullOrWhiteSpace(explicitUrl)) return explicitUrl.TrimEnd('/');

    // 2. Railway convenience variables
    var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN");
    if (!string.IsNullOrWhiteSpace(railwayDomain)) return $"https://{railwayDomain}";

    var railwayStatic = Environment.GetEnvironmentVariable("RAILWAY_STATIC_URL");
    if (!string.IsNullOrWhiteSpace(railwayStatic)) return railwayStatic.TrimEnd('/');

    return null;
}

static string? BuildConnectionString(IConfiguration config)
{
    // Railway/Heroku/Render style: DATABASE_URL=postgresql://user:pass@host:port/db
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(url))
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var db = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;
        return $"Host={uri.Host};Port={port};Username={user};Password={pass};Database={db};" +
               $"SSL Mode=Require;Trust Server Certificate=true;Pooling=true";
    }

    return config.GetConnectionString("DefaultConnection");
}

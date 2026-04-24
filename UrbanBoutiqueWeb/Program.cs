using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueWeb.Data;
using UrbanBoutiqueWeb.Controllers;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Username=postgres;Password=1;Database=urban_boutique";

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connStr));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "UrbanBoutique.Session";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

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

    var adminUser = builder.Configuration["Security:DefaultAdminUsername"] ?? "admin";
    var adminPass = builder.Configuration["Security:DefaultAdminPassword"] ?? "admin123";

    if (!db.Users.Any())
    {
        db.Users.Add(new User {
            Username = adminUser,
            Password = AuthController.HashPassword(adminPass),
            Role = "Admin"
        });
        db.SaveChanges();
    }
    else
    {
        // Upgrade legacy SHA256 hashes (pre-PBKDF2) to the new format by resetting default admin.
        var existingAdmin = db.Users.FirstOrDefault(u => u.Username == adminUser);
        if (existingAdmin != null && !existingAdmin.Password.StartsWith("pbkdf2$"))
        {
            existingAdmin.Password = AuthController.HashPassword(adminPass);
            db.SaveChanges();
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

// Session-aware redirects: unauthenticated users see the dedicated login page,
// authenticated admins/staff go straight to their dashboard.
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

app.MapControllers();

app.Run();

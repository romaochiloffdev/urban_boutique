using Microsoft.AspNetCore.Mvc;
using UrbanBoutiqueWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace UrbanBoutiqueWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [SessionAuth(Role = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // --- PRODUCTS ---
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _context.Products.Include(p => p.Variants).ToListAsync();
            var result = products.SelectMany(p => p.Variants.Select(v => new {
                VariantID = v.VariantID,
                ProductName = p.Name, Category = p.Category, Price = p.Price,
                Size = v.Size, Color = v.Color, StockQuantity = v.StockQuantity,
                IsLowStock = v.StockQuantity < 5
            })).ToList();
            return Ok(result);
        }

        public class ProductRequest {
            public string Name { get; set; } public decimal Price { get; set; } public string Category { get; set; }
            public string Size { get; set; } public string Color { get; set; } public int StockQuantity { get; set; }
        }

        [HttpPost("products")]
        public async Task<IActionResult> AddProduct([FromBody] ProductRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Name)) return BadRequest(new { message = "Name is required" });
            if (req.Price <= 0) return BadRequest(new { message = "Price must be greater than 0" });
            if (req.StockQuantity < 0) return BadRequest(new { message = "Stock cannot be negative" });

            var newProduct = new Product { Name = req.Name.Trim(), Price = req.Price, Category = req.Category ?? "" };
            newProduct.Variants.Add(new ProductVariant { Size = req.Size ?? "", Color = req.Color ?? "", StockQuantity = req.StockQuantity });
            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // --- CATEGORIES ---
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var cats = await _context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
            return Ok(cats);
        }

        public class CategoryRequest { public string Name { get; set; } }

        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] CategoryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Name)) return BadRequest(new { message = "Name is required" });
            var name = req.Name.Trim();
            if (await _context.Categories.AnyAsync(c => c.Name == name))
                return BadRequest(new { message = "Category already exists" });
            _context.Categories.Add(new Category { Name = name });
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // --- USERS ---
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users.Select(u => new { u.UserID, u.Username, u.Role }).ToListAsync();
            return Ok(users);
        }

        public class UserRequest { public string Username { get; set; } public string Password { get; set; } public string Role { get; set; } }

        [HttpPost("users")]
        public async Task<IActionResult> AddUser([FromBody] UserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Username and password are required" });
            if (req.Password.Length < 4)
                return BadRequest(new { message = "Password must be at least 4 characters" });
            if (await _context.Users.AnyAsync(u => u.Username == req.Username))
                return BadRequest(new { message = "Username exists" });

            _context.Users.Add(new User {
                Username = req.Username.Trim(),
                Password = AuthController.HashPassword(req.Password),
                Role = req.Role == "Admin" ? "Admin" : "Sales Staff"
            });
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("users/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] UserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Username and new password are required" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user == null) return NotFound(new { message = "User not found" });
            user.Password = AuthController.HashPassword(req.Password);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // --- REPORTS ---
        [HttpGet("reports/today")]
        public async Task<IActionResult> GetTodaySales()
        {
            var today = System.DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var sales = await _context.Sales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow).ToListAsync();
            return Ok(new {
                total = sales.Sum(s => s.TotalAmount),
                count = sales.Count
            });
        }

        [HttpGet("reports/deadstock")]
        public async Task<IActionResult> GetDeadStock()
        {
            var thirtyDaysAgo = System.DateTime.UtcNow.AddDays(-30);
            var soldIds = await _context.SaleItems
                .Where(si => si.Sale.SaleDate >= thirtyDaysAgo)
                .Select(si => si.VariantID).Distinct().ToListAsync();
            var deadStock = await _context.ProductVariants.Include(v => v.Product)
                .Where(v => v.StockQuantity > 0 && !soldIds.Contains(v.VariantID))
                .Select(v => new { v.Product.Name, v.Product.Category, v.Size, v.Color, v.StockQuantity, v.Product.Price })
                .ToListAsync();
            return Ok(deadStock);
        }
    }
}

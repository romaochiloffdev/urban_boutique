using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using UrbanBoutiqueWeb.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace UrbanBoutiqueWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        public class LoginRequest { public string Username { get; set; } public string Password { get; set; } }
        public class RegisterRequest { public string Username { get; set; } public string Password { get; set; } }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Username) || string.IsNullOrWhiteSpace(req?.Password))
                return BadRequest(new { success = false, message = "Username and password are required" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == req.Username);

            if (user == null || !VerifyPassword(req.Password, user.Password))
                return Unauthorized(new { success = false, message = "Invalid username or password" });

            HttpContext.Session.SetInt32("UserId", user.UserID);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);

            return Ok(new { success = true, username = user.Username, role = user.Role });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Username) || string.IsNullOrWhiteSpace(req?.Password))
                return BadRequest(new { success = false, message = "Username and password are required" });

            var username = req.Username.Trim();
            if (username.Length < 3)
                return BadRequest(new { success = false, message = "Username must be at least 3 characters" });
            if (req.Password.Length < 4)
                return BadRequest(new { success = false, message = "Password must be at least 4 characters" });

            if (await _context.Users.AnyAsync(u => u.Username == username))
                return BadRequest(new { success = false, message = "Username is already taken" });

            var user = new User
            {
                Username = username,
                Password = HashPassword(req.Password),
                Role = "Customer"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Auto-login after registration
            HttpContext.Session.SetInt32("UserId", user.UserID);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);

            return Ok(new { success = true, username = user.Username, role = user.Role });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { success = true });
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            var username = HttpContext.Session.GetString("Username");
            var role = HttpContext.Session.GetString("Role");
            if (username == null) return Unauthorized(new { authenticated = false });
            return Ok(new { authenticated = true, username, role });
        }

        // --- Password hashing: PBKDF2 with per-user salt ---
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashSize);

            return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;

            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2") return false;

            if (!int.TryParse(parts[1], out int iters)) return false;
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iters, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}

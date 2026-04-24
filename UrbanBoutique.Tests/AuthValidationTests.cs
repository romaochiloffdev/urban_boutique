// Author: Ochilov Ilyosjon (ID: B2300540)
// Tests for registration + login validation paths in AuthController,
// using EF Core's in-memory provider and a mock session.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueWeb.Controllers;
using UrbanBoutiqueWeb.Data;
using Xunit;

namespace UrbanBoutique.Tests
{
    public class AuthValidationTests : IDisposable
    {
        private readonly AppDbContext _db;

        public AuthValidationTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(options);
        }

        public void Dispose() => _db.Dispose();

        private AuthController NewController()
        {
            var controller = new AuthController(_db);
            // A minimal HttpContext with a fake session so tests can exercise SetString/GetString.
            controller.ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext { Session = new FakeSession() }
            };
            return controller;
        }

        // --- Registration ---

        [Theory]
        [InlineData(null, "password")]
        [InlineData("", "password")]
        [InlineData("user", null)]
        [InlineData("user", "")]
        public async Task Register_MissingField_ReturnsBadRequest(string? u, string? p)
        {
            var r = await NewController().Register(
                new AuthController.RegisterRequest { Username = u!, Password = p! });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task Register_ShortUsername_ReturnsBadRequest()
        {
            var r = await NewController().Register(
                new AuthController.RegisterRequest { Username = "ab", Password = "password" });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task Register_ShortPassword_ReturnsBadRequest()
        {
            var r = await NewController().Register(
                new AuthController.RegisterRequest { Username = "alice", Password = "x" });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task Register_Valid_CreatesCustomerAndStartsSession()
        {
            var controller = NewController();
            var r = await controller.Register(
                new AuthController.RegisterRequest { Username = "alice", Password = "alice123" });

            Assert.IsType<OkObjectResult>(r);
            var saved = _db.Users.Single();
            Assert.Equal("alice", saved.Username);
            Assert.Equal("Customer", saved.Role);
            Assert.StartsWith("pbkdf2$", saved.Password);

            // Session populated
            Assert.Equal("alice",    controller.HttpContext.Session.GetString("Username"));
            Assert.Equal("Customer", controller.HttpContext.Session.GetString("Role"));
        }

        [Fact]
        public async Task Register_DuplicateUsername_ReturnsBadRequest()
        {
            _db.Users.Add(new User {
                Username = "bob",
                Password = AuthController.HashPassword("pw"),
                Role = "Customer"
            });
            _db.SaveChanges();

            var r = await NewController().Register(
                new AuthController.RegisterRequest { Username = "bob", Password = "another" });

            Assert.IsType<BadRequestObjectResult>(r);
            Assert.Equal(1, _db.Users.Count());
        }

        // --- Login ---

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOk_AndPopulatesSession()
        {
            _db.Users.Add(new User {
                Username = "admin",
                Password = AuthController.HashPassword("secret"),
                Role = "Admin"
            });
            _db.SaveChanges();

            var controller = NewController();
            var r = await controller.Login(
                new AuthController.LoginRequest { Username = "admin", Password = "secret" });

            Assert.IsType<OkObjectResult>(r);
            Assert.Equal("Admin", controller.HttpContext.Session.GetString("Role"));
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            _db.Users.Add(new User {
                Username = "admin",
                Password = AuthController.HashPassword("secret"),
                Role = "Admin"
            });
            _db.SaveChanges();

            var r = await NewController().Login(
                new AuthController.LoginRequest { Username = "admin", Password = "bad" });
            Assert.IsType<UnauthorizedObjectResult>(r);
        }

        [Fact]
        public async Task Login_UnknownUser_ReturnsUnauthorized()
        {
            var r = await NewController().Login(
                new AuthController.LoginRequest { Username = "ghost", Password = "p" });
            Assert.IsType<UnauthorizedObjectResult>(r);
        }

        [Theory]
        [InlineData(null, "p")]
        [InlineData("u", null)]
        [InlineData("", "")]
        public async Task Login_MissingField_ReturnsBadRequest(string? u, string? p)
        {
            var r = await NewController().Login(
                new AuthController.LoginRequest { Username = u!, Password = p! });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public void Me_WithoutSession_ReturnsUnauthorized()
        {
            var r = NewController().Me();
            Assert.IsType<UnauthorizedObjectResult>(r);
        }

        [Fact]
        public void Me_WithSession_ReturnsOk()
        {
            var controller = NewController();
            controller.HttpContext.Session.SetString("Username", "alice");
            controller.HttpContext.Session.SetString("Role", "Customer");
            var r = controller.Me();
            Assert.IsType<OkObjectResult>(r);
        }
    }

    /// <summary>
    /// Minimal <see cref="ISession"/> stub that keeps values in a dictionary for tests.
    /// </summary>
    internal class FakeSession : ISession
    {
        private readonly System.Collections.Generic.Dictionary<string, byte[]> _store = new();
        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(System.Threading.CancellationToken token = default) => Task.CompletedTask;
        public Task LoadAsync(System.Threading.CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value)
        {
            if (_store.TryGetValue(key, out var v)) { value = v; return true; }
            value = null!; return false;
        }
    }
}

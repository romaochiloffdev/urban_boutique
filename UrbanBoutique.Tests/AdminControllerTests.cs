// Author: Ochilov Ilyosjon (ID: B2300540)
// Tests for the Admin product / category / user endpoints validation.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueWeb.Controllers;
using UrbanBoutiqueWeb.Data;
using Xunit;

namespace UrbanBoutique.Tests
{
    public class AdminControllerTests : IDisposable
    {
        private readonly AppDbContext _db;

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(options);
        }

        public void Dispose() => _db.Dispose();
        private AdminController NewController() => new(_db);

        // --- Product validation ---

        [Theory]
        [InlineData(null, 10, 5)]           // null name
        [InlineData("",   10, 5)]           // empty name
        [InlineData("   ", 10, 5)]          // whitespace
        public async Task AddProduct_InvalidName_ReturnsBadRequest(string? name, decimal price, int stock)
        {
            var r = await NewController().AddProduct(new AdminController.ProductRequest {
                Name = name!, Price = price, Category = "Clothing",
                Size = "M", Color = "Black", StockQuantity = stock
            });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task AddProduct_NonPositivePrice_ReturnsBadRequest(decimal price)
        {
            var r = await NewController().AddProduct(new AdminController.ProductRequest {
                Name = "X", Price = price, Category = "C",
                Size = "M", Color = "Black", StockQuantity = 1
            });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task AddProduct_NegativeStock_ReturnsBadRequest()
        {
            var r = await NewController().AddProduct(new AdminController.ProductRequest {
                Name = "X", Price = 10m, Category = "C",
                Size = "M", Color = "Black", StockQuantity = -1
            });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task AddProduct_Valid_PersistsWithVariant()
        {
            var r = await NewController().AddProduct(new AdminController.ProductRequest {
                Name = "  Leather Jacket  ",   // will be trimmed
                Price = 199.99m,
                Category = "Clothing",
                Size = "L", Color = "Brown", StockQuantity = 7
            });

            Assert.IsType<OkObjectResult>(r);

            var saved = await _db.Products.Include(p => p.Variants).SingleAsync();
            Assert.Equal("Leather Jacket", saved.Name);
            Assert.Equal(199.99m, saved.Price);
            var variant = Assert.Single(saved.Variants);
            Assert.Equal(7, variant.StockQuantity);
        }

        // --- Category ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddCategory_InvalidName_ReturnsBadRequest(string? name)
        {
            var r = await NewController().AddCategory(
                new AdminController.CategoryRequest { Name = name! });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task AddCategory_DuplicateName_ReturnsBadRequest()
        {
            _db.Categories.Add(new Category { Name = "Jewelry" });
            _db.SaveChanges();

            var r = await NewController().AddCategory(
                new AdminController.CategoryRequest { Name = "Jewelry" });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task AddCategory_Valid_Persists()
        {
            var r = await NewController().AddCategory(
                new AdminController.CategoryRequest { Name = "Jewelry" });
            Assert.IsType<OkObjectResult>(r);
            Assert.Single(_db.Categories);
        }

        // --- User ---

        [Fact]
        public async Task AddUser_Duplicate_ReturnsBadRequest()
        {
            _db.Users.Add(new User {
                Username = "bob",
                Password = AuthController.HashPassword("pw"),
                Role = "Customer"
            });
            _db.SaveChanges();

            var r = await NewController().AddUser(new AdminController.UserRequest {
                Username = "bob", Password = "newpw", Role = "Admin"
            });
            Assert.IsType<BadRequestObjectResult>(r);
        }

        [Fact]
        public async Task AddUser_RoleCoercion_NonAdminBecomesSalesStaff()
        {
            var r = await NewController().AddUser(new AdminController.UserRequest {
                Username = "worker", Password = "pw123", Role = "Hacker"   // not a known role
            });
            Assert.IsType<OkObjectResult>(r);

            var saved = _db.Users.Single();
            Assert.Equal("Sales Staff", saved.Role);   // anything not "Admin" is coerced
        }

        [Fact]
        public async Task ResetPassword_UnknownUser_ReturnsNotFound()
        {
            var r = await NewController().ResetPassword(new AdminController.UserRequest {
                Username = "ghost", Password = "pw123"
            });
            Assert.IsType<NotFoundObjectResult>(r);
        }
    }
}

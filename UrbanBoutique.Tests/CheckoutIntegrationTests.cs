// Author: Ochilov Ilyosjon (ID: B2300540)
// End-to-end tests for the Cashier checkout flow using EF Core's in-memory provider.
// These validate Algorithm 2 from PseudoCode.md: stock is deducted atomically,
// Sale + SaleItem rows are persisted, and invalid carts are rejected.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueWeb.Controllers;
using UrbanBoutiqueWeb.Data;
using Xunit;

namespace UrbanBoutique.Tests
{
    public class CheckoutIntegrationTests : IDisposable
    {
        private readonly AppDbContext _db;

        public CheckoutIntegrationTests()
        {
            // Each test gets a fresh, isolated in-memory DB.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics
                        .InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new AppDbContext(options);

            SeedProducts();
        }

        public void Dispose() => _db.Dispose();

        private void SeedProducts()
        {
            var shirt = new Product { Name = "T-Shirt", Price = 20m, Category = "Clothing" };
            shirt.Variants.Add(new ProductVariant { Size = "M", Color = "Black", StockQuantity = 10 });
            shirt.Variants.Add(new ProductVariant { Size = "L", Color = "Navy Blue", StockQuantity = 3 });
            _db.Products.Add(shirt);
            _db.SaveChanges();
        }

        private CashierController NewController() => new CashierController(_db);

        [Fact]
        public async Task Checkout_EmptyCart_ReturnsBadRequest()
        {
            var result = await NewController().Checkout(
                new CashierController.CheckoutRequest { Items = new List<CashierController.CartItem>() });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Checkout_NullRequest_ReturnsBadRequest()
        {
            var result = await NewController().Checkout(null!);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Checkout_ZeroQuantity_ReturnsBadRequest()
        {
            var result = await NewController().Checkout(new CashierController.CheckoutRequest {
                Items = new List<CashierController.CartItem> {
                    new() { VariantID = 1, Quantity = 0 }
                }
            });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Checkout_NegativeQuantity_ReturnsBadRequest()
        {
            var result = await NewController().Checkout(new CashierController.CheckoutRequest {
                Items = new List<CashierController.CartItem> {
                    new() { VariantID = 1, Quantity = -3 }
                }
            });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Checkout_InsufficientStock_ReturnsBadRequest_StockUnchanged()
        {
            // Variant 2 has only 3 in stock
            var variantBefore = await _db.ProductVariants.FindAsync(2);
            var initialStock = variantBefore!.StockQuantity;

            var result = await NewController().Checkout(new CashierController.CheckoutRequest {
                Items = new List<CashierController.CartItem> {
                    new() { VariantID = 2, Quantity = 5 }      // > 3
                }
            });

            Assert.IsType<BadRequestObjectResult>(result);

            var variantAfter = await _db.ProductVariants.FindAsync(2);
            Assert.Equal(initialStock, variantAfter!.StockQuantity);
            Assert.Empty(_db.Sales);
        }

        [Fact]
        public async Task Checkout_Valid_DecrementsStockAndCreatesSale()
        {
            var result = await NewController().Checkout(new CashierController.CheckoutRequest {
                Items = new List<CashierController.CartItem> {
                    new() { VariantID = 1, Quantity = 2 },
                    new() { VariantID = 2, Quantity = 1 }
                }
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);

            // Stock deducted
            Assert.Equal(8, (await _db.ProductVariants.FindAsync(1))!.StockQuantity);  // 10-2
            Assert.Equal(2, (await _db.ProductVariants.FindAsync(2))!.StockQuantity);  // 3-1

            // Sale persisted
            var sale = _db.Sales.Include(s => s.SaleItems).Single();
            Assert.Equal(3 * 20m, sale.TotalAmount);          // 3 items × $20
            Assert.Equal(2, sale.SaleItems.Count);
        }

        [Fact]
        public async Task Checkout_UnknownVariant_ReturnsBadRequest()
        {
            var result = await NewController().Checkout(new CashierController.CheckoutRequest {
                Items = new List<CashierController.CartItem> {
                    new() { VariantID = 9999, Quantity = 1 }
                }
            });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(_db.Sales);
        }

        [Fact]
        public async Task GetProduct_ExistingVariant_ReturnsOk()
        {
            var result = await NewController().GetProduct(1);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetProduct_Missing_ReturnsNotFound()
        {
            var result = await NewController().GetProduct(9999);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetAvailableProducts_FiltersByStockAndSearch()
        {
            _db.Products.Add(new Product {
                Name = "Sunglasses", Price = 30m, Category = "Accessories",
                Variants = new List<ProductVariant> {
                    new() { Size = "One Size", Color = "Black", StockQuantity = 0 },   // out of stock
                    new() { Size = "One Size", Color = "Gold",  StockQuantity = 5 }
                }
            });
            _db.SaveChanges();

            var ok = Assert.IsType<OkObjectResult>(
                await NewController().GetAvailableProducts("sunglass"));

            var items = (System.Collections.IEnumerable)ok.Value!;
            // Only Gold (in-stock) should be returned
            var count = items.Cast<object>().Count();
            Assert.Equal(1, count);
        }
    }
}

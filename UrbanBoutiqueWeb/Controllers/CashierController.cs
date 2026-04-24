using Microsoft.AspNetCore.Mvc;
using UrbanBoutiqueWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace UrbanBoutiqueWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CashierController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CashierController(AppDbContext context)
        {
            _context = context;
        }

        // Public product list (used by storefront and cashier)
        [HttpGet("products")]
        public async Task<IActionResult> GetAvailableProducts([FromQuery] string? search)
        {
            var query = _context.ProductVariants.Include(v => v.Product).Where(v => v.StockQuantity > 0);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(v => v.Product.Name.ToLower().Contains(term) ||
                                         v.Product.Category.ToLower().Contains(term));
            }

            var result = await query.Select(v => new {
                v.VariantID, ProductName = v.Product.Name, v.Product.Category, v.Product.Price,
                v.Size, v.Color, v.StockQuantity
            }).ToListAsync();

            return Ok(result);
        }

        [HttpGet("products/{variantId}")]
        public async Task<IActionResult> GetProduct(int variantId)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.VariantID == variantId);

            if (variant == null) return NotFound(new { message = "Product not found" });

            return Ok(new {
                variant.VariantID,
                ProductName = variant.Product.Name,
                variant.Product.Category,
                variant.Product.Price,
                variant.Size,
                variant.Color,
                variant.StockQuantity
            });
        }

        public class CheckoutRequest { public List<CartItem> Items { get; set; } = new(); }
        public class CartItem { public int VariantID { get; set; } public int Quantity { get; set; } }

        [HttpPost("checkout")]
        [SessionAuth]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req)
        {
            if (req?.Items == null || req.Items.Count == 0)
                return BadRequest(new { message = "Cart is empty" });

            if (req.Items.Any(i => i.Quantity <= 0))
                return BadRequest(new { message = "Quantity must be positive" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalAmount = 0;
                var newSale = new Sale { SaleDate = DateTime.UtcNow };
                _context.Sales.Add(newSale);

                foreach (var item in req.Items)
                {
                    var variant = await _context.ProductVariants
                        .Include(v => v.Product)
                        .FirstOrDefaultAsync(v => v.VariantID == item.VariantID);

                    if (variant == null)
                        throw new Exception($"Variant {item.VariantID} not found");
                    if (variant.StockQuantity < item.Quantity)
                        throw new Exception($"Insufficient stock for {variant.Product.Name}. Only {variant.StockQuantity} left.");

                    variant.StockQuantity -= item.Quantity;
                    totalAmount += variant.Product.Price * item.Quantity;

                    newSale.SaleItems.Add(new SaleItem {
                        VariantID = variant.VariantID,
                        Quantity = item.Quantity,
                        Price = variant.Product.Price
                    });
                }

                newSale.TotalAmount = totalAmount;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, total = totalAmount, saleId = newSale.SaleID });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

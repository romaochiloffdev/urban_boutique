using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UrbanBoutiqueAdmin.Data
{
    [Table("Users")]
    public class User
    {
        [Key] public int UserID { get; set; }
        [Required][MaxLength(50)] public string Username { get; set; } = "";
        [Required][MaxLength(255)] public string Password { get; set; } = "";
        [Required][MaxLength(20)] public string Role { get; set; } = "";
    }

    [Table("Categories")]
    public class Category
    {
        [Key] public int CategoryID { get; set; }
        [Required][MaxLength(50)] public string Name { get; set; } = "";
    }

    [Table("Products")]
    public class Product
    {
        [Key] public int ProductID { get; set; }
        [Required][MaxLength(100)] public string Name { get; set; } = "";
        [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
        [MaxLength(50)] public string Category { get; set; } = "";
        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }

    [Table("ProductVariants")]
    public class ProductVariant
    {
        [Key] public int VariantID { get; set; }
        public int ProductID { get; set; }
        [ForeignKey("ProductID")] public virtual Product Product { get; set; } = null!;
        [MaxLength(10)] public string Size { get; set; } = "";
        [MaxLength(50)] public string Color { get; set; } = "";
        public int StockQuantity { get; set; }
    }

    [Table("Sales")]
    public class Sale
    {
        [Key] public int SaleID { get; set; }
        public DateTime SaleDate { get; set; } = DateTime.UtcNow;
        [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }

    [Table("SaleItems")]
    public class SaleItem
    {
        [Key] public int SaleItemID { get; set; }
        public int SaleID { get; set; }
        [ForeignKey("SaleID")] public virtual Sale Sale { get; set; } = null!;
        public int VariantID { get; set; }
        [ForeignKey("VariantID")] public virtual ProductVariant Variant { get; set; } = null!;
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<SaleItem> SaleItems { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(DbConfig.ConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();
        }
    }
}

// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Reports Module

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueAdmin.Data;

namespace UrbanBoutiqueAdmin
{
    public partial class ReportsWindow : Window
    {
        public ObservableCollection<DeadStockDisplayModel> DeadStockList { get; set; } = new();

        public ReportsWindow()
        {
            InitializeComponent();
            dgDeadStock.ItemsSource = DeadStockList;
            LoadReports();
        }

        private void LoadReports()
        {
            try
            {
                using var db = new AppDbContext();

                // Today's sales & count (timestamps are stored in UTC)
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);
                var salesToday = db.Sales
                    .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow)
                    .ToList();

                txtTodaySales.Text = $"${salesToday.Sum(s => s.TotalAmount):F2}";
                txtTodayCount.Text = salesToday.Count.ToString();

                // Dead stock
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var soldIds = db.SaleItems
                    .Where(si => si.Sale.SaleDate >= cutoff)
                    .Select(si => si.VariantID)
                    .Distinct()
                    .ToList();

                var deadStock = db.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => v.StockQuantity > 0 && !soldIds.Contains(v.VariantID))
                    .ToList();

                foreach (var v in deadStock)
                    DeadStockList.Add(new DeadStockDisplayModel
                    {
                        ProductName = v.Product.Name,
                        Category = v.Product.Category,
                        Size = v.Size,
                        Color = v.Color,
                        StockQuantity = v.StockQuantity,
                        Price = v.Product.Price
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading reports: " + ex.Message,
                    "Report Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class DeadStockDisplayModel
    {
        public string ProductName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public int StockQuantity { get; set; }
        public decimal Price { get; set; }
    }
}

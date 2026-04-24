// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Admin Panel

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueAdmin.Data;

namespace UrbanBoutiqueAdmin
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ProductDisplayModel> ProductsList { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();

            if (!CurrentUser.IsAuthenticated || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access denied. Admin privileges required.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            dgProducts.ItemsSource = ProductsList;
            txtUserName.Text = CurrentUser.Username;
            LoadCategories();
            LoadProductsFromDatabase();
        }

        private void LoadCategories()
        {
            try
            {
                using var db = new AppDbContext();
                var cats = db.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToList();
                cmbCategory.Items.Clear();
                foreach (var c in cats) cmbCategory.Items.Add(c);
            }
            catch { /* Non-critical */ }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProductName.Text))
            {
                Warn("Please enter product name.");
                return;
            }
            if (!decimal.TryParse(txtPrice.Text, out var price) || price <= 0)
            {
                Warn("Please enter a valid positive price.");
                return;
            }
            if (!int.TryParse(txtStockQuantity.Text, out var stock) || stock < 0)
            {
                Warn("Please enter a valid stock quantity.");
                return;
            }
            if (cmbCategory.SelectedItem == null)
            {
                Warn("Please select a category.");
                return;
            }
            if (cmbSize.SelectedItem == null)
            {
                Warn("Please select a size.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var product = new Product
                {
                    Name = txtProductName.Text.Trim(),
                    Price = price,
                    Category = cmbCategory.SelectedItem.ToString() ?? ""
                };
                product.Variants.Add(new ProductVariant
                {
                    Size = (cmbSize.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "M",
                    Color = string.IsNullOrWhiteSpace(txtColor.Text) ? "-" : txtColor.Text.Trim(),
                    StockQuantity = stock
                });

                db.Products.Add(product);
                db.SaveChanges();

                MessageBox.Show("Product saved successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadProductsFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProductsFromDatabase()
        {
            ProductsList.Clear();
            try
            {
                using var db = new AppDbContext();
                var products = db.Products.Include(p => p.Variants).ToList();
                foreach (var p in products)
                    foreach (var v in p.Variants)
                        ProductsList.Add(new ProductDisplayModel
                        {
                            ProductName = p.Name,
                            Category = p.Category,
                            Price = p.Price,
                            Size = v.Size,
                            Color = v.Color,
                            StockQuantity = v.StockQuantity,
                            IsLowStock = v.StockQuantity < 5
                        });
            }
            catch { /* tables may not exist on first run */ }
        }

        private void ClearForm()
        {
            txtProductName.Clear();
            txtPrice.Clear();
            txtColor.Clear();
            txtStockQuantity.Clear();
            cmbCategory.SelectedIndex = -1;
            cmbSize.SelectedIndex = -1;
        }

        private void Warn(string msg) =>
            MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            new ReportsWindow { Owner = this }.ShowDialog();
        }

        private void BtnUserManagement_Click(object sender, RoutedEventArgs e)
        {
            new UserManagementWindow { Owner = this }.ShowDialog();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            CurrentUser.SignOut();
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            this.Close();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadCategories();
            LoadProductsFromDatabase();
        }
    }

    public class ProductDisplayModel
    {
        public string ProductName { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Price { get; set; }
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public int StockQuantity { get; set; }
        public bool IsLowStock { get; set; }
    }
}

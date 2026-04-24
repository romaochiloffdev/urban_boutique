// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Admin Panel
//
// ==============================================================================
// This file implements the administrator dashboard. Admins can:
//     - Add new products (with a single size/colour variant).
//     - See the current inventory with a low-stock visual indicator.
//     - Open the Reports window for sales and dead-stock analytics.
//     - Open the Staff Management window to add users or reset passwords.
//     - Sign out and return to the login screen.
//
// Design notes:
// - UI ↔ data binding is done through an <see cref="ObservableCollection{T}"/>
//   so changes made by <c>LoadProductsFromDatabase</c> immediately update the
//   DataGrid without manual refresh logic.
// - Category options are loaded from the database at start-up so the dropdown
//   reflects the current catalog instead of a hard-coded list.
// ==============================================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UrbanBoutiqueAdmin.Data;

namespace UrbanBoutiqueAdmin
{
    /// <summary>
    /// Main administrator dashboard. Access is gated by <see cref="CurrentUser"/>
    /// being populated with an <c>Admin</c> role before construction.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Products displayed in the inventory grid. Bound to
        /// <c>dgProducts.ItemsSource</c> — any add/remove becomes a UI update.
        /// </summary>
        public ObservableCollection<ProductDisplayModel> ProductsList { get; } = new();

        /// <summary>
        /// Constructs the dashboard and performs the authorisation check.
        /// If the current session is not an administrator the application is
        /// shut down immediately — defence in depth, since the login window
        /// already prevents this path in practice.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            if (!CurrentUser.IsAuthenticated || !CurrentUser.IsAdmin)
            {
                FileLogger.Warn("Unauthenticated access attempt to MainWindow.");
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

        /// <summary>
        /// Populates the category dropdown from the <c>Categories</c> table
        /// in alphabetical order. Fails silently if the table isn't ready —
        /// the dropdown will simply be empty.
        /// </summary>
        private void LoadCategories()
        {
            try
            {
                using var db = new AppDbContext();
                var cats = db.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToList();
                cmbCategory.Items.Clear();
                foreach (var c in cats) cmbCategory.Items.Add(c);
            }
            catch (Exception ex)
            {
                FileLogger.Warn("Could not load categories: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles the "Save Product" button. Validates the form, persists
        /// a new <see cref="Product"/> with one variant, then reloads the grid.
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // --- Validate all required fields ---
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

                // --- Build the new product graph ---
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
                FileLogger.Info($"Added product '{product.Name}' (stock: {stock}).");

                MessageBox.Show("Product saved successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadProductsFromDatabase();
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to save product.", ex);
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reloads the inventory grid from the database. Flattens the
        /// Product ↔ Variant relationship into a single row per variant
        /// so each size/colour combination is visible to the admin.
        /// </summary>
        private void LoadProductsFromDatabase()
        {
            ProductsList.Clear();
            try
            {
                using var db = new AppDbContext();
                var products = db.Products.Include(p => p.Variants).ToList();

                // Flatten: one row per variant.
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
                            IsLowStock = v.StockQuantity < 5     // threshold drives the red row style
                        });
            }
            catch (Exception ex)
            {
                FileLogger.Warn("Could not load products: " + ex.Message);
            }
        }

        /// <summary>Clears all input fields after a successful save.</summary>
        private void ClearForm()
        {
            txtProductName.Clear();
            txtPrice.Clear();
            txtColor.Clear();
            txtStockQuantity.Clear();
            cmbCategory.SelectedIndex = -1;
            cmbSize.SelectedIndex = -1;
        }

        /// <summary>Shows a warning dialog with the supplied message.</summary>
        private void Warn(string msg) =>
            MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);

        /// <summary>Opens the Reports window as a modal dialog.</summary>
        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            new ReportsWindow { Owner = this }.ShowDialog();
        }

        /// <summary>Opens the Staff Management window as a modal dialog.</summary>
        private void BtnUserManagement_Click(object sender, RoutedEventArgs e)
        {
            new UserManagementWindow { Owner = this }.ShowDialog();
        }

        /// <summary>
        /// Signs the current admin out and returns to the login window.
        /// Uses <c>Application.Current.MainWindow</c> re-assignment so that
        /// closing this window does not also terminate the application.
        /// </summary>
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            FileLogger.Info($"Admin '{CurrentUser.Username}' signed out.");
            CurrentUser.SignOut();
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            this.Close();
        }

        /// <summary>Refreshes both categories and products from the database.</summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadCategories();
            LoadProductsFromDatabase();
        }
    }

    /// <summary>
    /// View-model for a single row in the inventory grid. Intentionally a
    /// flat DTO rather than a full <see cref="Product"/> + <see cref="ProductVariant"/>
    /// because WPF bindings are simpler against scalar properties.
    /// </summary>
    public class ProductDisplayModel
    {
        public string ProductName { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Price { get; set; }
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public int StockQuantity { get; set; }

        /// <summary>
        /// <c>true</c> when this variant has fewer than five items left.
        /// Used by a DataTrigger in XAML to highlight the row in red.
        /// </summary>
        public bool IsLowStock { get; set; }
    }
}

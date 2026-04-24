// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Front Office (Cashier)

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Npgsql;

namespace UrbanBoutiqueCashier
{
    public partial class MainWindow : Window
    {
        private readonly string connectionString = DbConfig.ConnectionString;

        public ObservableCollection<ProductDisplayModel> AvailableProducts { get; set; }
        public ObservableCollection<CartItemModel> ShoppingCart { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            AvailableProducts = new ObservableCollection<ProductDisplayModel>();
            ShoppingCart = new ObservableCollection<CartItemModel>();

            dgAvailableProducts.ItemsSource = AvailableProducts;
            dgCart.ItemsSource = ShoppingCart;

            LoadAvailableProducts();
        }

        private void LoadAvailableProducts(string searchQuery = "")
        {
            AvailableProducts.Clear();

            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();

                var query = @"SELECT p.""Name"", p.""Category"", p.""Price"",
                                     v.""VariantID"", v.""Size"", v.""Color"", v.""StockQuantity""
                              FROM ""Products"" p
                              INNER JOIN ""ProductVariants"" v ON p.""ProductID"" = v.""ProductID""
                              WHERE v.""StockQuantity"" > 0";

                if (!string.IsNullOrWhiteSpace(searchQuery))
                    query += @" AND (p.""Name"" ILIKE @Search OR p.""Category"" ILIKE @Search)";

                query += @" ORDER BY p.""Name""";

                using var cmd = new NpgsqlCommand(query, conn);
                if (!string.IsNullOrWhiteSpace(searchQuery))
                    cmd.Parameters.AddWithValue("@Search", $"%{searchQuery}%");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    AvailableProducts.Add(new ProductDisplayModel
                    {
                        VariantID = Convert.ToInt32(reader["VariantID"]),
                        ProductName = reader["Name"].ToString() ?? "",
                        Category = reader["Category"].ToString() ?? "",
                        Price = Convert.ToDecimal(reader["Price"]),
                        Size = reader["Size"].ToString() ?? "",
                        Color = reader["Color"].ToString() ?? "",
                        StockQuantity = Convert.ToInt32(reader["StockQuantity"])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database connection error: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e) =>
            LoadAvailableProducts(txtSearch.Text);

        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (dgAvailableProducts.SelectedItem is not ProductDisplayModel selected)
            {
                MessageBox.Show("Please select a product to add.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var existing = ShoppingCart.FirstOrDefault(c => c.VariantID == selected.VariantID);
            if (existing != null)
            {
                if (existing.Quantity >= selected.StockQuantity)
                {
                    MessageBox.Show("Not enough stock available.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                existing.Quantity++;
            }
            else
            {
                ShoppingCart.Add(new CartItemModel
                {
                    VariantID = selected.VariantID,
                    ProductName = $"{selected.ProductName} ({selected.Size}, {selected.Color})",
                    Price = selected.Price,
                    Quantity = 1
                });
            }

            UpdateTotalAmount();
        }

        private void BtnRemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (dgCart.SelectedItem is not CartItemModel selected)
            {
                MessageBox.Show("Please select an item to remove.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.Quantity > 1) selected.Quantity--;
            else ShoppingCart.Remove(selected);

            UpdateTotalAmount();
            dgCart.Items.Refresh();
        }

        private void UpdateTotalAmount()
        {
            decimal total = ShoppingCart.Sum(item => item.Subtotal);
            txtTotalAmount.Text = $"${total:F2}";
        }

        private void BtnCheckout_Click(object sender, RoutedEventArgs e)
        {
            if (ShoppingCart.Count == 0)
            {
                MessageBox.Show("Shopping cart is empty.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var conn = new NpgsqlConnection(connectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database connection error: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. Create a Sale record first
                decimal totalAmount = 0;
                int saleId;

                using (var cmdSale = new NpgsqlCommand(
                    @"INSERT INTO ""Sales"" (""SaleDate"", ""TotalAmount"") VALUES (@d, @t) RETURNING ""SaleID""",
                    conn, transaction))
                {
                    cmdSale.Parameters.AddWithValue("@d", DateTime.UtcNow);
                    cmdSale.Parameters.AddWithValue("@t", 0m); // will update later
                    saleId = Convert.ToInt32(cmdSale.ExecuteScalar());
                }

                // 2. For each cart item: lock stock, validate, update, insert SaleItem
                foreach (var item in ShoppingCart)
                {
                    int currentStock;
                    decimal currentPrice;

                    using (var cmdCheck = new NpgsqlCommand(
                        @"SELECT v.""StockQuantity"", p.""Price""
                          FROM ""ProductVariants"" v
                          INNER JOIN ""Products"" p ON v.""ProductID"" = p.""ProductID""
                          WHERE v.""VariantID"" = @id FOR UPDATE",
                        conn, transaction))
                    {
                        cmdCheck.Parameters.AddWithValue("@id", item.VariantID);
                        using var reader = cmdCheck.ExecuteReader();
                        if (!reader.Read())
                            throw new Exception($"Variant {item.VariantID} not found");
                        currentStock = reader.GetInt32(0);
                        currentPrice = reader.GetDecimal(1);
                    }

                    if (currentStock < item.Quantity)
                        throw new Exception($"Insufficient stock for {item.ProductName}. Only {currentStock} left.");

                    using (var cmdUpdate = new NpgsqlCommand(
                        @"UPDATE ""ProductVariants"" SET ""StockQuantity"" = ""StockQuantity"" - @q WHERE ""VariantID"" = @id",
                        conn, transaction))
                    {
                        cmdUpdate.Parameters.AddWithValue("@q", item.Quantity);
                        cmdUpdate.Parameters.AddWithValue("@id", item.VariantID);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    using (var cmdItem = new NpgsqlCommand(
                        @"INSERT INTO ""SaleItems"" (""SaleID"", ""VariantID"", ""Quantity"", ""Price"") VALUES (@s, @v, @q, @p)",
                        conn, transaction))
                    {
                        cmdItem.Parameters.AddWithValue("@s", saleId);
                        cmdItem.Parameters.AddWithValue("@v", item.VariantID);
                        cmdItem.Parameters.AddWithValue("@q", item.Quantity);
                        cmdItem.Parameters.AddWithValue("@p", currentPrice);
                        cmdItem.ExecuteNonQuery();
                    }

                    totalAmount += currentPrice * item.Quantity;
                }

                // 3. Update Sale total
                using (var cmdUpdTotal = new NpgsqlCommand(
                    @"UPDATE ""Sales"" SET ""TotalAmount"" = @t WHERE ""SaleID"" = @id",
                    conn, transaction))
                {
                    cmdUpdTotal.Parameters.AddWithValue("@t", totalAmount);
                    cmdUpdTotal.Parameters.AddWithValue("@id", saleId);
                    cmdUpdTotal.ExecuteNonQuery();
                }

                transaction.Commit();

                MessageBox.Show($"Sale #{saleId} completed. Total: ${totalAmount:F2}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ShoppingCart.Clear();
                UpdateTotalAmount();
                LoadAvailableProducts(txtSearch.Text);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show("Transaction failed: " + ex.Message,
                    "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ProductDisplayModel
    {
        public int VariantID { get; set; }
        public string ProductName { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Price { get; set; }
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public int StockQuantity { get; set; }
    }

    public class CartItemModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int VariantID { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtotal)));
            }
        }

        public decimal Subtotal => Price * Quantity;
    }
}

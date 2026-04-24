// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Front Office (Cashier)
//
// ==============================================================================
// This file implements the cashier terminal. The checkout routine is the most
// safety-critical piece of the system — two cashiers could be ringing up the
// last item at the same time, so stock must be checked under a row-level lock
// and either fully applied or fully rolled back.
//
// Algorithm 1 (AddToCart)      — see PseudoCode.md
// Algorithm 2 (CompleteCheckout) — see PseudoCode.md
//
// Key techniques used in this file:
//   • ObservableCollection<T> + INotifyPropertyChanged for live UI updates
//   • Parameterised SQL via Npgsql (prevents SQL injection)
//   • PostgreSQL row locks (SELECT ... FOR UPDATE) to serialise concurrent
//     cashier updates
//   • Multi-statement transaction with explicit COMMIT / ROLLBACK
// ==============================================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Npgsql;

namespace UrbanBoutiqueCashier
{
    /// <summary>
    /// The cashier terminal. Provides product search, a live shopping cart and
    /// a transactional checkout routine that persists a <c>Sale</c> row and its
    /// <c>SaleItems</c> children while deducting stock.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Connection string is read from <c>App.config</c> so it can be
        /// overridden per-environment without rebuilding the app.
        /// </summary>
        private readonly string connectionString = DbConfig.ConnectionString;

        /// <summary>Products the cashier can add to the cart.</summary>
        public ObservableCollection<ProductDisplayModel> AvailableProducts { get; }
            = new ObservableCollection<ProductDisplayModel>();

        /// <summary>Current shopping cart contents.</summary>
        public ObservableCollection<CartItemModel> ShoppingCart { get; }
            = new ObservableCollection<CartItemModel>();

        /// <summary>
        /// Wires up the two DataGrids to their collections and loads the
        /// initial product list.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            dgAvailableProducts.ItemsSource = AvailableProducts;
            dgCart.ItemsSource = ShoppingCart;

            LoadAvailableProducts();
        }

        /// <summary>
        /// Loads every in-stock product variant from the database, optionally
        /// filtered by a product name or category search term. Uses a
        /// parameterised query with <c>ILIKE</c> for case-insensitive matching.
        /// </summary>
        /// <param name="searchQuery">Optional search term; empty to load all.</param>
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
                FileLogger.Error("Failed to load products.", ex);
                MessageBox.Show("Database connection error: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Handler for the search button — reloads with the typed filter.</summary>
        private void BtnSearch_Click(object sender, RoutedEventArgs e) =>
            LoadAvailableProducts(txtSearch.Text);

        /// <summary>
        /// Algorithm 1 (AddToCart) from <c>PseudoCode.md</c>.
        /// Validates that adding one more unit of the selected product won't
        /// exceed the current on-hand stock, then either increments the
        /// existing cart line or inserts a new one.
        /// </summary>
        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (dgAvailableProducts.SelectedItem is not ProductDisplayModel selected)
            {
                MessageBox.Show("Please select a product to add.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
                existing.Quantity++;                                      // triggers PropertyChanged → UI updates
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

        /// <summary>
        /// Decrements the quantity of the selected cart line, or removes it
        /// entirely when the quantity reaches zero.
        /// </summary>
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

        /// <summary>Recomputes the cart total and updates the footer label.</summary>
        private void UpdateTotalAmount()
        {
            decimal total = ShoppingCart.Sum(item => item.Subtotal);
            txtTotalAmount.Text = $"${total:F2}";
        }

        /// <summary>
        /// Algorithm 2 (CompleteCheckout) from <c>PseudoCode.md</c>. Runs the
        /// entire sale inside a single database transaction:
        ///   1. INSERT the parent <c>Sales</c> row and capture its id.
        ///   2. For every cart item: re-read the stock under a <c>FOR UPDATE</c>
        ///      lock (serialises concurrent cashiers), verify it's still enough,
        ///      decrement the stock and INSERT the matching <c>SaleItems</c> row.
        ///   3. UPDATE the <c>Sales.TotalAmount</c> with the final total.
        ///   4. COMMIT. On any exception, ROLLBACK so nothing persists.
        ///
        /// Notably the price stored on the <c>SaleItems</c> row is read from the
        /// database — the client cannot tamper it via the UI.
        /// </summary>
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
                FileLogger.Error("Could not open DB connection for checkout.", ex);
                MessageBox.Show("Database connection error: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using var transaction = conn.BeginTransaction();
            try
            {
                // ---- 1. Create a Sale record first (TotalAmount filled in at the end) ----
                decimal totalAmount = 0;
                int saleId;

                using (var cmdSale = new NpgsqlCommand(
                    @"INSERT INTO ""Sales"" (""SaleDate"", ""TotalAmount"") VALUES (@d, @t) RETURNING ""SaleID""",
                    conn, transaction))
                {
                    cmdSale.Parameters.AddWithValue("@d", DateTime.UtcNow);
                    cmdSale.Parameters.AddWithValue("@t", 0m);
                    saleId = Convert.ToInt32(cmdSale.ExecuteScalar());
                }

                // ---- 2. For each cart item: lock, validate, update stock, insert SaleItem ----
                foreach (var item in ShoppingCart)
                {
                    int currentStock;
                    decimal currentPrice;

                    // FOR UPDATE pessimistically locks the row against other cashiers
                    // until this transaction commits or rolls back.
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
                        cmdItem.Parameters.AddWithValue("@p", currentPrice);      // price from DB, not client
                        cmdItem.ExecuteNonQuery();
                    }

                    totalAmount += currentPrice * item.Quantity;
                }

                // ---- 3. Update the Sale's TotalAmount now that we know it ----
                using (var cmdUpdTotal = new NpgsqlCommand(
                    @"UPDATE ""Sales"" SET ""TotalAmount"" = @t WHERE ""SaleID"" = @id",
                    conn, transaction))
                {
                    cmdUpdTotal.Parameters.AddWithValue("@t", totalAmount);
                    cmdUpdTotal.Parameters.AddWithValue("@id", saleId);
                    cmdUpdTotal.ExecuteNonQuery();
                }

                // ---- 4. COMMIT atomically ----
                transaction.Commit();
                FileLogger.Info($"Sale #{saleId} completed — ${totalAmount:F2}, {ShoppingCart.Count} line(s).");

                MessageBox.Show($"Sale #{saleId} completed. Total: ${totalAmount:F2}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ShoppingCart.Clear();
                UpdateTotalAmount();
                LoadAvailableProducts(txtSearch.Text);
            }
            catch (Exception ex)
            {
                // ANY failure → undo everything
                transaction.Rollback();
                FileLogger.Error("Checkout transaction rolled back.", ex);
                MessageBox.Show("Transaction failed: " + ex.Message,
                    "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>View-model for an available-products grid row.</summary>
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

    /// <summary>
    /// View-model for a line in the shopping cart. Implements
    /// <see cref="INotifyPropertyChanged"/> so the cart DataGrid's Subtotal
    /// column refreshes whenever the quantity changes.
    /// </summary>
    public class CartItemModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int VariantID { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }

        private int _quantity;
        /// <summary>Quantity of this product in the cart. Raises change events for both the quantity and the subtotal.</summary>
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

        /// <summary>Derived: price × quantity. Always recomputed on access.</summary>
        public decimal Subtotal => Price * Quantity;
    }
}

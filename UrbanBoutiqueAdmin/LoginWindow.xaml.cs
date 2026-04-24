// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Login Module
//
// ==============================================================================
// This file implements the sign-in window for the Admin desktop application.
// It is the first window shown by the app (see App.xaml StartupUri) and is
// responsible for:
//     1. Creating the database schema on first launch.
//     2. Seeding a default admin account and the three initial categories.
//     3. Validating user credentials using PBKDF2-hashed passwords.
//     4. Storing the authenticated user in <see cref="CurrentUser"/> and
//        opening <see cref="MainWindow"/> for admins.
// ==============================================================================

using System;
using System.Linq;
using System.Windows;
using UrbanBoutiqueAdmin.Data;

namespace UrbanBoutiqueAdmin
{
    /// <summary>
    /// The login window that gates access to the Admin dashboard.
    /// Instantiated by the WPF runtime via <c>App.xaml</c>'s <c>StartupUri</c>.
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>
        /// Initialises the UI and ensures the database is ready to use before
        /// the user can attempt to log in.
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
            EnsureDatabaseSeeded();
        }

        /// <summary>
        /// Creates the database schema (via <see cref="Microsoft.EntityFrameworkCore.DatabaseFacade.EnsureCreated"/>)
        /// and seeds the default admin account and categories if the tables
        /// are empty. This lets the app run on a fresh machine with no manual
        /// SQL setup — only an empty <c>urban_boutique</c> database is required.
        /// </summary>
        private void EnsureDatabaseSeeded()
        {
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();

                if (!db.Users.Any())
                {
                    db.Users.Add(new User
                    {
                        Username = "admin",
                        Password = PasswordHasher.Hash("admin123"),
                        Role = "Admin"
                    });
                    db.SaveChanges();
                    FileLogger.Info("Seeded default admin user.");
                }

                if (!db.Categories.Any())
                {
                    db.Categories.AddRange(
                        new Category { Name = "Clothing" },
                        new Category { Name = "Footwear" },
                        new Category { Name = "Accessories" }
                    );
                    db.SaveChanges();
                    FileLogger.Info("Seeded default categories.");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("Database initialisation failed.", ex);
                MessageBox.Show("Database connection error: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the "Sign In" button click. Validates the credentials using
        /// <see cref="PasswordHasher.Verify"/>, sets <see cref="CurrentUser"/>
        /// on success and opens the main dashboard. Only Admin-role accounts
        /// are allowed into this application.
        /// </summary>
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;

            // --- Step 1: basic input validation ---
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            try
            {
                using var db = new AppDbContext();

                // --- Step 2: look up the user by username only ---
                // Never check password in the query: we can't do a hash comparison
                // in SQL (each hash has a unique per-row salt).
                var user = db.Users.FirstOrDefault(u => u.Username == username);

                if (user == null || !PasswordHasher.Verify(password, user.Password))
                {
                    FileLogger.Warn($"Failed login attempt for '{username}'.");
                    ShowError("Invalid username or password.");
                    return;
                }

                // --- Step 3: populate current-session state ---
                CurrentUser.UserID = user.UserID;
                CurrentUser.Username = user.Username;
                CurrentUser.Role = user.Role;

                // --- Step 4: enforce role — this app is admin-only ---
                if (user.Role != "Admin")
                {
                    FileLogger.Warn($"Non-admin '{username}' attempted admin sign-in.");
                    ShowError("Only administrators can sign in here. Sales staff must use the Cashier terminal.");
                    CurrentUser.SignOut();
                    return;
                }

                // --- Step 5: open the dashboard and close this window ---
                FileLogger.Info($"Admin '{username}' signed in.");
                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                FileLogger.Error("Unhandled error during login.", ex);
                ShowError("Login error: " + ex.Message);
            }
        }

        /// <summary>Shows the inline error banner with the supplied message.</summary>
        private void ShowError(string message)
        {
            txtError.Text = message;
            borderError.Visibility = Visibility.Visible;
        }
    }
}

// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - Login Module

using System;
using System.Linq;
using System.Windows;
using UrbanBoutiqueAdmin.Data;

namespace UrbanBoutiqueAdmin
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            EnsureDatabaseSeeded();
        }

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
                }

                if (!db.Categories.Any())
                {
                    db.Categories.AddRange(
                        new Category { Name = "Clothing" },
                        new Category { Name = "Footwear" },
                        new Category { Name = "Accessories" }
                    );
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database connection error: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var user = db.Users.FirstOrDefault(u => u.Username == username);

                if (user == null || !PasswordHasher.Verify(password, user.Password))
                {
                    ShowError("Invalid username or password.");
                    return;
                }

                CurrentUser.UserID = user.UserID;
                CurrentUser.Username = user.Username;
                CurrentUser.Role = user.Role;

                if (user.Role != "Admin")
                {
                    ShowError("Only administrators can sign in here. Sales staff must use the Cashier terminal.");
                    CurrentUser.SignOut();
                    return;
                }

                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError("Login error: " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            borderError.Visibility = Visibility.Visible;
        }
    }
}

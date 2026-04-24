// Author: Ochilov Ilyosjon (ID: B2300540)
// Project: Urban Boutique POS System - User Management Module

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UrbanBoutiqueAdmin.Data;

namespace UrbanBoutiqueAdmin
{
    public partial class UserManagementWindow : Window
    {
        public ObservableCollection<UserDisplayModel> UsersList { get; set; } = new();

        public UserManagementWindow()
        {
            InitializeComponent();
            dgUsers.ItemsSource = UsersList;
            LoadUsers();
        }

        private void LoadUsers()
        {
            UsersList.Clear();
            try
            {
                using var db = new AppDbContext();
                foreach (var u in db.Users.OrderBy(u => u.UserID).ToList())
                    UsersList.Add(new UserDisplayModel
                    {
                        UserID = u.UserID,
                        Username = u.Username,
                        Role = u.Role
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading users: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;
            var role = (cmbRole.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
            {
                Warn("Please fill all fields.");
                return;
            }
            if (password.Length < 4)
            {
                Warn("Password must be at least 4 characters.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                if (db.Users.Any(u => u.Username == username))
                {
                    Warn("Username already exists. Use Reset to change password.");
                    return;
                }

                db.Users.Add(new User
                {
                    Username = username,
                    Password = PasswordHasher.Hash(password),
                    Role = role
                });
                db.SaveChanges();

                MessageBox.Show("User added successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                ClearForm();
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var newPassword = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(newPassword))
            {
                Warn("Please enter username and new password.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var user = db.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                {
                    Warn("User not found.");
                    return;
                }

                user.Password = PasswordHasher.Hash(newPassword);
                db.SaveChanges();

                MessageBox.Show($"Password for '{username}' reset.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            txtUsername.Clear();
            txtPassword.Clear();
            cmbRole.SelectedIndex = -1;
        }

        private void Warn(string msg) =>
            MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public class UserDisplayModel
    {
        public int UserID { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
    }
}

// Author: Ochilov Ilyosjon (ID: B2300540)
// Tests for DATABASE_URL parsing and public URL resolution.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using UrbanBoutiqueWeb.Data;
using Xunit;

namespace UrbanBoutique.Tests
{
    public class DatabaseConnectionTests
    {
        // --- ParseDatabaseUrl ---

        [Fact]
        public void ParseDatabaseUrl_ExtractsHostPortUserPassDb()
        {
            var cs = DatabaseConnection.ParseDatabaseUrl(
                "postgresql://alice:s3cret@db.example.com:6543/shop");

            Assert.Contains("Host=db.example.com", cs);
            Assert.Contains("Port=6543", cs);
            Assert.Contains("Username=alice", cs);
            Assert.Contains("Password=s3cret", cs);
            Assert.Contains("Database=shop", cs);
        }

        [Fact]
        public void ParseDatabaseUrl_DefaultsPortTo5432_WhenMissing()
        {
            var cs = DatabaseConnection.ParseDatabaseUrl(
                "postgresql://u:p@host/db");
            Assert.Contains("Port=5432", cs);
        }

        [Fact]
        public void ParseDatabaseUrl_EnablesSsl()
        {
            var cs = DatabaseConnection.ParseDatabaseUrl(
                "postgresql://u:p@host:5432/db");
            Assert.Contains("SSL Mode=Require", cs);
            Assert.Contains("Trust Server Certificate=true", cs);
        }

        [Fact]
        public void ParseDatabaseUrl_UnescapesUserAndPassword()
        {
            // Railway sometimes URL-encodes special characters
            var cs = DatabaseConnection.ParseDatabaseUrl(
                "postgresql://user%40name:p%40ss@host:5432/db");
            Assert.Contains("Username=user@name", cs);
            Assert.Contains("Password=p@ss", cs);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseDatabaseUrl_ThrowsOnEmptyInput(string url)
        {
            Assert.Throws<ArgumentException>(() => DatabaseConnection.ParseDatabaseUrl(url));
        }

        [Fact]
        public void ParseDatabaseUrl_ThrowsOnMalformedUri()
        {
            Assert.Throws<ArgumentException>(() =>
                DatabaseConnection.ParseDatabaseUrl("this is not a valid uri"));
        }

        // --- Build (respects env var first) ---

        [Fact]
        public void Build_FallsBackToConfiguration_WhenNoEnvVar()
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
            var config = BuildConfig(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = "Host=local;Database=dev"
            });

            var cs = DatabaseConnection.Build(config);
            Assert.Equal("Host=local;Database=dev", cs);
        }

        [Fact]
        public void Build_ReturnsNull_WhenNothingConfigured()
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
            var config = BuildConfig(new Dictionary<string, string?>());
            Assert.Null(DatabaseConnection.Build(config));
        }

        // --- ResolvePublicUrl ---

        [Fact]
        public void ResolvePublicUrl_PreferAppUrlEnv()
        {
            Environment.SetEnvironmentVariable("APP_URL", "https://custom.example.com/");
            Environment.SetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN", "rail.example.com");

            try
            {
                var url = DatabaseConnection.ResolvePublicUrl(BuildConfig());
                Assert.Equal("https://custom.example.com", url);   // trailing slash stripped
            }
            finally
            {
                Environment.SetEnvironmentVariable("APP_URL", null);
                Environment.SetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN", null);
            }
        }

        [Fact]
        public void ResolvePublicUrl_FallsBackToRailwayDomain()
        {
            Environment.SetEnvironmentVariable("APP_URL", null);
            Environment.SetEnvironmentVariable("PUBLIC_URL", null);
            Environment.SetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN", "demo.up.railway.app");

            try
            {
                var url = DatabaseConnection.ResolvePublicUrl(BuildConfig());
                Assert.Equal("https://demo.up.railway.app", url);
            }
            finally
            {
                Environment.SetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN", null);
            }
        }

        [Fact]
        public void ResolvePublicUrl_ReturnsNull_WhenNothingSet()
        {
            Environment.SetEnvironmentVariable("APP_URL", null);
            Environment.SetEnvironmentVariable("PUBLIC_URL", null);
            Environment.SetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN", null);
            Environment.SetEnvironmentVariable("RAILWAY_STATIC_URL", null);
            Assert.Null(DatabaseConnection.ResolvePublicUrl(BuildConfig()));
        }

        // --- Helpers ---
        private static IConfiguration BuildConfig(Dictionary<string, string?>? values = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
                .Build();
        }
    }
}

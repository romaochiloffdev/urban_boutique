using System;
using Microsoft.Extensions.Configuration;

namespace UrbanBoutiqueWeb.Data
{
    /// <summary>
    /// Resolves the PostgreSQL connection string used by the application. Prefers
    /// the Railway / Heroku-style <c>DATABASE_URL</c> environment variable and falls
    /// back to <c>ConnectionStrings:DefaultConnection</c> from configuration.
    /// </summary>
    /// <remarks>
    /// Extracted from <c>Program.cs</c> so the parsing logic can be unit-tested.
    /// </remarks>
    public static class DatabaseConnection
    {
        /// <summary>
        /// Builds an Npgsql-compatible connection string.
        /// </summary>
        /// <param name="config">Configuration (for the fallback path).</param>
        /// <returns>The connection string, or <c>null</c> if none is configured.</returns>
        public static string? Build(IConfiguration config)
        {
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");
            return !string.IsNullOrWhiteSpace(url)
                ? ParseDatabaseUrl(url)
                : config.GetConnectionString("DefaultConnection");
        }

        /// <summary>
        /// Converts a PostgreSQL URI (<c>postgresql://user:pass@host:port/db</c>) into an
        /// Npgsql key=value connection string with SSL enabled.
        /// </summary>
        /// <param name="url">The URI, e.g. from Railway's DATABASE_URL variable.</param>
        /// <returns>An Npgsql connection string.</returns>
        /// <exception cref="ArgumentException">If the URI is malformed.</exception>
        public static string ParseDatabaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL is required", nameof(url));

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException($"Invalid database URL: {url}", nameof(url), ex);
            }

            var userInfo = uri.UserInfo.Split(':', 2);
            var user = Uri.UnescapeDataString(userInfo[0]);
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var db = uri.AbsolutePath.TrimStart('/');
            var port = uri.Port > 0 ? uri.Port : 5432;

            return $"Host={uri.Host};Port={port};Username={user};Password={pass};Database={db};" +
                   $"SSL Mode=Require;Trust Server Certificate=true;Pooling=true";
        }

        /// <summary>
        /// Returns the public application URL, used by the frontend config endpoint.
        /// Honours (in order): <c>APP_URL</c>, <c>PUBLIC_URL</c>, <c>Public:AppUrl</c>
        /// config, then Railway's <c>RAILWAY_PUBLIC_DOMAIN</c> / <c>RAILWAY_STATIC_URL</c>.
        /// </summary>
        public static string? ResolvePublicUrl(IConfiguration config)
        {
            var explicitUrl = Environment.GetEnvironmentVariable("APP_URL")
                              ?? Environment.GetEnvironmentVariable("PUBLIC_URL")
                              ?? config["Public:AppUrl"];
            if (!string.IsNullOrWhiteSpace(explicitUrl)) return explicitUrl.TrimEnd('/');

            var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN");
            if (!string.IsNullOrWhiteSpace(railwayDomain)) return $"https://{railwayDomain}";

            var railwayStatic = Environment.GetEnvironmentVariable("RAILWAY_STATIC_URL");
            if (!string.IsNullOrWhiteSpace(railwayStatic)) return railwayStatic.TrimEnd('/');

            return null;
        }
    }
}

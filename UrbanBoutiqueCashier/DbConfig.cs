using System;
using System.Configuration;

namespace UrbanBoutiqueCashier
{
    internal static class DbConfig
    {
        public static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["UrbanBoutique"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(cs)) return cs;

                var envCs = Environment.GetEnvironmentVariable("URBAN_BOUTIQUE_DB");
                if (!string.IsNullOrWhiteSpace(envCs)) return envCs;

                return "Host=localhost;Username=postgres;Password=1;Database=urban_boutique";
            }
        }
    }
}

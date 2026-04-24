namespace UrbanBoutiqueAdmin.Data
{
    public static class CurrentUser
    {
        public static int UserID { get; set; }
        public static string Username { get; set; } = "";
        public static string Role { get; set; } = "";

        public static bool IsAdmin => Role == "Admin";
        public static bool IsAuthenticated => UserID > 0;

        public static void SignOut()
        {
            UserID = 0;
            Username = "";
            Role = "";
        }
    }
}

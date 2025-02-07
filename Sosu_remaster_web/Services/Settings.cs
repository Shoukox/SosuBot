namespace Sosu.Services
{
    public class Settings
    {
        public static void InitializeVariables()
        {
            Variables.osuUsers = new List<Sosu.Types.osuUser>();
            Variables.chats = new List<Sosu.Types.Chat>();
        }
        public static void CreateDatabase()
        {
            //Variables.db = new Database("Host=ec2-18-211-236-255.compute-1.amazonaws.com;" +
            //                "Port=5432;" +
            //                "User ID=oziliprhmviimb;" +
            //                "Password=c7fc69db477b043a41da6354a7707ba6d229c1de81d2b7f4dde92e8850305f07;" +
            //                "Database=d56vhuom8nrkgq;" +
            //                "Pooling=true;" +
            //                "SSL Mode=Require;" +
            //                "TrustServerCertificate=true;");
            //Variables.db = new Database("Host=localhost;Port=1337;Username=postgres;Password=5202340;Database=shiukkzbot");
        }
        public static void ConnectToOsuApi()
        {
            Variables.osuApi = new Sosu.osu.V1.osuApi("67368ae869a6b45f012b6a7a8536ee65226ad257");
        }
        public static void LoadData()
        {
            Data.LoadData();
        }

        private static void StartSaveTimer()
        {
            TextDatabase.SaveTimer();
        }

        public static void LoadAllSettings()
        {
            InitializeVariables();
            CreateDatabase();
            ConnectToOsuApi();
            LoadData();
            StartSaveTimer();
        }
    }
}

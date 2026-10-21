using System.Windows;

namespace CRS
{
    public partial class App : Application
    {
        public static string ConnectionString { get; } =
            "Data Source=stud-mssql.sttec.yar.ru,38325;" +
            "Initial Catalog=user16_db;" +
            "User ID=user16_db;" +
            "Password=user16;" +
            "TrustServerCertificate=True;" +
            "MultipleActiveResultSets=True;" +
            "App=EntityFramework";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var login = new LoginWindow();
            login.Show();
        }
    }
}
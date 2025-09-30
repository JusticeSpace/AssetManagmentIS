using System.Windows;

namespace AssetManagment
{
    public partial class App : Application
    {
        public static Users CurrentUser { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var loginWindow = new Windows.LoginWindow();
            loginWindow.Show();
        }
    }
}
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AssetManagment.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly AssetControlDBEntities _context;
        private bool _isLoggingIn = false;

        public LoginWindow()
        {
            InitializeComponent();
            _context = new AssetControlDBEntities();
            txtUsername.Focus();

            // Обработка Enter
            txtPassword.KeyDown += TxtPassword_KeyDown;
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isLoggingIn)
            {
                BtnLogin_Click(null, null);
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn) return;

            try
            {
                _isLoggingIn = true;
                btnLogin.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string username = txtUsername.Text.Trim();
                string password = txtPassword.Password;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Введите имя пользователя и пароль",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string passwordHash = GetMD5Hash(password);

                await Task.Delay(500);

                var user = _context.Users
                    .Include("UserRoles")
                    .Include("Employees")
                    .FirstOrDefault(u => u.Username == username &&
                                       u.PasswordHash == passwordHash &&
                                       u.IsActive == true);

                if (user != null)
                {
                    App.CurrentUser = user;

                    user.LastLoginDate = DateTime.Now;
                    _context.SaveChanges();

                    var mainWindow = new MainWindow();
                    mainWindow.Show();

                    this.Close();
                }
                else
                {
                    MessageBox.Show("Неверное имя пользователя или пароль",
                        "Ошибка входа",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    txtPassword.Clear();
                    txtUsername.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при входе: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isLoggingIn = false;
                btnLogin.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        private string GetMD5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace AssetManagment.Pages
{
    public partial class ProfilePage : Page
    {
        private readonly AssetControlDBEntities _context;
        private Users _currentUser;
        private Employees _currentEmployee;

        public ProfilePage()
        {
            InitializeComponent();
            _context = new AssetControlDBEntities();
            Loaded += ProfilePage_Loaded;
        }

        private void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserData();
        }

        private void LoadUserData()
        {
            if (App.CurrentUser == null) return;

            _currentUser = _context.Users
                .Include("UserRoles")
                .Include("Employees.Positions")
                .Include("Employees.Departments")
                .FirstOrDefault(u => u.UserID == App.CurrentUser.UserID);

            if (_currentUser?.Employees == null) return;
            _currentEmployee = _currentUser.Employees;

            // Карточка слева
            txtFullName.Text = $"{_currentEmployee.LastName} {_currentEmployee.FirstName} {_currentEmployee.MiddleName}".Trim();
            txtRoleText.Text = _currentUser.UserRoles.RoleName;

            var (bgColor, fgColor, icon) = GetRoleStyle(_currentUser.RoleID);
            chipRole.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));
            txtRoleText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgColor));
            RoleIcon.Kind = (PackIconKind)Enum.Parse(typeof(PackIconKind), icon);

            txtAssetCount.Text = _context.Assets.Count(a => a.ResponsibleEmployeeID == _currentEmployee.EmployeeID && a.IsActive == true).ToString();

            if (_currentUser.LastLoginDate.HasValue)
            {
                var lastLogin = _currentUser.LastLoginDate.Value;
                txtLastLogin.Text = lastLogin.Date == DateTime.Today ? $"Сегодня, {lastLogin:HH:mm}" : lastLogin.ToString("dd MMM yyyy");
            }

            txtEmailDisplay.Text = string.IsNullOrWhiteSpace(_currentEmployee.Email) ? "Не указан" : _currentEmployee.Email;
            txtPhoneDisplay.Text = string.IsNullOrWhiteSpace(_currentEmployee.Phone) ? "Не указан" : _currentEmployee.Phone;
            txtPositionDisplay.Text = _currentEmployee.Positions?.PositionName ?? "Не указана";
            txtDepartmentDisplay.Text = _currentEmployee.Departments?.DepartmentName ?? "Не указан";

            // Формы справа
            txtLastName.Text = _currentEmployee.LastName;
            txtFirstName.Text = _currentEmployee.FirstName;
            txtMiddleName.Text = _currentEmployee.MiddleName;
            txtUsername.Text = _currentUser.Username;
            txtEmail.Text = _currentEmployee.Email;
            txtPhone.Text = _currentEmployee.Phone;

            LoadProfileImage();
        }

        private void LoadProfileImage()
        {
            if (_currentEmployee.Photo != null && _currentEmployee.Photo.Length > 0)
            {
                using (var stream = new MemoryStream(_currentEmployee.Photo))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    ProfileImageBrush.ImageSource = image;
                }
            }
            else
            {
                ProfileImageBrush.ImageSource = null;
            }
        }

        private (string, string, string) GetRoleStyle(int roleId)
        {
            switch (roleId)
            {
                case 1: return ("#FEF2F2", "#DC2626", "ShieldAccount"); // Admin
                case 2: return ("#FEF3C7", "#D97706", "StarAccount");   // Manager
                case 3: return ("#F0FDF4", "#16A34A", "Account");       // User
                default: return ("#F3F4F6", "#6B7280", "Help");
            }
        }

        private void ChangePhoto_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    byte[] imageData = File.ReadAllBytes(openFileDialog.FileName);
                    _currentEmployee.Photo = imageData;
                    _context.SaveChanges();
                    LoadProfileImage();
                    ShowMessage("Фото профиля успешно обновлено!");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Ошибка загрузки фото: {ex.Message}", true);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtLastName.Text) || string.IsNullOrWhiteSpace(txtFirstName.Text))
                { ShowMessage("Фамилия и Имя не могут быть пустыми.", true); return; }
                if (string.IsNullOrWhiteSpace(txtUsername.Text))
                { ShowMessage("Логин не может быть пустым.", true); return; }
                if (!string.IsNullOrWhiteSpace(txtEmail.Text) && !IsValidEmail(txtEmail.Text))
                { ShowMessage("Введите корректный email адрес.", true); return; }
                if (_context.Users.Any(u => u.Username == txtUsername.Text && u.UserID != _currentUser.UserID))
                { ShowMessage("Этот логин уже занят.", true); return; }
                if (!string.IsNullOrWhiteSpace(txtEmail.Text) && _context.Employees.Any(emp => emp.Email == txtEmail.Text && emp.EmployeeID != _currentEmployee.EmployeeID))
                { ShowMessage("Этот email уже используется другим сотрудником.", true); return; }

                // --- ИСПРАВЛЕНИЕ: Сохраняем все поля ---
                _currentEmployee.LastName = txtLastName.Text.Trim();
                _currentEmployee.FirstName = txtFirstName.Text.Trim();
                _currentEmployee.MiddleName = string.IsNullOrWhiteSpace(txtMiddleName.Text) ? null : txtMiddleName.Text.Trim();
                _currentEmployee.Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();
                _currentEmployee.Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim();
                _currentUser.Username = txtUsername.Text.Trim();

                _context.SaveChanges();

                LoadUserData();
                ShowMessage("Данные профиля успешно обновлены!");
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка при сохранении: {ex.Message}", true);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            LoadUserData();
            ShowMessage("Изменения отменены.");
        }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtCurrentPassword.Password) || string.IsNullOrWhiteSpace(txtNewPassword.Password) || string.IsNullOrWhiteSpace(txtConfirmPassword.Password))
                { ShowMessage("Заполните все поля для смены пароля.", true); return; }
                if (GetMD5Hash(txtCurrentPassword.Password) != _currentUser.PasswordHash)
                { ShowMessage("Неверный текущий пароль.", true); return; }
                if (txtNewPassword.Password != txtConfirmPassword.Password)
                { ShowMessage("Новые пароли не совпадают.", true); return; }
                if (txtNewPassword.Password.Length < 6)
                { ShowMessage("Пароль должен содержать не менее 6 символов.", true); return; }

                _currentUser.PasswordHash = GetMD5Hash(txtNewPassword.Password);
                _context.SaveChanges();

                txtCurrentPassword.Clear();
                txtNewPassword.Clear();
                txtConfirmPassword.Clear();
                ShowMessage("Пароль успешно изменен!");
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка при смене пароля: {ex.Message}", true);
            }
        }

        private string GetMD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private bool IsValidEmail(string email) => Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private void ShowMessage(string message, bool isError = false)
        {
            if (NotificationSnackbar.MessageQueue != null)
            {
                NotificationSnackbar.MessageQueue.Enqueue(message);
            }
        }
    }
}
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

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
                .Include("Employees")
                .Include("Employees.Positions")
                .Include("Employees.Departments")
                .FirstOrDefault(u => u.UserID == App.CurrentUser.UserID);

            if (_currentUser?.Employees == null) return;

            _currentEmployee = _currentUser.Employees;

            // Инициалы
            txtInitials.Text = $"{_currentEmployee.FirstName[0]}{_currentEmployee.LastName[0]}";

            // Основная информация
            txtFullName.Text = $"{_currentEmployee.LastName} {_currentEmployee.FirstName} {_currentEmployee.MiddleName}";
            txtRoleText.Text = _currentUser.UserRoles.RoleName;

            // Цвет и стиль роли (исправлено для C# 7.3)
            string background, foreground, icon;
            switch (_currentUser.RoleID)
            {
                case 1: // Администратор
                    background = "#FEF2F2";
                    foreground = "#DC2626";
                    icon = "Shield";
                    break;
                case 2: // Менеджер
                    background = "#FEF3C7";
                    foreground = "#D97706";
                    icon = "Star";
                    break;
                case 3: // Пользователь
                    background = "#F0FDF4";
                    foreground = "#16A34A";
                    icon = "Account";
                    break;
                default:
                    background = "#F3F4F6";
                    foreground = "#6B7280";
                    icon = "Help";
                    break;
            }

            chipRole.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
            txtRoleText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));

            // Остальной код без изменений...
            var assetsCount = _context.Assets.Count(a => a.ResponsibleEmployeeID == _currentEmployee.EmployeeID && a.IsActive == true);
            txtAssetCount.Text = assetsCount.ToString();

            if (_currentUser.LastLoginDate.HasValue)
            {
                var lastLogin = _currentUser.LastLoginDate.Value;
                txtLastLogin.Text = lastLogin.Date == DateTime.Today
                    ? $"Сегодня {lastLogin:HH:mm}"
                    : lastLogin.ToString("dd.MM.yyyy");
            }

            // Контактная информация (отображение)
            txtEmailDisplay.Text = string.IsNullOrWhiteSpace(_currentEmployee.Email) ? "Не указан" : _currentEmployee.Email;
            txtPhoneDisplay.Text = string.IsNullOrWhiteSpace(_currentEmployee.Phone) ? "Не указан" : _currentEmployee.Phone;
            txtPositionDisplay.Text = _currentEmployee.Positions?.PositionName ?? "Не указана";
            txtDepartmentDisplay.Text = _currentEmployee.Departments?.DepartmentName ?? "Не указан";

            // Форма редактирования
            txtLastName.Text = _currentEmployee.LastName;
            txtFirstName.Text = _currentEmployee.FirstName;
            txtMiddleName.Text = _currentEmployee.MiddleName;
            txtUsername.Text = _currentUser.Username;
            txtEmail.Text = _currentEmployee.Email;
            txtPhone.Text = _currentEmployee.Phone;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация email
                if (!string.IsNullOrWhiteSpace(txtEmail.Text))
                {
                    if (!IsValidEmail(txtEmail.Text))
                    {
                        ShowMessage("Введите корректный email адрес", true);
                        return;
                    }

                    var emailExists = _context.Employees.Any(emp =>
                        emp.Email == txtEmail.Text && emp.EmployeeID != _currentEmployee.EmployeeID);

                    if (emailExists)
                    {
                        ShowMessage("Этот email уже используется другим пользователем", true);
                        return;
                    }
                }

                // Валидация телефона
                if (!string.IsNullOrWhiteSpace(txtPhone.Text))
                {
                    if (!IsValidPhone(txtPhone.Text))
                    {
                        ShowMessage("Введите корректный номер телефона", true);
                        return;
                    }
                }

                // Обновление данных
                _currentEmployee.Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text;
                _currentEmployee.Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text;

                _context.SaveChanges();

                // Обновление отображения
                txtEmailDisplay.Text = string.IsNullOrWhiteSpace(_currentEmployee.Email) ? "Не указан" : _currentEmployee.Email;
                txtPhoneDisplay.Text = string.IsNullOrWhiteSpace(_currentEmployee.Phone) ? "Не указан" : _currentEmployee.Phone;

                ShowMessage("✅ Данные успешно обновлены!");
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Ошибка при сохранении: {ex.Message}", true);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            LoadUserData();
            ShowMessage("Изменения отменены");
        }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtCurrentPassword.Password) ||
                    string.IsNullOrWhiteSpace(txtNewPassword.Password) ||
                    string.IsNullOrWhiteSpace(txtConfirmPassword.Password))
                {
                    ShowMessage("Заполните все поля для смены пароля", true);
                    return;
                }

                string currentPasswordHash = GetMD5Hash(txtCurrentPassword.Password);
                if (currentPasswordHash != _currentUser.PasswordHash)
                {
                    ShowMessage("❌ Неверный текущий пароль", true);
                    return;
                }

                if (txtNewPassword.Password != txtConfirmPassword.Password)
                {
                    ShowMessage("❌ Новые пароли не совпадают", true);
                    return;
                }

                if (txtNewPassword.Password.Length < 6)
                {
                    ShowMessage("❌ Пароль должен содержать не менее 6 символов", true);
                    return;
                }

                _currentUser.PasswordHash = GetMD5Hash(txtNewPassword.Password);
                _context.SaveChanges();

                txtCurrentPassword.Clear();
                txtNewPassword.Clear();
                txtConfirmPassword.Clear();

                ShowMessage("🔒 Пароль успешно изменен!");
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Ошибка при смене пароля: {ex.Message}", true);
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

        private bool IsValidEmail(string email)
        {
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern);
        }

        private bool IsValidPhone(string phone)
        {
            string pattern = @"^\+?[0-9KATEX_INLINE_OPENKATEX_INLINE_CLOSE\-\s]{10,}$";
            return Regex.IsMatch(phone, pattern);
        }

        private void ShowMessage(string message, bool isError = false)
        {
            var messageQueue = NotificationSnackbar.MessageQueue ?? new SnackbarMessageQueue(TimeSpan.FromSeconds(4));
            NotificationSnackbar.MessageQueue = messageQueue;
            messageQueue.Enqueue(message);
        }
    }
}
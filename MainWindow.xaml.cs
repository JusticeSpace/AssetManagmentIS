using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AssetManagment.Pages;

namespace AssetManagment
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timeTimer;
        private readonly AssetControlDBEntities _context;

        public MainWindow()
        {
            InitializeComponent();
            _context = new AssetControlDBEntities();
            _timeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _timeTimer.Tick += TimeTimer_Tick;
            _timeTimer.Start();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserInfo();
            ConfigureMenuByRole();
            UpdateDateTime();
            NavigateToDashboard();
        }

        private void LoadUserInfo()
        {
            if (App.CurrentUser?.Employees == null) return;

            var employee = App.CurrentUser.Employees;
            txtUserName.Text = $"{employee.LastName} {employee.FirstName}";
            txtUserRole.Text = App.CurrentUser.UserRoles?.RoleName ?? "Пользователь";

            // Загрузка фото профиля
            if (employee.Photo != null && employee.Photo.Length > 0)
            {
                try
                {
                    using (var stream = new MemoryStream(employee.Photo))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        ProfilePhotoBrush.ImageSource = image;
                    }
                }
                catch
                {
                    ProfilePhotoBrush.ImageSource = null;
                }
            }
            else
            {
                ProfilePhotoBrush.ImageSource = null;
            }
        }

        private void ConfigureMenuByRole()
        {
            if (App.CurrentUser == null) return;

            // Доступ к Сотрудникам только для Администратора и Менеджера
            btnEmployees.Visibility = (App.CurrentUser.RoleID == 1 || App.CurrentUser.RoleID == 2)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateDateTime()
        {
            txtCurrentDateTime.Text = DateTime.Now.ToString("dd MMMM yyyy, HH:mm",
                new System.Globalization.CultureInfo("ru-RU"));
        }

        private void TimeTimer_Tick(object sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void NavigateToDashboard()
        {
            MainFrame.Navigate(new DashboardPage(_context, App.CurrentUser));
            SetActiveButton(btnDashboard);
            UpdatePageInfo("ViewDashboard", "Панель управления", "Обзор системы и статистика");
        }

        // === ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ДОСТУПА ИЗ СТРАНИЦ ===
        public void SetActiveButton(Button activeButton)
        {
            foreach (var child in MenuPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Style = (Style)FindResource("MenuButtonStyle");
                }
            }
            if (activeButton != null)
            {
                activeButton.Style = (Style)FindResource("ActiveMenuButtonStyle");
            }
        }

        public void UpdatePageInfo(string iconKind, string title, string subtitle)
        {
            try
            {
                PageIcon.Kind = (MaterialDesignThemes.Wpf.PackIconKind)Enum.Parse(
                    typeof(MaterialDesignThemes.Wpf.PackIconKind), iconKind);
                PageTitle.Text = title;
                PageSubtitle.Text = subtitle;
            }
            catch
            {
                PageTitle.Text = title;
                PageSubtitle.Text = subtitle;
            }
        }

        // === УНИВЕРСАЛЬНЫЙ МЕТОД НАВИГАЦИИ ===
        public void NavigateToPage(string pageName)
        {
            try
            {
                switch (pageName)
                {
                    case "Dashboard":
                        NavigateToDashboard();
                        break;
                    case "Assets":
                        BtnAssets_Click(null, null);
                        break;
                    case "Employees":
                        BtnEmployees_Click(null, null);
                        break;
                    case "Profile":
                        BtnProfile_Click(null, null);
                        break;
                    default:
                        MessageBox.Show($"Страница '{pageName}' не найдена", "Ошибка навигации");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка");
            }
        }

        // === ОБРАБОТЧИКИ КНОПОК МЕНЮ ===
        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDashboard();
        }

        private void BtnAssets_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AssetsPage(_context, App.CurrentUser));
            SetActiveButton(btnAssets);
            UpdatePageInfo("PackageVariantClosed", "Управление активами",
                "Просмотр и редактирование активов");
        }

        private void BtnEmployees_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new EmployeesPage());
            SetActiveButton(btnEmployees);
            UpdatePageInfo("AccountGroup", "Сотрудники", "Управление персоналом");
        }

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ProfilePage());
            SetActiveButton(null);
            UpdatePageInfo("Account", "Профиль пользователя",
                "Управление личной информацией");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainFrame.Content is AssetsPage assetsPage)
                {
                    assetsPage.RefreshData();
                }
                else if (MainFrame.Content is EmployeesPage employeesPage)
                {
                    employeesPage.RefreshData();
                }
                else if (MainFrame.Content is DashboardPage)
                {
                    NavigateToDashboard();
                }
                else if (MainFrame.Content is ProfilePage profilePage)
                {
                    // Перезагрузка профиля
                    MainFrame.Navigate(new ProfilePage());
                }
                else
                {
                    // Обновление текущей страницы
                    MainFrame.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите выйти из системы?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                App.CurrentUser = null;
                _timeTimer?.Stop();
                _context?.Dispose();

                var loginWindow = new Windows.LoginWindow();
                loginWindow.Show();
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timeTimer?.Stop();
            _context?.Dispose();
            base.OnClosed(e);
        }
    }
}
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
            txtUserRole.Text = App.CurrentUser.UserRoles.RoleName;

            // Загрузка фото профиля
            if (employee.Photo != null && employee.Photo.Length > 0)
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
            else
            {
                ProfilePhotoBrush.ImageSource = null; // Очищаем, если фото нет
            }
        }

        private void ConfigureMenuByRole()
        {
            if (App.CurrentUser == null) return;

            btnEmployees.Visibility = (App.CurrentUser.RoleID == 1 || App.CurrentUser.RoleID == 2)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateDateTime()
        {
            txtCurrentDateTime.Text = DateTime.Now.ToString("dd MMMM yyyy, HH:mm");
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

        private void SetActiveButton(Button activeButton)
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

        private void UpdatePageInfo(string iconKind, string title, string subtitle)
        {
            PageIcon.Kind = (MaterialDesignThemes.Wpf.PackIconKind)Enum.Parse(typeof(MaterialDesignThemes.Wpf.PackIconKind), iconKind);
            PageTitle.Text = title;
            PageSubtitle.Text = subtitle;
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            NavigateToDashboard();
        }

        private void BtnAssets_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AssetsPage(_context, App.CurrentUser));
            SetActiveButton(btnAssets);
            UpdatePageInfo("PackageVariantClosed", "Управление активами", "Просмотр и редактирование активов");
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
            UpdatePageInfo("Account", "Профиль пользователя", "Управление личной информацией");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
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
                // Для DashboardPage можно просто пересоздать страницу для обновления
                NavigateToDashboard();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы действительно хотите выйти из системы?",
                "Подтверждение выхода", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                App.CurrentUser = null;
                _timeTimer?.Stop();
                _context?.Dispose();
                new Windows.LoginWindow().Show();
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
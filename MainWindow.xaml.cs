using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AssetManagment.Pages;

namespace AssetManagment
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timeTimer;
        private readonly AssetControlDBEntities _context; // readonly поле

        public MainWindow()
        {
            InitializeComponent();

            _context = new AssetControlDBEntities(); // Инициализация контекста

            // Инициализация таймера для времени (упрощенная инициализация)
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
            if (App.CurrentUser?.Employees != null) // Pattern matching для C# 7.3
            {
                var employee = App.CurrentUser.Employees;
                txtUserName.Text = $"{employee.LastName} {employee.FirstName}";
                txtUserRole.Text = App.CurrentUser.UserRoles.RoleName;
                txtUserInitials.Text = $"{employee.FirstName[0]}{employee.LastName[0]}";
            }
        }

        private void ConfigureMenuByRole()
        {
            if (App.CurrentUser == null) return;

            switch (App.CurrentUser.RoleID)
            {
                case 3: // Пользователь
                    btnEmployees.Visibility = Visibility.Collapsed;
                    btnSettings.Visibility = Visibility.Collapsed;
                    btnReports.Visibility = Visibility.Collapsed;
                    break;
                case 2: // Менеджер
                    btnSettings.Visibility = Visibility.Collapsed;
                    break;
                case 1: // Администратор
                    // Все доступно
                    break;
            }
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
            // Передаем параметры в конструктор
            MainFrame.Navigate(new DashboardPage(_context, App.CurrentUser));
            SetActiveButton(btnDashboard);
            UpdatePageInfo("ViewDashboard", "Панель управления", "Обзор системы и статистика");
        }

        private void SetActiveButton(Button activeButton)
        {
            // Сброс всех кнопок
            foreach (Button btn in new[] { btnDashboard, btnAssets, btnEmployees, btnLocations, btnReports, btnSettings })
            {
                btn.Style = (Style)FindResource("MenuButtonStyle");
            }

            // Установка активной кнопки
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
            MainFrame.Navigate(new DashboardPage(_context, App.CurrentUser));
            SetActiveButton(btnDashboard);
            UpdatePageInfo("ViewDashboard", "Панель управления", "Обзор системы и статистика");
        }

        private void BtnAssets_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AssetsPage(_context, App.CurrentUser));
            SetActiveButton(btnAssets);
            UpdatePageInfo("Package", "Управление активами", "Просмотр и редактирование активов");
        }

        private void BtnEmployees_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Страница сотрудников в разработке", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            SetActiveButton(btnEmployees);
            UpdatePageInfo("AccountGroup", "Сотрудники", "Управление персоналом");
        }

        private void BtnLocations_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Страница локаций в разработке", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            SetActiveButton(btnLocations);
            UpdatePageInfo("MapMarker", "Локации", "Управление местоположениями");
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Страница отчетов в разработке", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            SetActiveButton(btnReports);
            UpdatePageInfo("ChartLine", "Отчеты", "Аналитика и статистика");
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Страница настроек в разработке", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            SetActiveButton(btnSettings);
            UpdatePageInfo("Settings", "Настройки", "Конфигурация системы");
        }

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ProfilePage());
            SetActiveButton(null); // Профиль не в основном меню
            UpdatePageInfo("Account", "Профиль пользователя", "Управление личной информацией");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content is Page currentPage)
            {
                MainFrame.Refresh();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы действительно хотите выйти из системы?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

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
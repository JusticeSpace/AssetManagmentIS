using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using MaterialDesignThemes.Wpf;

namespace AssetManagment
{
    public partial class MainWindow : Window
    {
        private AssetControlDBEntities _context;
        private Users _currentUser;
        private DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();
            _context = new AssetControlDBEntities();

            // Временно устанавливаем пользователя
            _currentUser = _context.Users.FirstOrDefault(u => u.Username == "admin");

            // Добавляем обработчик события
            MenuListBox.SelectionChanged += MenuListBox_SelectionChanged;

            // Настройка таймера для обновления времени
            SetupTimer();

            // Загрузка информации о пользователе
            UpdateUserInfo();

            // Загружаем дашборд по умолчанию
            NavigateToDashboard();
        }

        private void SetupTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                var culture = new CultureInfo("ru-RU");
                txtCurrentDateTime.Text = DateTime.Now.ToString("d MMMM yyyy, HH:mm", culture);
            };
            _timer.Start();
        }

        private void UpdateUserInfo()
        {
            if (_currentUser != null)
            {
                var userInfo = _context.vw_UsersInfo
                    .FirstOrDefault(u => u.UserID == _currentUser.UserID);

                if (userInfo != null)
                {
                    txtUserRole.Text = userInfo.RoleName;
                    txtUserEmail.Text = userInfo.Email;
                }
            }
        }

        private void MenuListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = MenuListBox.SelectedItem as ListBoxItem;

            if (selectedItem == null) return;

            // Закрываем боковое меню после выбора
            DrawerHost.IsLeftDrawerOpen = false;

            if (selectedItem == DashboardItem)
            {
                NavigateToDashboard();
            }
            else if (selectedItem == AssetsItem)
            {
                NavigateToAssets();
            }
            else if (selectedItem == EmployeesItem)
            {
                NavigateToEmployees();
            }
            else if (selectedItem == LocationsItem)
            {
                NavigateToLocations();
            }
            else if (selectedItem == ReportsItem)
            {
                NavigateToReports();
            }
            else if (selectedItem == SettingsItem)
            {
                NavigateToSettings();
            }
        }

        private void NavigateToDashboard()
        {
            PageTitle.Text = "Панель управления";
            var dashboardPage = new Pages.DashboardPage(_context, _currentUser);
            MainFrame.Navigate(dashboardPage);
        }

        private void NavigateToAssets()
        {
            PageTitle.Text = "Управление активами";
            var assetsPage = new Pages.AssetsPage(_context, _currentUser);
            MainFrame.Navigate(assetsPage);
        }

        private void NavigateToEmployees()
        {
            PageTitle.Text = "Сотрудники";
            ShowNotification("Модуль управления сотрудниками будет доступен в следующей версии");
        }

        private void NavigateToLocations()
        {
            PageTitle.Text = "Локации";
            ShowNotification("Модуль управления локациями будет доступен в следующей версии");
        }

        private void NavigateToReports()
        {
            PageTitle.Text = "Отчеты";
            ShowNotification("Модуль отчетов будет доступен в следующей версии");
        }

        private void NavigateToSettings()
        {
            PageTitle.Text = "Настройки";
            ShowNotification("Настройки системы будут доступны в следующей версии");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // Обновляем текущую страницу
            if (MainFrame.Content is Pages.DashboardPage dashboardPage)
            {
                dashboardPage.RefreshData();
            }
            else if (MainFrame.Content is Pages.AssetsPage assetsPage)
            {
                assetsPage.RefreshData();
            }

            ShowNotification("Данные успешно обновлены", true);
        }

        private void ShowNotification(string message, bool isSuccess = false)
        {
            var messageQueue = MainSnackbar.MessageQueue;

            // Настраиваем стиль уведомления
            var duration = isSuccess ? 2000 : 4000;

            messageQueue?.Enqueue(
                message,
                null,
                null,
                null,
                false,
                true,
                TimeSpan.FromMilliseconds(duration));
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _timer?.Stop();
            _context?.Dispose();
        }
    }
}
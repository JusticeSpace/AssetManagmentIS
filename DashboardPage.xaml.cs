using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AssetManagment.Pages
{
    public partial class DashboardPage : Page
    {
        private readonly AssetControlDBEntities _context;
        private readonly Users _currentUser;

        public DashboardPage() : this(new AssetControlDBEntities(), App.CurrentUser) { }

        public DashboardPage(AssetControlDBEntities context, Users currentUser)
        {
            InitializeComponent();
            _context = context ?? new AssetControlDBEntities();
            _currentUser = currentUser ?? App.CurrentUser;

            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();
            ConfigureUIByRole();
        }

        private void LoadDashboardData()
        {
            try
            {
                LoadWelcomeMessage();
                LoadAssetStatistics();
                LoadAdditionalStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных Dashboard:\n\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWelcomeMessage()
        {
            if (_currentUser?.Employees != null)
            {
                var employee = _currentUser.Employees;
                string fullName = $"{employee.FirstName} {employee.LastName}";
                txtWelcome.Text = $"Добро пожаловать, {fullName}!";
            }
            else if (_currentUser != null)
            {
                txtWelcome.Text = $"Добро пожаловать, {_currentUser.Username}!";
            }
            else
            {
                txtWelcome.Text = "Добро пожаловать!";
            }

            if (_currentUser?.UserRoles != null)
            {
                txtUserRole.Text = _currentUser.UserRoles.RoleName;
            }
            else
            {
                txtUserRole.Text = "Пользователь";
            }

            txtCurrentDate.Text = DateTime.Now.ToString("dddd, d MMMM yyyy",
                new System.Globalization.CultureInfo("ru-RU"));
        }

        private void LoadAssetStatistics()
        {
            var oneMonthAgo = DateTime.Now.AddMonths(-1);
            var allAssets = _context.Assets.Where(a => a.IsActive == true).ToList();
            var totalAssets = allAssets.Count;

            var activeAssets = allAssets.Count(a => a.StatusID == 1);
            var inRepairAssets = allAssets.Count(a => a.StatusID == 3);
            var disposedAssets = _context.Assets.Count(a => a.StatusID == 4);

            txtTotalAssets.Text = totalAssets.ToString();
            txtActiveAssets.Text = activeAssets.ToString();
            txtInRepair.Text = inRepairAssets.ToString();
            txtDisposed.Text = disposedAssets.ToString();

            if (totalAssets > 0)
            {
                double activePercent = (double)activeAssets / totalAssets * 100;
                double repairPercent = (double)inRepairAssets / totalAssets * 100;

                txtActiveAssetsPercent.Text = $"{activePercent:F1}% от общего";

                // Если progressActive это ProgressBar
                progressActive.Value = activePercent;
                progressRepair.Value = repairPercent;
            }
            else
            {
                txtActiveAssetsPercent.Text = "Нет данных";
                progressActive.Value = 0;
                progressRepair.Value = 0;
            }

            var totalWithDisposed = totalAssets + disposedAssets;
            if (totalWithDisposed > 0)
            {
                progressDisposed.Value = (double)disposedAssets / totalWithDisposed * 100;
            }

            var recentAssets = allAssets.Count(a => a.CreatedDate.HasValue && a.CreatedDate.Value >= oneMonthAgo);
            txtTotalAssetsChange.Text = recentAssets > 0 ? $"+{recentAssets} за месяц" : "В системе";
        }

        private void LoadAdditionalStatistics()
        {
            // Сотрудники и отделы
            txtTotalEmployees.Text = _context.Employees.Count(e => e.IsActive == true).ToString();
            txtTotalDepartments.Text = _context.Departments.Count().ToString();

            // === ИСПРАВЛЕНО: Используем правильное название таблицы ===
            txtTotalCategories.Text = _context.Categories.Count().ToString();
            txtTotalLocations.Text = _context.Locations.Count().ToString();
        }

        private void ConfigureUIByRole()
        {
            if (_currentUser == null) return;

            bool canAccessEmployees = _currentUser.RoleID == 1 || _currentUser.RoleID == 2;

            if (!canAccessEmployees)
            {
                btnGoToEmployees.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(btnGoToAssets, 2);
            }
            else
            {
                btnGoToEmployees.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(btnGoToAssets, 1);
            }
        }

        private void BtnGoToAssets_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Navigate(new AssetsPage(_context, _currentUser));
                mainWindow.SetActiveButton(mainWindow.btnAssets);
                mainWindow.UpdatePageInfo("PackageVariantClosed", "Управление активами",
                    "Просмотр и редактирование активов");
            }
        }

        private void BtnGoToEmployees_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || (_currentUser.RoleID != 1 && _currentUser.RoleID != 2))
            {
                MessageBox.Show("Доступ к разделу 'Сотрудники' ограничен. Требуются права Администратора или Менеджера.",
                    "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Navigate(new EmployeesPage());
                mainWindow.SetActiveButton(mainWindow.btnEmployees);
                mainWindow.UpdatePageInfo("AccountGroup", "Сотрудники", "Управление персоналом");
            }
        }
    }
}
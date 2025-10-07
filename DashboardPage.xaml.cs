using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            _context = context;
            _currentUser = currentUser;
            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            try
            {
                LoadWelcomeMessage();
                LoadAssetStatistics();
                LoadRecentActions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWelcomeMessage()
        {
            if (_currentUser?.Employees != null)
            {
                txtWelcome.Text = $"Добро пожаловать, {_currentUser.Employees.FirstName}!";
            }
            txtLastUpdate.Text = $"Статистика на {DateTime.Now:dd MMMM, HH:mm}";
        }

        private void LoadAssetStatistics()
        {
            // --- ИСПРАВЛЕНИЕ ---
            // Вычисляем дату месяц назад ЗА ПРЕДЕЛАМИ LINQ-запроса
            var oneMonthAgo = DateTime.Now.AddMonths(-1);

            var allAssetsQuery = _context.Assets.Where(a => a.IsActive == true);
            var totalAssetsCount = allAssetsQuery.Count();

            var activeAssetsCount = allAssetsQuery.Count(a => a.StatusID == 1);
            var inRepairAssetsCount = allAssetsQuery.Count(a => a.StatusID == 3); // На ремонте
            var disposedAssetsCount = _context.Assets.Count(a => a.StatusID == 4); // Списанные

            txtTotalAssets.Text = totalAssetsCount.ToString();
            txtActiveAssets.Text = activeAssetsCount.ToString();
            txtInRepair.Text = inRepairAssetsCount.ToString();
            txtDisposed.Text = disposedAssetsCount.ToString();

            if (totalAssetsCount > 0)
            {
                progressActive.Value = (double)activeAssetsCount / totalAssetsCount * 100;
                progressRepair.Value = (double)inRepairAssetsCount / totalAssetsCount * 100;
                txtActiveAssetsPercent.Text = $"{progressActive.Value:F1}% от общего числа";
            }

            var totalWithDisposed = totalAssetsCount + disposedAssetsCount;
            if (totalWithDisposed > 0)
            {
                progressDisposed.Value = (double)disposedAssetsCount / totalWithDisposed * 100;
            }

            // --- ИСПРАВЛЕНИЕ ---
            // Теперь используем переменную `oneMonthAgo` в запросе
            var lastMonthAssets = allAssetsQuery.Count(a => a.CreatedDate >= oneMonthAgo);

            txtTotalAssetsChange.Text = lastMonthAssets > 0 ? $"+{lastMonthAssets} за месяц" : "Без изменений";
            txtTotalAssetsChange.Foreground = lastMonthAssets > 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        private void LoadRecentActions()
        {
            var recentActions = new List<RecentAction>();

            var recentAssets = _context.Assets
                .OrderByDescending(a => a.CreatedDate)
                .Take(5)
                .ToList();
            foreach (var asset in recentAssets)
            {
                recentActions.Add(new RecentAction
                {
                    Icon = "Package",
                    IconColor = "#10B981",
                    IconBackgroundColor = "#ECFDF5",
                    Action = "Добавлен актив",
                    Description = asset.AssetName,
                    Time = GetRelativeTime(asset.CreatedDate ?? DateTime.Now)
                });
            }

            var needAttentionAssets = _context.Assets
                .Where(a => a.StatusID == 3) // На ремонте
                .OrderByDescending(a => a.ModifiedDate)
                .Take(3)
                .ToList();
            foreach (var asset in needAttentionAssets)
            {
                recentActions.Add(new RecentAction
                {
                    Icon = "AlertCircle",
                    IconColor = "#F59E0B",
                    IconBackgroundColor = "#FEF3C7",
                    Action = "Требует внимания",
                    Description = asset.AssetName,
                    Time = GetRelativeTime(asset.ModifiedDate ?? DateTime.Now)
                });
            }

            RecentActionsItemsControl.ItemsSource = recentActions
                .OrderByDescending(a => a.Time == "Только что" ? DateTime.MaxValue : (a.Time.Contains("мин") ? DateTime.Now.AddMinutes(-1) : DateTime.MinValue))
                .Take(8);
        }

        private string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalMinutes < 1) return "Только что";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} мин назад";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} ч назад";
            if (timeSpan.TotalDays < 30) return $"{(int)timeSpan.TotalDays} дн назад";
            return dateTime.ToString("dd.MM.yyyy");
        }

        // --- Обработчики кнопок (остаются без изменений) ---
        private void BtnAddAsset_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new AssetsPage(_context, _currentUser));
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            // Логика экспорта
        }

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            // Логика уведомлений
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            // Логика справки
        }
    }

    public class RecentAction
    {
        public string Icon { get; set; }
        public string IconColor { get; set; }
        public string IconBackgroundColor { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public string Time { get; set; }
    }
}
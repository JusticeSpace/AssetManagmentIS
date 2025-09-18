using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;

namespace AssetManagment
{
    public partial class MainWindow : Window
    {
        private AssetControlDBEntities _context;
        private Users _currentUser;
        private DispatcherTimer _timer;
        public ObservableCollection<AssetViewModel> RecentAssets { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            _context = new AssetControlDBEntities();
            RecentAssets = new ObservableCollection<AssetViewModel>();
            DataContext = this;

            // Временно устанавливаем пользователя
            _currentUser = _context.Users.FirstOrDefault(u => u.Username == "admin");

            // Настройка таймера для обновления времени
            SetupTimer();

            // Загрузка данных
            LoadDashboardData();
            UpdateUserInfo();
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

        private void LoadDashboardData()
        {
            try
            {
                // Обновляем статистику в БД
                _context.sp_UpdateDashboardStats();

                // Загружаем статистику
                var stats = _context.DashboardStats.FirstOrDefault();
                if (stats != null)
                {
                    // Обновляем карточки
                    txtTotalAssets.Text = (stats.TotalAssets ?? 0).ToString("N0");
                    txtActiveAssets.Text = (stats.ActiveAssets ?? 0).ToString("N0");
                    txtInRepair.Text = (stats.InRepairAssets ?? 0).ToString("N0");
                    txtDisposed.Text = (stats.DisposedAssets ?? 0).ToString("N0");

                    // Вычисляем проценты и изменения
                    if (stats.TotalAssets > 0)
                    {
                        var activePercent = (stats.ActiveAssets * 100.0 / stats.TotalAssets) ?? 0;
                        txtActiveAssetsPercent.Text = $"{activePercent:F0}% от общего числа";
                    }

                    // Обновляем информационные строки
                    var lastMonth = _context.Assets
                        .Where(a => a.CreatedDate >= System.Data.Entity.DbFunctions.AddMonths(DateTime.Now, -1))
                        .Count();

                    if (lastMonth > 0)
                    {
                        txtTotalAssetsChange.Text = $"↑ {lastMonth} за последний месяц";
                        txtTotalAssetsChange.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                    }
                    else
                    {
                        txtTotalAssetsChange.Text = "Без изменений за месяц";
                        txtTotalAssetsChange.Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141));
                    }
                }

                // Загружаем последние активы
                LoadRecentAssets();

                // Загружаем статистику по категориям
                LoadCategoryStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRecentAssets()
        {
            RecentAssets.Clear();

            var recentAssets = _context.vw_AssetsFullInfo
                .Where(a => a.IsActive == true)
                .OrderByDescending(a => a.CreatedDate)
                .Take(10)
                .ToList();

            foreach (var asset in recentAssets)
            {
                var statusColor = "#27AE60"; // По умолчанию зеленый
                switch (asset.StatusName)
                {
                    case "На складе":
                        statusColor = "#3498DB";
                        break;
                    case "На ремонте":
                        statusColor = "#F39C12";
                        break;
                    case "Списан":
                        statusColor = "#E74C3C";
                        break;
                }

                RecentAssets.Add(new AssetViewModel
                {
                    Id = asset.AssetCode,
                    Name = asset.AssetName,
                    Category = asset.CategoryName,
                    Status = asset.StatusName,
                    StatusColor = statusColor,
                    DateAdded = asset.CreatedDate?.ToString("dd.MM.yyyy HH:mm") ?? ""
                });
            }

            dgRecentAssets.ItemsSource = RecentAssets;
        }

        private void LoadCategoryStatistics()
        {
            categoryStatsPanel.Children.Clear();

            var categoryStats = _context.vw_AssetsByCategory
                .OrderByDescending(c => c.AssetCount)
                .ToList();

            var totalAssets = categoryStats.Sum(c => c.AssetCount ?? 0);

            var colors = new[] { "#3498DB", "#27AE60", "#F39C12", "#E74C3C", "#9B59B6", "#1ABC9C" };
            var colorIndex = 0;

            foreach (var cat in categoryStats)
            {
                var percentage = totalAssets > 0 ? (cat.AssetCount * 100.0 / totalAssets) ?? 0 : 0;

                var grid = new Grid { Margin = new Thickness(0, 8, 0, 8) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

                // Цветной индикатор
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[colorIndex % colors.Length])),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(ellipse, 0);
                grid.Children.Add(ellipse);

                // Название категории
                var nameText = new TextBlock
                {
                    Text = cat.CategoryName,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0)
                };
                Grid.SetColumn(nameText, 1);
                grid.Children.Add(nameText);

                // Количество
                var countText = new TextBlock
                {
                    Text = cat.AssetCount?.ToString() ?? "0",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(countText, 2);
                grid.Children.Add(countText);

                // Процент
                var percentText = new TextBlock
                {
                    Text = $"{percentage:F0}%",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(percentText, 3);
                grid.Children.Add(percentText);

                categoryStatsPanel.Children.Add(grid);
                colorIndex++;
            }

            // Если нет категорий
            if (!categoryStats.Any())
            {
                var emptyText = new TextBlock
                {
                    Text = "Нет данных по категориям",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                };
                categoryStatsPanel.Children.Add(emptyText);
            }
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

        // Обработчики событий
        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();
        }

        private void BtnAssets_Click(object sender, RoutedEventArgs e)
        {
            var assetsWindow = new AssetsWindow(_context, _currentUser);
            assetsWindow.ShowDialog();
            LoadDashboardData(); // Обновить данные после закрытия
        }

        private void BtnEmployees_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Модуль управления сотрудниками будет доступен в следующей версии",
                "АКТИВ+ | В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLocations_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Модуль управления локациями будет доступен в следующей версии",
                "АКТИВ+ | В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Модуль отчетов будет доступен в следующей версии",
                "АКТИВ+ | В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Настройки системы будут доступны в следующей версии",
                "АКТИВ+ | В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();

            // Показываем уведомление об обновлении
            var notification = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 0),
                Child = new TextBlock
                {
                    Text = "✓ Данные успешно обновлены",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Medium
                }
            };

            Grid.SetRow(notification, 1);
            (Content as Grid).Children.Add(notification);

            // Удаляем уведомление через 3 секунды
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                (Content as Grid).Children.Remove(notification);
                timer.Stop();
            };
            timer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _timer?.Stop();
            _context?.Dispose();
        }
    }

    // ViewModel для отображения активов
    public class AssetViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public string StatusColor { get; set; }
        public string DateAdded { get; set; }
    }
}
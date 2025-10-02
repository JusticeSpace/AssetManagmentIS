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

        public DashboardPage() : this(new AssetControlDBEntities(), App.CurrentUser)
        {
        }

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
                LoadCategories();
                LoadRecentActions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWelcomeMessage()
        {
            if (_currentUser?.Employees != null)
            {
                var employee = _currentUser.Employees;
                txtWelcome.Text = $"Добро пожаловать, {employee.FirstName}!";
            }

            txtLastUpdate.Text = $"Последнее обновление: {DateTime.Now:HH:mm}";
        }

        private void LoadAssetStatistics()
        {
            var allAssets = _context.Assets.Where(a => a.IsActive == true).ToList();
            var activeAssets = allAssets.Where(a => a.StatusID == 1).Count();
            var inRepairAssets = allAssets.Where(a => a.StatusID == 3).Count(); // На ремонте
            var disposedAssets = _context.Assets.Where(a => a.StatusID == 4).Count(); // Списанные

            // Обновляем UI
            txtTotalAssets.Text = allAssets.Count.ToString();
            txtActiveAssets.Text = activeAssets.ToString();
            txtInRepair.Text = inRepairAssets.ToString();
            txtDisposed.Text = disposedAssets.ToString();

            // Рассчитываем проценты и обновляем прогресс-бары
            if (allAssets.Count > 0)
            {
                var activePercent = (activeAssets * 100.0 / allAssets.Count);
                var repairPercent = (inRepairAssets * 100.0 / allAssets.Count);
                var disposedPercent = (disposedAssets * 100.0 / (_context.Assets.Count())); // От всех активов

                txtActiveAssetsPercent.Text = $"{activePercent:F1}% от общего числа";

                progressActive.Value = activePercent;
                progressRepair.Value = repairPercent;
                progressDisposed.Value = disposedPercent;
            }

            // Изменение за последний месяц
            var lastMonthAssets = allAssets.Where(a => a.CreatedDate >= DateTime.Now.AddMonths(-1)).Count();
            if (lastMonthAssets > 0)
            {
                txtTotalAssetsChange.Text = $"+{lastMonthAssets} за месяц";
                txtTotalAssetsChange.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else
            {
                txtTotalAssetsChange.Text = "Без изменений";
                txtTotalAssetsChange.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
            }

            txtDisposedInfo.Text = $"За {DateTime.Now.Year} год";
        }

        private void LoadCategories()
        {
            var categories = new List<CategoryStats>();

            var categoryGroups = _context.Assets
                .Where(a => a.IsActive == true)
                .GroupBy(a => a.CategoryID)
                .Select(g => new
                {
                    CategoryID = g.Key,
                    Count = g.Count()
                })
                .ToList();

            var totalAssets = _context.Assets.Count(a => a.IsActive == true);

            foreach (var group in categoryGroups)
            {
                var category = _context.Categories.FirstOrDefault(c => c.CategoryID == group.CategoryID);
                if (category != null)
                {
                    categories.Add(new CategoryStats
                    {
                        CategoryName = category.CategoryName,
                        Count = group.Count,
                        Percentage = totalAssets > 0 ? (group.Count * 100.0 / totalAssets) : 0
                    });
                }
            }

            CategoriesItemsControl.ItemsSource = categories.OrderByDescending(c => c.Count);
        }

        private void LoadRecentActions()
        {
            var recentActions = new List<RecentAction>();

            // Получаем последние добавленные активы
            var recentAssets = _context.Assets
                .Where(a => a.IsActive == true)
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

            // Добавляем активы требующие внимания
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

            RecentActionsItemsControl.ItemsSource = recentActions.Take(8);
        }

        private string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "Только что";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} мин назад";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} ч назад";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays} дн назад";

            return dateTime.ToString("dd.MM.yyyy");
        }

        public void RefreshData()
        {
            LoadDashboardData();
        }

        // Обработчики событий
        private void BtnAddAsset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var assetsPage = new AssetsPage(_context, _currentUser);
                ((MainWindow)Window.GetWindow(this)).MainFrame.Navigate(assetsPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия страницы активов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnScanAsset_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("🔍 Функция сканирования QR-кодов\n\nБудет доступна в следующей версии.\nПозволит быстро находить активы по штрих-коду.",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv|Excel файлы (*.xlsx)|*.xlsx",
                    FileName = $"Assets_Report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var assets = _context.vw_AssetsFullInfo.ToList();
                    var csv = new System.Text.StringBuilder();

                    // Заголовки
                    csv.AppendLine("ID;Код;Название;Категория;Статус;Местоположение;Ответственный;Цена;Дата покупки");

                    // Данные
                    foreach (var asset in assets)
                    {
                        csv.AppendLine($"{asset.AssetID};{asset.AssetCode};{asset.AssetName};" +
                                     $"{asset.CategoryName};{asset.StatusName};{asset.LocationName};" +
                                     $"{asset.ResponsiblePerson};{asset.PurchasePrice};" +
                                     $"{asset.PurchaseDate:dd.MM.yyyy}");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(),
                        System.Text.Encoding.UTF8);

                    MessageBox.Show($"✅ Отчет успешно экспортирован!\n\nФайл: {saveDialog.FileName}\nЗаписей: {assets.Count}",
                        "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInventory_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("📋 Модуль инвентаризации\n\nПозволит:\n• Проводить плановые проверки\n• Сверять фактическое наличие\n• Генерировать акты инвентаризации\n\nБудет доступен в следующей версии.",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            var notifications = new System.Text.StringBuilder();
            notifications.AppendLine("🔔 ЦЕНТР УВЕДОМЛЕНИЙ\n");

            // Активы требующие ремонта
            var needRepair = _context.Assets.Count(a => a.StatusID == 3);
            if (needRepair > 0)
                notifications.AppendLine($"⚠️ {needRepair} активов требуют ремонта");

            // Недавно добавленные
            var recentlyAdded = _context.Assets
                .Count(a => a.CreatedDate >= DateTime.Now.AddDays(-7));
            if (recentlyAdded > 0)
                notifications.AppendLine($"✅ {recentlyAdded} новых активов за неделю");

            // Дорогие активы
            var expensiveAssets = _context.Assets
                .Count(a => a.PurchasePrice > 100000);
            if (expensiveAssets > 0)
                notifications.AppendLine($"💰 {expensiveAssets} дорогостоящих активов (>100,000₽)");

            // Активы без ответственного
            var unassignedAssets = _context.Assets
                .Count(a => a.ResponsibleEmployeeID == null && a.IsActive == true);
            if (unassignedAssets > 0)
                notifications.AppendLine($"👤 {unassignedAssets} активов без ответственного");

            if (needRepair == 0 && recentlyAdded == 0 && expensiveAssets == 0 && unassignedAssets == 0)
                notifications.AppendLine("✨ Все в порядке! Уведомлений нет.");

            MessageBox.Show(notifications.ToString(), "Центр уведомлений",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpText = @"📚 СПРАВКА ПО СИСТЕМЕ АКТИВ+

🏠 ГЛАВНАЯ ПАНЕЛЬ
• Статистика активов в реальном времени
• Графики и аналитика
• Быстрый доступ к функциям

📦 УПРАВЛЕНИЕ АКТИВАМИ
• Добавление и редактирование
• Отслеживание статусов
• Назначение ответственных

📊 АНАЛИТИКА
• Распределение по категориям
• Динамика изменений
• Отчеты и экспорт

🚀 БЫСТРЫЕ ДЕЙСТВИЯ
• Добавить актив - создание записи
• Сканировать - поиск по QR-коду
• Экспорт - выгрузка в CSV/Excel
• Инвентаризация - проверка наличия
• Уведомления - важные события

❓ ПОДДЕРЖКА
Email: support@activplus.ru
Телефон: +7 (495) 123-45-67

Версия: 1.0.0 | © 2024 АКТИВ+";

            MessageBox.Show(helpText, "Справочная информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Классы для данных
    public class CategoryStats
    {
        public string CategoryName { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
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
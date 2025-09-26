using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Documents;

namespace AssetManagment
{
    public partial class AssetsWindow : Window
    {
        private AssetControlDBEntities _context;
        private Users _currentUser;
        private ObservableCollection<AssetItemViewModel> _assets;
        private DispatcherTimer _refreshTimer;
        private string _currentSearchText = "";
        private string _currentStatusFilter = "all";
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;

        public AssetsWindow(AssetControlDBEntities context, Users currentUser)
        {
            InitializeComponent();
            _context = context;
            _currentUser = currentUser;
            _assets = new ObservableCollection<AssetItemViewModel>();

            InitializeWindow();
            LoadAssets();
        }

        private void InitializeWindow()
        {
            // Настройка таймера для автообновления
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMinutes(5);
            _refreshTimer.Tick += (s, e) => LoadAssets();
            _refreshTimer.Start();

            UpdateLastRefreshTime();
        }

        private void LoadAssets(string searchText = "", string statusFilter = "all", int page = 1)
        {
            try
            {
                _currentSearchText = searchText;
                _currentStatusFilter = statusFilter;
                _currentPage = page;

                var query = _context.vw_AssetsFullInfo
                    .Where(a => a.IsActive == true);

                // Применяем поиск
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    query = query.Where(a =>
                        a.AssetCode.Contains(searchText) ||
                        a.AssetName.Contains(searchText) ||
                        a.CategoryName.Contains(searchText) ||
                        a.LocationName.Contains(searchText) ||
                        a.ResponsiblePerson.Contains(searchText));

                    btnClearSearch.Visibility = Visibility.Visible;
                }
                else
                {
                    btnClearSearch.Visibility = Visibility.Collapsed;
                }

                // Применяем фильтр по статусу (используем StatusName вместо StatusID)
                switch (statusFilter)
                {
                    case "active":
                        query = query.Where(a => a.StatusName == "Активен");
                        break;
                    case "repair":
                        query = query.Where(a => a.StatusName == "На ремонте");
                        break;
                }

                // Подсчитываем общее количество
                var totalCount = query.Count();
                _totalPages = (int)Math.Ceiling(totalCount / (double)_pageSize);

                // Применяем пагинацию
                var assets = query
                    .OrderByDescending(a => a.CreatedDate)
                    .Skip((page - 1) * _pageSize)
                    .Take(_pageSize)
                    .ToList();

                // Преобразуем в ViewModel
                _assets.Clear();
                foreach (var asset in assets)
                {
                    _assets.Add(new AssetItemViewModel
                    {
                        AssetID = asset.AssetID,
                        AssetCode = asset.AssetCode,
                        AssetName = asset.AssetName,
                        Model = asset.Model ?? "",
                        CategoryName = asset.CategoryName,
                        CategoryIcon = GetCategoryIcon(asset.CategoryName),
                        StatusName = asset.StatusName,
                        StatusColor = GetStatusColor(asset.StatusName),
                        LocationName = asset.LocationName,
                        ResponsiblePerson = asset.ResponsiblePerson ?? "Не назначен",
                        PurchasePrice = asset.PurchasePrice,
                        IsSelected = false
                    });
                }

                dgAssets.ItemsSource = _assets;
                UpdatePagination();
                UpdateLastRefreshTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}",
                    "АКТИВ+ | Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetCategoryIcon(string categoryName)
        {
            switch (categoryName?.ToLower())
            {
                case "компьютеры":
                    return "💻";
                case "мебель":
                    return "🪑";
                case "транспорт":
                    return "🚗";
                case "оргтехника":
                    return "🖨️";
                default:
                    return "📦";
            }
        }

        private string GetStatusColor(string statusName)
        {
            switch (statusName)
            {
                case "Активен":
                    return "#27AE60";
                case "На складе":
                    return "#3498DB";
                case "На ремонте":
                    return "#F39C12";
                case "Списан":
                    return "#E74C3C";
                default:
                    return "#95A5A6";
            }
        }

        private void UpdatePagination()
        {
            // Здесь можно обновить кнопки пагинации
            // В реальном приложении это будет более сложная логика
        }

        private void UpdateLastRefreshTime()
        {
            if (FindName("txtLastUpdate") is Run lastUpdate)
            {
                var now = DateTime.Now;
                lastUpdate.Text = $"{now:HH:mm:ss}";
            }
        }

        // Обработчики событий
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Временное решение пока не создано окно редактирования
            MessageBox.Show("Для добавления нового актива:\n\n" +
                "1. Введите код актива\n" +
                "2. Укажите наименование и категорию\n" +
                "3. Выберите локацию и ответственного\n" +
                "4. Нажмите 'Сохранить'\n\n" +
                "Эта функция будет доступна в следующей версии.",
                "АКТИВ+ | Добавление актива",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgAssets.SelectedItem as AssetItemViewModel;
            if (selected != null)
            {
                MessageBox.Show($"Редактирование актива:\n\n" +
                    $"Код: {selected.AssetCode}\n" +
                    $"Наименование: {selected.AssetName}\n" +
                    $"Категория: {selected.CategoryName}\n" +
                    $"Статус: {selected.StatusName}\n\n" +
                    "Функция редактирования будет доступна в следующей версии.",
                    "АКТИВ+ | Редактирование",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ShowNotification("Выберите актив для редактирования", NotificationType.Warning);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _assets.Where(a => a.IsSelected).ToList();

            if (!selectedItems.Any())
            {
                var selected = dgAssets.SelectedItem as AssetItemViewModel;
                if (selected != null)
                    selectedItems.Add(selected);
            }

            if (selectedItems.Any())
            {
                var message = selectedItems.Count == 1
                    ? $"Вы уверены, что хотите удалить актив '{selectedItems[0].AssetName}'?"
                    : $"Вы уверены, что хотите удалить {selectedItems.Count} активов?";

                var result = MessageBox.Show(message, "АКТИВ+ | Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        foreach (var item in selectedItems)
                        {
                            var asset = _context.Assets.Find(item.AssetID);
                            if (asset != null)
                            {
                                asset.IsActive = false;
                                asset.ModifiedDate = DateTime.Now;
                                asset.ModifiedByUserID = _currentUser.UserID;
                            }
                        }

                        _context.SaveChanges();
                        LoadAssets();

                        var successMessage = selectedItems.Count == 1
                            ? "Актив успешно удален"
                            : $"{selectedItems.Count} активов успешно удалено";

                        ShowNotification(successMessage, NotificationType.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"Ошибка при удалении: {ex.Message}", NotificationType.Error);
                    }
                }
            }
            else
            {
                ShowNotification("Выберите активы для удаления", NotificationType.Warning);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Здесь будет логика экспорта в Excel
                ShowNotification("Функция экспорта будет доступна в следующей версии", NotificationType.Info);
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка экспорта: {ex.Message}", NotificationType.Error);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Задержка поиска для оптимизации
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                LoadAssets(txtSearch.Text);
            };
            timer.Start();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            LoadAssets();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAssets(_currentSearchText, _currentStatusFilter, _currentPage);
            ShowNotification("Данные обновлены", NotificationType.Success);
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = (sender as CheckBox)?.IsChecked ?? false;
            foreach (var asset in _assets)
            {
                asset.IsSelected = isChecked;
            }

            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            var selectedCount = _assets.Count(a => a.IsSelected);
            if (selectedCount > 0)
            {
                if (FindName("btnClearSelection") is Button clearButton)
                {
                    clearButton.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (FindName("btnClearSelection") is Button clearButton)
                {
                    clearButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ShowNotification(string message, NotificationType type)
        {
            // Создаем уведомление
            var notification = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 12, 20, 12), // Исправлено
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.2,
                    BlurRadius = 10,
                    ShadowDepth = 2
                }
            };

            // Устанавливаем цвет в зависимости от типа
            switch (type)
            {
                case NotificationType.Success:
                    notification.Background = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                    break;
                case NotificationType.Warning:
                    notification.Background = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                    break;
                case NotificationType.Error:
                    notification.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    break;
                default:
                    notification.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    break;
            }

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium,
                FontSize = 14
            };

            notification.Child = textBlock;

            // Добавляем в Grid
            Grid.SetRow(notification, 0);
            Grid.SetRowSpan(notification, 2);
            (Content as Grid).Children.Add(notification);

            // Удаляем через 3 секунды
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
            _refreshTimer?.Stop();
        }

        private enum NotificationType
        {
            Success,
            Warning,
            Error,
            Info
        }
    }

    // ViewModel для элемента списка
    public class AssetItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int AssetID { get; set; }
        public string AssetCode { get; set; }
        public string AssetName { get; set; }
        public string Model { get; set; }
        public string CategoryName { get; set; }
        public string CategoryIcon { get; set; }
        public string StatusName { get; set; }
        public string StatusColor { get; set; }
        public string LocationName { get; set; }
        public string ResponsiblePerson { get; set; }
        public decimal? PurchasePrice { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
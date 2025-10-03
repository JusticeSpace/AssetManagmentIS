using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace AssetManagment.Pages
{
    public partial class AssetsPage : Page
    {
        private readonly AssetControlDBEntities _context;
        private Users _currentUser;
        private ObservableCollection<AssetItemViewModel> _assets;
        private DispatcherTimer _refreshTimer;
        private string _currentSearchText = "";
        private int? _currentStatusFilter = null;
        private int? _currentCategoryFilter = null;
        private int? _currentLocationFilter = null;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        private int _totalItems = 0;

        public AssetsPage(AssetControlDBEntities context, Users currentUser)
        {
            InitializeComponent();
            _context = context;
            _currentUser = currentUser;
            _assets = new ObservableCollection<AssetItemViewModel>();

            // Сначала инициализируем страницу
            InitializePage();

            // Затем загружаем фильтры
            LoadFilters();

            // И только потом загружаем данные
            LoadAssets();
        }

        private void InitializePage()
        {
            // Настройка таймера для автообновления
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMinutes(5);
            _refreshTimer.Tick += (s, e) => LoadAssets();
            _refreshTimer.Start();

            UpdateLastRefreshTime();
        }

        private void LoadFilters()
        {
            // Загрузка категорий
            var categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
            cmbCategoryFilter.Items.Clear();
            cmbCategoryFilter.Items.Add(new ComboBoxItem { Content = "Все категории", IsSelected = true });
            foreach (var category in categories)
            {
                cmbCategoryFilter.Items.Add(new ComboBoxItem
                {
                    Content = category.CategoryName,
                    Tag = category.CategoryID
                });
            }

            // Загрузка локаций
            var locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
            cmbLocationFilter.Items.Clear();
            cmbLocationFilter.Items.Add(new ComboBoxItem { Content = "Все локации", IsSelected = true });
            foreach (var location in locations)
            {
                cmbLocationFilter.Items.Add(new ComboBoxItem
                {
                    Content = location.LocationName,
                    Tag = location.LocationID
                });
            }
        }
        public void RefreshData()
        {
            LoadAssets();
        }


        private void LoadAssets()
        {
            try
            {
                var query = _context.Assets.Where(a => a.IsActive == true);

                // Применяем поиск - ИСПРАВЛЕНО (убрали InventoryNumber)
                if (!string.IsNullOrWhiteSpace(_currentSearchText))
                {
                    query = query.Where(a =>
                        a.AssetCode.Contains(_currentSearchText) ||
                        a.AssetName.Contains(_currentSearchText) ||
                        a.SerialNumber.Contains(_currentSearchText));
                }

                // Остальной код остается без изменений...
                // Применяем фильтр по статусу
                if (_currentStatusFilter.HasValue)
                {
                    query = query.Where(a => a.StatusID == _currentStatusFilter.Value);
                }

                // Применяем фильтр по категории
                if (_currentCategoryFilter.HasValue)
                {
                    query = query.Where(a => a.CategoryID == _currentCategoryFilter.Value);
                }

                // Применяем фильтр по локации
                if (_currentLocationFilter.HasValue)
                {
                    query = query.Where(a => a.LocationID == _currentLocationFilter.Value);
                }

                // Подсчитываем общее количество
                _totalItems = query.Count();
                _totalPages = _pageSize > 0 ? (int)Math.Ceiling(_totalItems / (double)_pageSize) : 1;

                // Применяем пагинацию
                var pagedQuery = query.OrderByDescending(a => a.AssetID);

                if (_pageSize > 0)
                {
                    pagedQuery = (IOrderedQueryable<Assets>)pagedQuery
                        .Skip((_currentPage - 1) * _pageSize)
                        .Take(_pageSize);
                }

                var assets = pagedQuery.ToList();

                // Преобразуем в ViewModel
                _assets.Clear();
                foreach (var asset in assets)
                {
                    var category = _context.Categories.FirstOrDefault(c => c.CategoryID == asset.CategoryID);
                    var status = _context.AssetStatuses.FirstOrDefault(s => s.StatusID == asset.StatusID);
                    var location = _context.Locations.FirstOrDefault(l => l.LocationID == asset.LocationID);
                    var employee = _context.Employees.FirstOrDefault(e => e.EmployeeID == asset.ResponsibleEmployeeID);

                    _assets.Add(new AssetItemViewModel
                    {
                        AssetID = asset.AssetID,
                        AssetCode = asset.AssetCode,
                        AssetName = asset.AssetName,
                        Model = asset.Model ?? "",
                        CategoryName = category?.CategoryName ?? "Без категории",
                        CategoryIcon = GetCategoryIcon(category?.CategoryName),
                        StatusName = status?.StatusName ?? "Неизвестно",
                        StatusColor = GetStatusColor(status?.StatusName),
                        LocationName = location?.LocationName ?? "Не указана",
                        ResponsiblePerson = employee != null ?
                            $"{employee.FirstName} {employee.LastName}" : "Не назначен",
                        PurchasePrice = asset.PurchasePrice,
                        IsSelected = false
                    });
                }

                dgAssets.ItemsSource = _assets;
                UpdatePagination();
                UpdateSelectionInfo();
                UpdateLastRefreshTime();
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка при загрузке данных: {ex.Message}", MessageType.Error);
            }
        }

        private string GetCategoryIcon(string categoryName)
        {
            switch (categoryName?.ToLower())
            {
                case "компьютеры": return "💻";
                case "мебель": return "🪑";
                case "транспорт": return "🚗";
                case "оргтехника": return "🖨️";
                case "оборудование": return "⚙️";
                case "инструменты": return "🔧";
                default: return "📦";
            }
        }

        private string GetStatusColor(string statusName)
        {
            var converter = new BrushConverter();
            switch (statusName)
            {
                case "Активен": return "#4CAF50";
                case "На складе": return "#2196F3";
                case "На ремонте": return "#FF9800";
                case "Списан": return "#F44336";
                default: return "#9E9E9E";
            }
        }

        private void UpdatePagination()
        {
            txtPageInfo.Text = $"{_currentPage} / {_totalPages}";
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
        }

        private void UpdateSelectionInfo()
        {
            var selectedCount = _assets.Count(a => a.IsSelected);

            if (selectedCount > 0)
            {
                txtSelectionInfo.Text = $"Выбрано: {selectedCount} из {_totalItems}";
                btnClearSelection.Visibility = Visibility.Visible;
            }
            else
            {
                txtSelectionInfo.Text = $"Всего записей: {_totalItems}";
                btnClearSelection.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateLastRefreshTime()
        {
            txtLastUpdate.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
        }

        private void ShowNotification(string message, MessageType type = MessageType.Info)
        {
            var messageQueue = NotificationSnackbar.MessageQueue;
            messageQueue?.Enqueue(message, null, null, null, false, true,
                TimeSpan.FromSeconds(type == MessageType.Error ? 5 : 3));
        }

        // Обработчики событий
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit()) return;

            var win = new Windows.AssetEditorWindow(_context, App.CurrentUser);
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
                RefreshData();
        }


        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgAssets.SelectedItem as AssetItemViewModel;
            if (selected != null)
            {
                ShowNotification($"Редактирование: {selected.AssetName}", MessageType.Info);
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
                    ? $"Удалить актив '{selectedItems[0].AssetName}'?"
                    : $"Удалить {selectedItems.Count} активов?";

                // Здесь должен быть диалог подтверждения
                MessageBox.Show(message, "Подтверждение", MessageBoxButton.YesNo);
            }
            else
            {
                ShowNotification("Выберите активы для удаления", MessageType.Warning);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel файлы (*.xlsx)|*.xlsx|CSV файлы (*.csv)|*.csv",
                    FileName = $"Assets_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Здесь будет логика экспорта
                    ShowNotification("Данные успешно экспортированы", MessageType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка экспорта: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAssets();
            ShowNotification("Данные обновлены", MessageType.Success);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = txtSearch.Text;
            _currentPage = 1;

            // Задержка поиска
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                LoadAssets();
            };
            timer.Start();
        }

        private void CmbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbStatusFilter == null || !IsLoaded) return;

            if (cmbStatusFilter.SelectedIndex == 0)
                _currentStatusFilter = null;
            else
            {
                switch (cmbStatusFilter.SelectedIndex)
                {
                    case 1: _currentStatusFilter = 1; break; // Активные
                    case 2: _currentStatusFilter = 2; break; // На ремонте
                    case 3: _currentStatusFilter = 4; break; // На складе
                    case 4: _currentStatusFilter = 3; break; // Списанные
                }
            }
            _currentPage = 1;
            LoadAssets();
        }

        private void CmbCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCategoryFilter == null || !IsLoaded) return;

            var selected = cmbCategoryFilter.SelectedItem as ComboBoxItem;
            _currentCategoryFilter = selected?.Tag as int?;
            _currentPage = 1;
            LoadAssets();
        }

        private void CmbLocationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLocationFilter == null || !IsLoaded) return;

            var selected = cmbLocationFilter.SelectedItem as ComboBoxItem;
            _currentLocationFilter = selected?.Tag as int?;
            _currentPage = 1;
            LoadAssets();
        }

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPageSize == null || !IsLoaded) return;

            if (cmbPageSize.SelectedIndex == 3) // "Все"
                _pageSize = 0;
            else
                _pageSize = int.Parse((cmbPageSize.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "20");

            _currentPage = 1;
            LoadAssets();
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

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var asset in _assets)
            {
                asset.IsSelected = false;
            }
            chkSelectAll.IsChecked = false;
            UpdateSelectionInfo();
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadAssets();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                LoadAssets();
            }
        }
        private bool CanEdit(bool showMessage = true)
        {
            // используем пользователя из конструктора или из App
            var user = _currentUser ?? App.CurrentUser;
            bool allowed = user != null && (user.RoleID == 1 || user.RoleID == 2);

            if (!allowed && showMessage)
            {
                MessageBox.Show("Доступно только для Администратора и Менеджера.",
                                "Нет прав", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return allowed;
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            var asset = (sender as Button)?.CommandParameter as AssetItemViewModel;
            if (asset != null)
            {
                ShowNotification($"Просмотр: {asset.AssetName}", MessageType.Info);
            }
        }

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit()) return;

            var btn = sender as Button;
            if (btn?.CommandParameter == null) return;

            var item = btn.CommandParameter;
            var prop = item.GetType().GetProperty("AssetID");
            if (prop == null) return;

            int assetId = (int)prop.GetValue(item, null);

            var win = new Windows.AssetEditorWindow(_context, App.CurrentUser, assetId);
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
                RefreshData();
        }

        private void BtnMove_Click(object sender, RoutedEventArgs e)
        {
            var asset = (sender as Button)?.CommandParameter as AssetItemViewModel;
            if (asset != null)
            {
                ShowNotification($"Перемещение: {asset.AssetName}", MessageType.Info);
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            var asset = (sender as Button)?.CommandParameter as AssetItemViewModel;
            if (asset != null)
            {
                ShowNotification($"История: {asset.AssetName}", MessageType.Info);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var asset = (sender as Button)?.CommandParameter as AssetItemViewModel;
            if (asset != null)
            {
                ShowNotification($"Удаление: {asset.AssetName}", MessageType.Warning);
            }
        }

        private enum MessageType
        {
            Success,
            Warning,
            Error,
            Info
        }
    }

    // ViewModel остается тот же
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
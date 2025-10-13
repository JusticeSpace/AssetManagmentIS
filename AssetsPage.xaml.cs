using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace AssetManagment.Pages
{
    public partial class AssetsPage : Page
    {
        private readonly AssetControlDBEntities _context;
        private readonly Users _currentUser;
        private readonly ObservableCollection<AssetViewModel> _assets;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;

        public AssetsPage(AssetControlDBEntities context, Users currentUser)
        {
            InitializeComponent();
            _context = context;
            _currentUser = currentUser;
            _assets = new ObservableCollection<AssetViewModel>();
            dgAssets.ItemsSource = _assets;

            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFilters();
            RefreshData();
            ApplyRolePermissions();
        }

        private void LoadFilters()
        {
            try
            {
                var statuses = new List<object> { new { StatusID = (int?)null, StatusName = "Все статусы" } };
                statuses.AddRange(_context.AssetStatuses.Select(s => new { StatusID = (int?)s.StatusID, s.StatusName }).OrderBy(s => s.StatusName).ToList());
                cmbStatusFilter.ItemsSource = statuses;
                cmbStatusFilter.DisplayMemberPath = "StatusName";
                cmbStatusFilter.SelectedValuePath = "StatusID";

                var categories = new List<object> { new { CategoryID = (int?)null, CategoryName = "Все категории" } };
                categories.AddRange(_context.Categories.Select(c => new { CategoryID = (int?)c.CategoryID, c.CategoryName }).OrderBy(c => c.CategoryName).ToList());
                cmbCategoryFilter.ItemsSource = categories;
                cmbCategoryFilter.DisplayMemberPath = "CategoryName";
                cmbCategoryFilter.SelectedValuePath = "CategoryID";

                var locations = new List<object> { new { LocationID = (int?)null, LocationName = "Все локации" } };
                locations.AddRange(_context.Locations.Select(l => new { LocationID = (int?)l.LocationID, l.LocationName }).OrderBy(l => l.LocationName).ToList());
                cmbLocationFilter.ItemsSource = locations;
                cmbLocationFilter.DisplayMemberPath = "LocationName";
                cmbLocationFilter.SelectedValuePath = "LocationID";
            }
            catch (Exception ex) { ShowNotification($"Ошибка загрузки фильтров: {ex.Message}", true); }
        }

        public void RefreshData()
        {
            try
            {
                IQueryable<Assets> query = _context.Assets;

                var searchText = txtSearch.Text.ToLower();
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    query = query.Where(a => a.AssetCode.ToLower().Contains(searchText) ||
                                           a.AssetName.ToLower().Contains(searchText) ||
                                           (a.Model != null && a.Model.ToLower().Contains(searchText)) ||
                                           (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(searchText)));
                }

                if (cmbStatusFilter.SelectedValue != null)
                {
                    int statusId = (int)cmbStatusFilter.SelectedValue;
                    query = query.Where(a => a.StatusID == statusId);
                }
                if (cmbCategoryFilter.SelectedValue != null)
                {
                    int categoryId = (int)cmbCategoryFilter.SelectedValue;
                    query = query.Where(a => a.CategoryID == categoryId);
                }
                if (cmbLocationFilter.SelectedValue != null)
                {
                    int locationId = (int)cmbLocationFilter.SelectedValue;
                    query = query.Where(a => a.LocationID == locationId);
                }

                var totalItems = query.Count();
                txtTotalCount.Text = totalItems.ToString();
                _totalPages = (int)Math.Ceiling(totalItems / (double)_pageSize);
                if (_totalPages == 0) _totalPages = 1;
                if (_currentPage > _totalPages) _currentPage = _totalPages;

                var pagedAssetIds = query.OrderByDescending(a => a.AssetID)
                                         .Skip((_currentPage - 1) * _pageSize)
                                         .Take(_pageSize)
                                         .Select(a => a.AssetID)
                                         .ToList();

                var results = _context.vw_AssetsFullInfo
                                      .Where(v => pagedAssetIds.Contains(v.AssetID))
                                      .AsEnumerable()
                                      .OrderByDescending(v => pagedAssetIds.IndexOf(v.AssetID))
                                      .ToList();

                _assets.Clear();
                foreach (var item in results)
                {
                    _assets.Add(new AssetViewModel(item));
                }

                txtPageInfo.Text = $"{_currentPage} / {_totalPages}";
                btnPrevPage.IsEnabled = _currentPage > 1;
                btnNextPage.IsEnabled = _currentPage < _totalPages;
                txtLastUpdate.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка загрузки данных: {ex.Message}", true);
            }
        }

        // === ИСПРАВЛЕННЫЙ МЕТОД РАЗГРАНИЧЕНИЯ ПРАВ ===
        private void ApplyRolePermissions()
        {
            var user = _currentUser ?? App.CurrentUser;
            bool isManagerOrAdmin = user != null && (user.RoleID == 1 || user.RoleID == 2);

            // Скрываем кнопки управления для обычных пользователей
            btnAdd.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnEdit.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnDelete.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;

            // === НОВОЕ: Скрываем кнопку управления категориями ===
            btnManageCategories.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;

            // Скрываем колонку действий в таблице
            var actionsColumn = dgAssets.Columns.LastOrDefault();
            if (actionsColumn != null)
            {
                actionsColumn.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name) return element;
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private bool CanEdit(bool showMessage = true)
        {
            var user = _currentUser ?? App.CurrentUser;
            bool allowed = user != null && (user.RoleID == 1 || user.RoleID == 2);
            if (!allowed && showMessage)
            {
                MessageBox.Show("Доступно только для Администратора и Менеджера.", "Нет прав", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return allowed;
        }

        private void ShowNotification(string message, bool isError = false)
        {
            if (NotificationSnackbar.MessageQueue != null)
            {
                NotificationSnackbar.MessageQueue.Enqueue(message);
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit()) return;
            var win = new Windows.AssetEditorWindow(_context, _currentUser);
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true) RefreshData();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit()) return;

            var selectedAsset = dgAssets.SelectedItem as AssetViewModel;
            if (selectedAsset != null)
            {
                var win = new Windows.AssetEditorWindow(_context, _currentUser, selectedAsset.FullInfo.AssetID);
                win.Owner = Window.GetWindow(this);
                if (win.ShowDialog() == true) RefreshData();
            }
            else
            {
                ShowNotification("Сначала выберите один актив для редактирования.", true);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit()) return;
            var selectedItems = _assets.Where(a => a.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                ShowNotification("Выберите один или несколько активов для списания.", true);
                return;
            }
            var result = MessageBox.Show($"Вы уверены, что хотите списать {selectedItems.Count} актив(ов)?", "Подтверждение списания", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            DisposeAssets(selectedItems.Select(i => i.FullInfo.AssetID).ToList());
        }

        // === НОВЫЙ МЕТОД: Управление категориями ===
        private void BtnManageCategories_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit())
            {
                MessageBox.Show("Управление категориями доступно только Администратору и Менеджеру.",
                    "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var win = new Windows.CategoryManagerWindow(_context, _currentUser ?? App.CurrentUser);
                win.Owner = Window.GetWindow(this);

                if (win.ShowDialog() == true)
                {
                    LoadFilters();
                    RefreshData();
                    ShowNotification("Данные обновлены после изменения категорий");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия окна управления категориями:\n\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDisposeItem_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit()) return;

            var button = sender as Button;
            if (button != null)
            {
                var vm = button.CommandParameter as AssetViewModel;
                if (vm != null)
                {
                    var result = MessageBox.Show($"Вы уверены, что хотите списать актив '{vm.FullInfo.AssetName}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes) return;
                    DisposeAssets(new List<int> { vm.FullInfo.AssetID });
                }
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.RoleID != 1)
            {
                MessageBox.Show("Эта операция доступна только Администратору.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            if (button != null)
            {
                var vm = button.CommandParameter as AssetViewModel;
                if (vm != null)
                {
                    var result = MessageBox.Show($"Вы уверены, что хотите НАВСЕГДА удалить актив '{vm.FullInfo.AssetName}'?\n\nЭто действие НЕОБРАТИМО!", "КРИТИЧЕСКОЕ ДЕЙСТВИЕ", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result != MessageBoxResult.Yes) return;
                    DeleteAssets(new List<int> { vm.FullInfo.AssetID });
                }
            }
        }

        private void DisposeAssets(List<int> assetIds)
        {
            try
            {
                var disposedStatusId = _context.AssetStatuses.FirstOrDefault(s => s.StatusName == "Списан")?.StatusID;
                if (disposedStatusId == null) { ShowNotification("Статус 'Списан' не найден в базе данных.", true); return; }

                var assetsToUpdate = _context.Assets.Where(a => assetIds.Contains(a.AssetID)).ToList();
                foreach (var asset in assetsToUpdate)
                {
                    asset.StatusID = disposedStatusId.Value;
                    asset.IsActive = false;
                    asset.ModifiedDate = DateTime.Now;
                    asset.ModifiedByUserID = _currentUser.UserID;
                }
                _context.SaveChanges();
                ShowNotification($"{assetsToUpdate.Count} актив(ов) успешно списаны.");
                RefreshData();
            }
            catch (Exception ex) { ShowNotification($"Ошибка списания: {ex.Message}", true); }
        }

        private void DeleteAssets(List<int> assetIds)
        {
            try
            {
                var assetsToDelete = _context.Assets.Where(a => assetIds.Contains(a.AssetID)).ToList();
                foreach (var asset in assetsToDelete)
                {
                    if (_context.AssetMovements.Any(m => m.AssetID == asset.AssetID))
                    {
                        MessageBox.Show($"Невозможно удалить '{asset.AssetName}', т.к. с ним связана история перемещений. Сначала удалите связанные записи или просто спишите актив.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                        continue;
                    }
                    _context.Assets.Remove(asset);
                }
                _context.SaveChanges();
                ShowNotification("Выбранные активы навсегда удалены.");
                RefreshData();
            }
            catch (Exception ex) { ShowNotification($"Ошибка удаления: {ex.Message}", true); }
        }

        private void DgAssets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnEdit.IsEnabled = dgAssets.SelectedItems.Count == 1;
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            var selectedCount = _assets.Count(a => a.IsSelected);
            txtSelectionInfo.Text = selectedCount > 0 ? $"Выбрано активов: {selectedCount}" : "";

            // Показываем/скрываем панель информации о выборе
            selectionInfoPanel.Visibility = selectedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            btnClearSelection.Visibility = selectedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshData();
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) { _currentPage = 1; RefreshData(); }
        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) { _currentPage = 1; RefreshData(); } }

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                var item = cmbPageSize.SelectedItem as ComboBoxItem;
                if (item != null)
                {
                    if (int.TryParse(item.Content.ToString(), out int size))
                    {
                        _pageSize = size;
                        _currentPage = 1;
                        RefreshData();
                    }
                }
            }
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked ?? false;
            foreach (var asset in _assets) asset.IsSelected = isChecked;
            UpdateSelectionInfo();
        }

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var asset in _assets) asset.IsSelected = false;
            if (chkSelectAll != null) chkSelectAll.IsChecked = false;
            UpdateSelectionInfo();
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) { if (_currentPage > 1) { _currentPage--; RefreshData(); } }
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) { if (_currentPage < _totalPages) { _currentPage++; RefreshData(); } }

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var vm = btn?.CommandParameter as AssetViewModel;
            if (vm != null)
            {
                var win = new Windows.AssetEditorWindow(_context, _currentUser, vm.FullInfo.AssetID);
                win.Owner = Window.GetWindow(this);
                if (win.ShowDialog() == true) RefreshData();
            }
        }
    }

    public class AssetViewModel : INotifyPropertyChanged
    {
        public vw_AssetsFullInfo FullInfo { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public AssetViewModel(vw_AssetsFullInfo fullInfo)
        {
            FullInfo = fullInfo;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;

namespace AssetManagment
{
    public partial class AssetsWindow : Window
    {
        private AssetControlDBEntities _context;
        private Users _currentUser; // Изменено с User на Users

        public AssetsWindow(AssetControlDBEntities context, Users currentUser)
        {
            InitializeComponent();
            _context = context;
            _currentUser = currentUser;
            LoadAssets();
            UpdateLastRefreshTime();
        }

        private void LoadAssets(string searchText = "")
        {
            var query = _context.vw_AssetsFullInfo
                .Where(a => a.IsActive == true);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(a =>
                    a.AssetCode.Contains(searchText) ||
                    a.AssetName.Contains(searchText) ||
                    a.CategoryName.Contains(searchText) ||
                    a.LocationName.Contains(searchText));
            }

            dgAssets.ItemsSource = query.ToList();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Временно показываем сообщение, пока не создано окно редактирования
            MessageBox.Show("Функция добавления актива будет реализована позже", "В разработке",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgAssets.SelectedItem as vw_AssetsFullInfo;
            if (selected != null)
            {
                MessageBox.Show($"Редактирование актива: {selected.AssetName}", "В разработке",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Выберите актив для редактирования", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgAssets.SelectedItem as vw_AssetsFullInfo;
            if (selected != null)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить актив '{selected.AssetName}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var asset = _context.Assets.Find(selected.AssetID);
                        asset.IsActive = false;
                        asset.ModifiedDate = DateTime.Now;
                        asset.ModifiedByUserID = _currentUser.UserID;

                        _context.SaveChanges();
                        LoadAssets();

                        MessageBox.Show("Актив успешно удален", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите актив для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadAssets(txtSearch.Text);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAssets();
            UpdateLastRefreshTime();
        }

        private void UpdateLastRefreshTime()
        {
            if (FindName("txtLastUpdate") is TextBlock lastUpdate)
            {
                lastUpdate.Text = DateTime.Now.ToString("HH:mm:ss");
            }
        }
    }
}
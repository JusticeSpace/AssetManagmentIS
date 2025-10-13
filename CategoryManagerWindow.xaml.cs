using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AssetManagment.Windows
{
    public partial class CategoryManagerWindow : Window
    {
        private readonly AssetControlDBEntities _context;
        private readonly Users _currentUser;
        private Categories _editingCategory;
        private string _searchText = string.Empty;

        public CategoryManagerWindow(AssetControlDBEntities context, Users currentUser)
        {
            InitializeComponent();
            _context = context ?? new AssetControlDBEntities();
            _currentUser = currentUser ?? App.CurrentUser;

            if (_currentUser == null || (_currentUser.RoleID != 1 && _currentUser.RoleID != 2))
            {
                MessageBox.Show("Доступ к управлению категориями разрешён только Администратору и Менеджеру.",
                    "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.BeginInvoke(new Action(Close));
                return;
            }

            Loaded += (s, e) => LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                var list = _context.Categories.OrderBy(c => c.CategoryName).ToList();

                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    var term = _searchText.Trim().ToLower();
                    list = list.Where(c => (c.CategoryName ?? "").ToLower().Contains(term)).ToList();
                }

                dgCategories.ItemsSource = list;
                txtCategoryCount.Text = $"Всего: {list.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки категорий:\n\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSearchCategories_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = txtSearchCategories.Text ?? string.Empty;
            LoadCategories();
        }

        private void BtnSaveCategory_Click(object sender, RoutedEventArgs e)
        {
            var name = (txtCategoryName.Text ?? "").Trim();
            var descr = (txtDescription.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название категории.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCategoryName.Focus();
                return;
            }

            try
            {
                // ВАЖНО: избавляемся от _editingCategory внутри LINQ
                int? editingId = _editingCategory?.CategoryID;
                string nameLower = name.ToLower();

                bool duplicate = _context.Categories
                    .Any(c => c.CategoryName.ToLower() == nameLower &&
                              (!editingId.HasValue || c.CategoryID != editingId.Value));

                if (duplicate)
                {
                    MessageBox.Show("Категория с таким названием уже существует.", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!editingId.HasValue)
                {
                    var entity = new Categories
                    {
                        CategoryName = name,
                        Description = string.IsNullOrWhiteSpace(descr) ? null : descr
                    };
                    _context.Categories.Add(entity);
                    _context.SaveChanges();
                    MessageBox.Show("Категория добавлена.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // правим отслеживаемую сущность
                    var entity = _context.Categories.Find(editingId.Value);
                    if (entity == null)
                    {
                        MessageBox.Show("Категория не найдена (возможно удалена другим пользователем).", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearForm();
                        LoadCategories();
                        return;
                    }

                    entity.CategoryName = name;
                    entity.Description = string.IsNullOrWhiteSpace(descr) ? null : descr;
                    _context.SaveChanges();

                    MessageBox.Show("Категория обновлена.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ClearForm();
                LoadCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения:\n\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.CommandParameter is Categories model)
            {
                // сохраняем только ID и подгружаем свежую отслеживаемую сущность при сохранении
                _editingCategory = new Categories { CategoryID = model.CategoryID };

                txtCategoryName.Text = model.CategoryName;
                txtDescription.Text = model.Description;

                txtFormTitle.Text = "Редактирование";
                txtFormSubtitle.Text = $"ID: {model.CategoryID}";
                FormIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Pencil;
                FormIconBorder.Background = (Brush)new BrushConverter().ConvertFrom("#FEF3C7");
                FormIcon.Foreground = (Brush)new BrushConverter().ConvertFrom("#F59E0B");
                btnSaveCategory.Content = "Сохранить";
            }
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.RoleID != 1)
            {
                MessageBox.Show("Удаление категорий доступно только Администратору.",
                    "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button b && b.CommandParameter is Categories model)
            {
                var entity = _context.Categories.Find(model.CategoryID);
                if (entity == null)
                {
                    MessageBox.Show("Категория не найдена (возможно уже удалена).", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadCategories();
                    return;
                }

                var linked = _context.Assets.Any(a => a.CategoryID == entity.CategoryID);
                if (linked)
                {
                    MessageBox.Show("Нельзя удалить категорию — к ней привязаны активы. Переназначьте активы.", "Остановка",
                        MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                var confirm = MessageBox.Show($"Удалить категорию '{entity.CategoryName}' навсегда?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

                try
                {
                    _context.Categories.Remove(entity);
                    _context.SaveChanges();

                    if (_editingCategory?.CategoryID == entity.CategoryID)
                        ClearForm();

                    LoadCategories();
                    MessageBox.Show("Категория удалена.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления:\n\n{ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _editingCategory = null;
            txtCategoryName.Text = "";
            txtDescription.Text = "";

            txtFormTitle.Text = "Новая категория";
            txtFormSubtitle.Text = "Заполните данные";
            FormIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Plus;
            FormIconBorder.Background = (Brush)new BrushConverter().ConvertFrom("#EEF2FF");
            FormIcon.Foreground = (Brush)new BrushConverter().ConvertFrom("#6366F1");
            btnSaveCategory.Content = "Сохранить категорию";

            dgCategories.SelectedItem = null;
        }

        private void DgCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // сюда ничего — оставим только редактирование по кнопке
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
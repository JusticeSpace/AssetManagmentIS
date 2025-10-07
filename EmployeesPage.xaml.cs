using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AssetManagment.Pages
{
    public partial class EmployeesPage : Page
    {
        private readonly AssetControlDBEntities _context;
        private List<EmployeeViewModel> _allEmployees;

        public EmployeesPage()
        {
            InitializeComponent();
            _context = new AssetControlDBEntities();
            Loaded += EmployeesPage_Loaded;
        }

        private void EmployeesPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFilters();
            RefreshData();
            ApplyRolePermissions();
        }

        private void LoadFilters()
        {
            try
            {
                var departments = new List<object> { new { DepartmentID = (int?)null, DepartmentName = "Все отделы" } };
                departments.AddRange(_context.Departments.Select(d => new { DepartmentID = (int?)d.DepartmentID, d.DepartmentName }).OrderBy(d => d.DepartmentName).ToList());
                cmbDepartmentFilter.ItemsSource = departments;
                cmbDepartmentFilter.DisplayMemberPath = "DepartmentName";
                cmbDepartmentFilter.SelectedValuePath = "DepartmentID";

                var positions = new List<object> { new { PositionID = (int?)null, PositionName = "Все должности" } };
                positions.AddRange(_context.Positions.Select(p => new { PositionID = (int?)p.PositionID, p.PositionName }).OrderBy(p => p.PositionName).ToList());
                cmbPositionFilter.ItemsSource = positions;
                cmbPositionFilter.DisplayMemberPath = "PositionName";
                cmbPositionFilter.SelectedValuePath = "PositionID";

                cmbStatusFilter.ItemsSource = new List<string> { "Все", "Активные", "Неактивные" };
                cmbStatusFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фильтров: {ex.Message}");
            }
        }

        public void RefreshData()
        {
            try
            {
                // --- ИСПРАВЛЕНИЕ: Убираем медленный подзапрос ---
                // Теперь все данные, включая HireDate, берутся из одного представления vw_UsersInfo
                _allEmployees = _context.vw_UsersInfo
                    .Join(_context.Employees, // Присоединяем фото
                          userInfo => userInfo.EmployeeID,
                          employee => employee.EmployeeID,
                          (userInfo, employee) => new { UserInfo = userInfo, employee.Photo })
                    .Select(e => new EmployeeViewModel
                    {
                        EmployeeID = e.UserInfo.EmployeeID,
                        FullName = e.UserInfo.FullName,
                        Email = e.UserInfo.Email,
                        Phone = e.UserInfo.Phone,
                        PositionName = e.UserInfo.PositionName,
                        DepartmentName = e.UserInfo.DepartmentName,
                        HireDate = e.UserInfo.HireDate ?? DateTime.MinValue,
                        IsActive = e.UserInfo.UserIsActive ?? false,
                        Photo = e.Photo
                    }).ToList();

                ApplyFilters();
                LoadStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void LoadStatistics()
        {
            if (_allEmployees == null) return;

            var oneMonthAgo = DateTime.Now.AddMonths(-1);
            txtTotalEmployees.Text = _allEmployees.Count.ToString();
            txtActiveEmployees.Text = _allEmployees.Count(e => e.IsActive).ToString();
            txtDepartmentCount.Text = _context.Departments.Count().ToString();
            txtNewEmployees.Text = _allEmployees.Count(e => e.HireDate >= oneMonthAgo).ToString();
        }

        private void ApplyRolePermissions()
        {
            if (App.CurrentUser == null) return;
            bool isManagerOrAdmin = App.CurrentUser.RoleID == 1 || App.CurrentUser.RoleID == 2;
            bool isAdmin = App.CurrentUser.RoleID == 1;

            btnAddEmployee.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;

            dgEmployees.LoadingRow += (s, args) =>
            {
                var editButton = FindVisualChild<Button>(args.Row, "BtnEdit");
                if (editButton != null) editButton.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;

                var toggleButton = FindVisualChild<Button>(args.Row, "BtnToggleStatus");
                if (toggleButton != null) toggleButton.Visibility = isManagerOrAdmin ? Visibility.Visible : Visibility.Collapsed;

                var deleteButton = FindVisualChild<Button>(args.Row, "BtnDeleteEmployee");
                if (deleteButton != null) deleteButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            };

            var actionsColumn = dgEmployees.Columns.LastOrDefault();
            if (actionsColumn != null && !isManagerOrAdmin)
            {
                actionsColumn.Visibility = Visibility.Collapsed;
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

        private void ApplyFilters()
        {
            if (_allEmployees == null) return;

            IEnumerable<EmployeeViewModel> filteredList = _allEmployees;

            var searchText = txtSearch.Text.ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredList = filteredList.Where(e =>
                    (e.FullName != null && e.FullName.ToLower().Contains(searchText)) ||
                    (e.Email != null && e.Email.ToLower().Contains(searchText)) ||
                    (e.Phone != null && e.Phone.Contains(searchText))
                );
            }

            if (cmbDepartmentFilter.SelectedValue != null)
            {
                var departmentId = (int)cmbDepartmentFilter.SelectedValue;
                var departmentName = _context.Departments.Find(departmentId)?.DepartmentName;
                if (departmentName != null)
                    filteredList = filteredList.Where(e => e.DepartmentName == departmentName);
            }

            if (cmbPositionFilter.SelectedValue != null)
            {
                var positionId = (int)cmbPositionFilter.SelectedValue;
                var positionName = _context.Positions.Find(positionId)?.PositionName;
                if (positionName != null)
                    filteredList = filteredList.Where(e => e.PositionName == positionName);
            }

            if (cmbStatusFilter.SelectedIndex == 1) // Активные
            {
                filteredList = filteredList.Where(e => e.IsActive);
            }
            else if (cmbStatusFilter.SelectedIndex == 2) // Неактивные
            {
                filteredList = filteredList.Where(e => !e.IsActive);
            }

            dgEmployees.ItemsSource = filteredList.ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private void DgEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
        {
            var win = new Windows.EmployeeEditorWindow(_context, App.CurrentUser);
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true) RefreshData();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var vm = button?.CommandParameter as EmployeeViewModel;
            if (vm != null)
            {
                var win = new Windows.EmployeeEditorWindow(_context, App.CurrentUser, vm.EmployeeID);
                win.Owner = Window.GetWindow(this);
                if (win.ShowDialog() == true) RefreshData();
            }
        }

        private void BtnToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var employee = button?.CommandParameter as EmployeeViewModel;
            if (employee == null) return;

            var result = MessageBox.Show($"Изменить статус сотрудника {employee.FullName}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var userToUpdate = _context.Users.FirstOrDefault(u => u.EmployeeID == employee.EmployeeID);
            if (userToUpdate != null)
            {
                userToUpdate.IsActive = !userToUpdate.IsActive;
                _context.SaveChanges();
                RefreshData();
                MessageBox.Show($"Статус сотрудника {employee.FullName} изменен.", "Успех");
            }
            else
            {
                MessageBox.Show($"Не найдена учетная запись для сотрудника {employee.FullName}.", "Ошибка");
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser?.RoleID != 1)
            {
                MessageBox.Show("Эта операция доступна только Администратору.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var vm = button?.CommandParameter as EmployeeViewModel;
            if (vm == null) return;

            var result = MessageBox.Show($"Вы уверены, что хотите НАВСЕГДА удалить сотрудника '{vm.FullName}' и его учетную запись?\n\nЭто действие НЕОБРАТИМО!", "КРИТИЧЕСКОЕ ДЕЙСТВИЕ", MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (_context.Assets.Any(a => a.ResponsibleEmployeeID == vm.EmployeeID))
                {
                    MessageBox.Show("Невозможно удалить сотрудника, так как за ним закреплены активы. Сначала переназначьте их.", "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                var userToDelete = _context.Users.FirstOrDefault(u => u.EmployeeID == vm.EmployeeID);
                if (userToDelete != null)
                {
                    _context.Users.Remove(userToDelete);
                }

                var employeeToDelete = _context.Employees.Find(vm.EmployeeID);
                if (employeeToDelete != null)
                {
                    _context.Employees.Remove(employeeToDelete);
                }

                _context.SaveChanges();
                MessageBox.Show("Сотрудник и его учетная запись были навсегда удалены.", "Успех");
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка");
            }
        }
    }

    public class EmployeeViewModel
    {
        public int EmployeeID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PositionName { get; set; }
        public string DepartmentName { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }
        public byte[] Photo { get; set; }
        public string StatusText => IsActive ? "Активен" : "Неактивен";
        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName)) return "??";
                var parts = FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1) return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
                if (parts.Length == 1 && parts[0].Length > 1) return parts[0].Substring(0, 2).ToUpper();
                return FullName.FirstOrDefault().ToString().ToUpper();
            }
        }
        public BitmapImage ProfileImage
        {
            get
            {
                if (Photo == null || Photo.Length == 0) return null;
                try
                {
                    using (var stream = new MemoryStream(Photo))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    }
                }
                catch { return null; }
            }
        }
    }
}
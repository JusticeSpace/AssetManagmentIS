using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            LoadData();
            LoadStatistics();
            ApplyRolePermissions();
        }

        private void LoadData()
        {
            try
            {
                var employees = _context.Employees
                    .Where(e => e.IsActive == true)
                    .ToList();

                _allEmployees = new List<EmployeeViewModel>();

                foreach (var emp in employees)
                {
                    var position = _context.Positions.FirstOrDefault(p => p.PositionID == emp.PositionID);
                    var department = _context.Departments.FirstOrDefault(d => d.DepartmentID == emp.DepartmentID);
                    var user = _context.Users.FirstOrDefault(u => u.EmployeeID == emp.EmployeeID);

                    string initials = "";
                    if (!string.IsNullOrEmpty(emp.FirstName) && !string.IsNullOrEmpty(emp.LastName))
                    {
                        initials = emp.FirstName.Substring(0, 1) + emp.LastName.Substring(0, 1);
                    }

                    _allEmployees.Add(new EmployeeViewModel
                    {
                        EmployeeID = emp.EmployeeID,
                        FullName = $"{emp.LastName} {emp.FirstName} {emp.MiddleName}",
                        Email = emp.Email,
                        Phone = emp.Phone,
                        PositionName = position?.PositionName ?? "Не указана",
                        DepartmentName = department?.DepartmentName ?? "Не указан",
                        HireDate = emp.HireDate ?? DateTime.Now,
                        IsActive = user?.IsActive ?? true,
                        Initials = initials
                    });
                }

                dgEmployees.ItemsSource = _allEmployees;

                // Загрузка фильтров
                var departments = _context.Departments.Select(d => d.DepartmentName).ToList();
                departments.Insert(0, "Все отделы");
                cmbDepartmentFilter.ItemsSource = departments;
                cmbDepartmentFilter.SelectedIndex = 0;

                var positions = _context.Positions.Select(p => p.PositionName).ToList();
                positions.Insert(0, "Все должности");
                cmbPositionFilter.ItemsSource = positions;
                cmbPositionFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStatistics()
        {
            txtTotalEmployees.Text = _allEmployees.Count.ToString();
            txtActiveEmployees.Text = _allEmployees.Count(e => e.IsActive).ToString();
            txtDepartmentCount.Text = _context.Departments.Count().ToString();

            var newCount = _allEmployees.Count(e => e.HireDate >= DateTime.Now.AddMonths(-1));
            txtNewEmployees.Text = newCount.ToString();
        }

        private void ApplyRolePermissions()
        {
            if (App.CurrentUser == null) return;

            // Только Менеджер и Администратор могут управлять сотрудниками
            if (App.CurrentUser.RoleID == 3) // Пользователь
            {
                btnAddEmployee.Visibility = Visibility.Collapsed;
                btnExport.Visibility = Visibility.Collapsed;

                // Скрываем колонку с действиями
                if (dgEmployees.Columns.Count > 0)
                {
                    dgEmployees.Columns[dgEmployees.Columns.Count - 1].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ApplyFilters()
        {
            if (_allEmployees == null) return;

            var filteredList = _allEmployees;

            // Поиск
            var searchText = txtSearch.Text.ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredList = filteredList.Where(e =>
                    e.FullName.ToLower().Contains(searchText) ||
                    (e.Email != null && e.Email.ToLower().Contains(searchText)) ||
                    (e.Phone != null && e.Phone.ToLower().Contains(searchText))
                ).ToList();
            }

            // Фильтр по отделу
            if (cmbDepartmentFilter.SelectedIndex > 0)
            {
                var selectedDepartment = cmbDepartmentFilter.SelectedItem.ToString();
                filteredList = filteredList.Where(e => e.DepartmentName == selectedDepartment).ToList();
            }

            // Фильтр по должности
            if (cmbPositionFilter.SelectedIndex > 0)
            {
                var selectedPosition = cmbPositionFilter.SelectedItem.ToString();
                filteredList = filteredList.Where(e => e.PositionName == selectedPosition).ToList();
            }

            // Фильтр по статусу
            if (cmbStatusFilter.SelectedIndex == 1) // Активные
            {
                filteredList = filteredList.Where(e => e.IsActive).ToList();
            }
            else if (cmbStatusFilter.SelectedIndex == 2) // Неактивные
            {
                filteredList = filteredList.Where(e => !e.IsActive).ToList();
            }

            dgEmployees.ItemsSource = filteredList;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // EmployeesPage.xaml.cs

        private void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null || (App.CurrentUser.RoleID != 1 && App.CurrentUser.RoleID != 2))
            {
                MessageBox.Show("Недостаточно прав. Доступ только для администратора и менеджера.");
                return;
            }

            var win = new Windows.EmployeeEditorWindow(_context, App.CurrentUser);
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                LoadData();
                LoadStatistics();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null || (App.CurrentUser.RoleID != 1 && App.CurrentUser.RoleID != 2))
            {
                MessageBox.Show("Недостаточно прав. Доступ только для администратора и менеджера.");
                return;
            }

            var btn = sender as Button;
            if (btn == null) return;
            var vm = btn.CommandParameter as EmployeeViewModel;
            if (vm == null) return;

            var win = new Windows.EmployeeEditorWindow(_context, App.CurrentUser, vm.EmployeeID);
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                LoadData();
                LoadStatistics();
            }
        }

        private void BtnToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var employee = button.CommandParameter as EmployeeViewModel;
            if (employee == null) return;

            var result = MessageBox.Show(
                $"Изменить статус сотрудника {employee.FullName}?\n\nТекущий статус: {employee.StatusText}",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var userToUpdate = _context.Users.FirstOrDefault(u => u.EmployeeID == employee.EmployeeID);
                if (userToUpdate != null)
                {
                    userToUpdate.IsActive = !userToUpdate.IsActive;
                    _context.SaveChanges();
                    LoadData();
                    LoadStatistics();
                    MessageBox.Show($"✅ Статус сотрудника {employee.FullName} изменен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = $"Employees_Report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("ФИО;Email;Телефон;Должность;Отдел;Дата найма;Статус");

                    foreach (var emp in _allEmployees)
                    {
                        csv.AppendLine($"{emp.FullName};{emp.Email};{emp.Phone};{emp.PositionName};{emp.DepartmentName};{emp.HireDate:dd.MM.yyyy};{emp.StatusText}");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(), System.Text.Encoding.UTF8);
                    MessageBox.Show($"✅ Отчет экспортирован!\n\nФайл: {saveDialog.FileName}\nСотрудников: {_allEmployees.Count}",
                        "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public string StatusText
        {
            get { return IsActive ? "Активен" : "Уволен"; }
        }
        public string Initials { get; set; }
    }
}
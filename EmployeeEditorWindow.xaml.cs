using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace AssetManagment.Windows
{
    public partial class EmployeeEditorWindow : Window
    {
        private readonly AssetControlDBEntities _context;
        private readonly Users _currentUser;
        private Employees _employee;
        private Users _userAccount;
        private bool _isDirty;

        public EmployeeEditorWindow(AssetControlDBEntities context, Users currentUser, int? employeeId = null)
        {
            InitializeComponent();
            _context = context;
            _currentUser = currentUser;

            if (_currentUser == null || (_currentUser.RoleID != 1 && _currentUser.RoleID != 2))
            {
                MessageBox.Show("Недостаточно прав. Доступ только для администратора и менеджера.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            LoadLookups();
            AttachDirtyHandlers();

            if (employeeId.HasValue)
            {
                _employee = _context.Employees.FirstOrDefault(e => e.EmployeeID == employeeId.Value);
                _userAccount = _context.Users.FirstOrDefault(u => u.EmployeeID == employeeId.Value);
                if (_employee != null)
                {
                    SetModeEdit();
                    FillFields();
                }
            }
            else
            {
                SetModeAdd();
            }

            this.Closing += OnWindowClosing;
        }

        private void AttachDirtyHandlers()
        {
            var textControls = new[] { txtLastName, txtFirstName, txtMiddleName, txtEmail, txtPhone, txtUsername };
            foreach (var tb in textControls)
                tb.TextChanged += (_, __) => { _isDirty = true; ValidateForm(); };

            var comboControls = new[] { cmbDepartment, cmbPosition, cmbRole };
            foreach (var cmb in comboControls)
                cmb.SelectionChanged += (_, __) => { _isDirty = true; ValidateForm(); };

            dpHireDate.SelectedDateChanged += (_, __) => _isDirty = true;
            chkCreateAccount.Checked += (_, __) => { _isDirty = true; ValidateForm(); };
            chkCreateAccount.Unchecked += (_, __) => { _isDirty = true; ValidateForm(); };
            chkUserIsActive.Checked += (_, __) => _isDirty = true;
            chkUserIsActive.Unchecked += (_, __) => _isDirty = true;
            txtPassword.PasswordChanged += (_, __) => _isDirty = true;
        }

        private void SetModeAdd()
        {
            TitleText.Text = "Новый сотрудник";
            ModeChipText.Text = "добавление";
            SubtitleText.Text = "Заполните ФИО, отдел и должность";
            MetaPanel.Visibility = Visibility.Collapsed;
            chkUserIsActive.IsChecked = true;
            dpHireDate.SelectedDate = DateTime.Now.Date;
            AccountPanel.Visibility = chkCreateAccount.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ValidateForm();
        }

        private void SetModeEdit()
        {
            TitleText.Text = "Редактирование сотрудника";
            ModeChipText.Text = "редактирование";
            SubtitleText.Text = "Измените данные и сохраните";
            MetaPanel.Visibility = Visibility.Visible;
        }

        private void LoadLookups()
        {
            cmbPosition.ItemsSource = _context.Positions.OrderBy(p => p.PositionName).ToList();
            cmbPosition.DisplayMemberPath = "PositionName";
            cmbPosition.SelectedValuePath = "PositionID";

            cmbDepartment.ItemsSource = _context.Departments.OrderBy(d => d.DepartmentName).ToList();
            cmbDepartment.DisplayMemberPath = "DepartmentName";
            cmbDepartment.SelectedValuePath = "DepartmentID";

            cmbRole.ItemsSource = _context.UserRoles.OrderBy(r => r.RoleName).ToList();
            cmbRole.DisplayMemberPath = "RoleName";
            cmbRole.SelectedValuePath = "RoleID";
        }

        private void FillFields()
        {
            txtLastName.Text = _employee.LastName;
            txtFirstName.Text = _employee.FirstName;
            txtMiddleName.Text = _employee.MiddleName;
            txtEmail.Text = _employee.Email;
            txtPhone.Text = _employee.Phone;
            dpHireDate.SelectedDate = _employee.HireDate ?? DateTime.Now.Date;

            if (_employee.PositionID != 0) cmbPosition.SelectedValue = _employee.PositionID;
            if (_employee.DepartmentID != 0) cmbDepartment.SelectedValue = _employee.DepartmentID;

            if (_userAccount != null)
            {
                chkCreateAccount.IsChecked = true;
                txtUsername.Text = _userAccount.Username;
                chkUserIsActive.IsChecked = _userAccount.IsActive ?? true;
                if (_userAccount.RoleID != 0) cmbRole.SelectedValue = _userAccount.RoleID;
                AccountPanel.Visibility = Visibility.Visible;
                var createdDate = _userAccount.CreatedDate?.ToString("dd.MM.yyyy HH:mm") ?? "неизвестно";
                MetaText.Text = $"Учетная запись создана: {createdDate}";
            }
            else
            {
                chkCreateAccount.IsChecked = false;
                AccountPanel.Visibility = Visibility.Collapsed;
                MetaPanel.Visibility = Visibility.Collapsed;
            }

            _isDirty = false;
            ValidateForm();
        }

        private void AccountToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            AccountPanel.Visibility = chkCreateAccount.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            _isDirty = true;
            ValidateForm();
        }

        private void ShowError(string text)
        {
            btnSave.IsEnabled = string.IsNullOrWhiteSpace(text);
            ErrorText.Text = string.IsNullOrWhiteSpace(text) ? "" : $"• {text}";
            ErrorText.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool ValidateForm()
        {
            ShowError(null);
            if (string.IsNullOrWhiteSpace(txtLastName.Text) || string.IsNullOrWhiteSpace(txtFirstName.Text))
            { ShowError("Введите фамилию и имя"); return false; }
            if (cmbDepartment.SelectedValue == null || cmbPosition.SelectedValue == null)
            { ShowError("Выберите отдел и должность"); return false; }
            if (chkCreateAccount.IsChecked == true && (string.IsNullOrWhiteSpace(txtUsername.Text) || cmbRole.SelectedValue == null))
            { ShowError("Укажите логин и роль для учетной записи"); return false; }
            return true;
        }

        // --- ИСПРАВЛЕННЫЙ МЕТОД СОХРАНЕНИЯ ---
        private void DoSave()
        {
            if (!ValidateForm()) return;

            try
            {
                // === СОЗДАНИЕ ИЛИ ОБНОВЛЕНИЕ СОТРУДНИКА ===
                bool isNewEmployee = _employee == null || _employee.EmployeeID == 0;

                if (isNewEmployee)
                {
                    _employee = new Employees();
                }

                // Заполняем поля
                _employee.LastName = txtLastName.Text.Trim();
                _employee.FirstName = txtFirstName.Text.Trim();
                _employee.MiddleName = string.IsNullOrWhiteSpace(txtMiddleName.Text) ? null : txtMiddleName.Text.Trim();
                _employee.Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();
                _employee.Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim();
                _employee.PositionID = (int)cmbPosition.SelectedValue;
                _employee.DepartmentID = (int)cmbDepartment.SelectedValue;
                _employee.HireDate = dpHireDate.SelectedDate ?? DateTime.Now.Date;
                _employee.IsActive = true;

                // Добавляем в контекст только если новый
                if (isNewEmployee)
                {
                    _context.Employees.Add(_employee);
                }

                // === ОБРАБОТКА УЧЕТНОЙ ЗАПИСИ ===
                if (chkCreateAccount.IsChecked == true)
                {
                    var username = txtUsername.Text.Trim();

                    // ⚠️ КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Вынести UserID в локальную переменную
                    int? currentUserId = _userAccount?.UserID;

                    // Проверка уникальности логина
                    var existingUser = _context.Users
                        .FirstOrDefault(u => u.Username == username &&
                                       (!currentUserId.HasValue || u.UserID != currentUserId.Value));

                    if (existingUser != null)
                    {
                        ShowError("Пользователь с таким логином уже существует");
                        return;
                    }

                    bool isNewUser = _userAccount == null;

                    if (isNewUser)
                    {
                        // Для новой учетной записи пароль обязателен
                        if (string.IsNullOrWhiteSpace(txtPassword.Password))
                        {
                            ShowError("Для новой учетной записи укажите пароль");
                            return;
                        }

                        // СНАЧАЛА сохраняем сотрудника, чтобы получить EmployeeID
                        _context.SaveChanges();

                        // ТЕПЕРЬ создаем учетную запись с корректным EmployeeID
                        _userAccount = new Users
                        {
                            Username = username,
                            PasswordHash = GetMD5(txtPassword.Password),
                            EmployeeID = _employee.EmployeeID,
                            RoleID = (int)cmbRole.SelectedValue,
                            IsActive = chkUserIsActive.IsChecked ?? true,
                            CreatedDate = DateTime.Now
                        };
                        _context.Users.Add(_userAccount);
                    }
                    else
                    {
                        // Обновляем существующую учетную запись
                        _userAccount.Username = username;

                        if (!string.IsNullOrWhiteSpace(txtPassword.Password))
                        {
                            _userAccount.PasswordHash = GetMD5(txtPassword.Password);
                        }

                        _userAccount.RoleID = (int)cmbRole.SelectedValue;
                        _userAccount.IsActive = chkUserIsActive.IsChecked ?? true;
                    }
                }
                else
                {
                    // Если галочка снята - деактивируем учетную запись
                    if (_userAccount != null)
                    {
                        _userAccount.IsActive = false;
                    }
                }

                // Финальное сохранение всех изменений
                _context.SaveChanges();

                _isDirty = false;
                MessageBox.Show("Сотрудник успешно сохранен!", "Успех",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errors = new System.Text.StringBuilder();
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        errors.AppendLine($"• {validationError.PropertyName}: {validationError.ErrorMessage}");
                    }
                }

                MessageBox.Show($"Ошибка валидации данных:\n\n{errors}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Ошибка сохранения в базу данных:\n\n{innerMessage}",
                               "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                var innerException = ex;
                while (innerException.InnerException != null)
                {
                    innerException = innerException.InnerException;
                }

                MessageBox.Show($"Непредвиденная ошибка:\n\n{innerException.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => DoSave();
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => DoSave();
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e) => Close();

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isDirty)
            {
                var r = MessageBox.Show("Есть несохранённые изменения. Закрыть без сохранения?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r == MessageBoxResult.No) e.Cancel = true;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
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
        private bool _dirty;

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

            this.Closing += Window_Closing;
        }

        private void AttachDirtyHandlers()
        {
            foreach (var tb in new[] { txtLastName, txtFirstName, txtMiddleName, txtEmail, txtPhone, txtUsername })
                tb.TextChanged += (_, __) => { _dirty = true; ValidateForm(); };
            dpHireDate.SelectedDateChanged += (_, __) => { _dirty = true; };
            cmbDepartment.SelectionChanged += (_, __) => { _dirty = true; ValidateForm(); };
            cmbPosition.SelectionChanged += (_, __) => { _dirty = true; ValidateForm(); };
            cmbRole.SelectionChanged += (_, __) => { _dirty = true; };
            chkCreateAccount.Checked += (_, __) => { _dirty = true; ValidateForm(); };
            chkCreateAccount.Unchecked += (_, __) => { _dirty = true; ValidateForm(); };
            chkEmpIsActive.Checked += (_, __) => { _dirty = true; };
            chkEmpIsActive.Unchecked += (_, __) => { _dirty = true; };
            chkUserIsActive.Checked += (_, __) => { _dirty = true; };
            chkUserIsActive.Unchecked += (_, __) => { _dirty = true; };
            txtPassword.PasswordChanged += (_, __) => { _dirty = true; };
        }

        private void SetModeAdd()
        {
            TitleText.Text = "Новый сотрудник";
            ModeChipText.Text = "добавление";
            SubtitleText.Text = "Заполните ФИО, отдел и должность";
            chkEmpIsActive.IsChecked = true;
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
            AccountPanel.Visibility = (_userAccount != null && chkCreateAccount.IsChecked == true)
                ? Visibility.Visible : Visibility.Collapsed;
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
            chkEmpIsActive.IsChecked = _employee.IsActive ?? true;

            if (_employee.PositionID != 0) cmbPosition.SelectedValue = _employee.PositionID;
            if (_employee.DepartmentID != 0) cmbDepartment.SelectedValue = _employee.DepartmentID;

            if (_userAccount != null)
            {
                chkCreateAccount.IsChecked = true;
                txtUsername.Text = _userAccount.Username;
                chkUserIsActive.IsChecked = _userAccount.IsActive ?? true;
                if (_userAccount.RoleID != 0) cmbRole.SelectedValue = _userAccount.RoleID;
                AccountPanel.Visibility = Visibility.Visible;
            }
            else
            {
                chkCreateAccount.IsChecked = false;
                AccountPanel.Visibility = Visibility.Collapsed;
            }

            _dirty = false;
            ValidateForm();
        }

        private void AccountToggle_Changed(object sender, RoutedEventArgs e)
        {
            AccountPanel.Visibility = (chkCreateAccount.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            _dirty = true;
            ValidateForm();
        }

        private void ValidateForm()
        {
            btnSave.IsEnabled = true;
            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = "";

            if (string.IsNullOrWhiteSpace(txtLastName.Text) || string.IsNullOrWhiteSpace(txtFirstName.Text))
            {
                ShowError("Введите фамилию и имя");
                return;
            }
            if (cmbDepartment.SelectedValue == null || cmbPosition.SelectedValue == null)
            {
                ShowError("Выберите отдел и должность");
                return;
            }

            if (chkCreateAccount.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text) || cmbRole.SelectedValue == null)
                {
                    ShowError("Укажите логин и роль для учетной записи");
                    return;
                }
            }
        }

        private void ShowError(string text)
        {
            btnSave.IsEnabled = false;
            ErrorText.Text = "• " + text;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void DoSave()
        {
            try
            {
                ValidateForm();
                if (!btnSave.IsEnabled) return;

                if (_employee == null)
                {
                    _employee = new Employees
                    {
                        LastName = txtLastName.Text.Trim(),
                        FirstName = txtFirstName.Text.Trim(),
                        MiddleName = string.IsNullOrWhiteSpace(txtMiddleName.Text) ? null : txtMiddleName.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim(),
                        Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim(),
                        PositionID = (int)cmbPosition.SelectedValue,
                        DepartmentID = (int)cmbDepartment.SelectedValue,
                        HireDate = dpHireDate.SelectedDate ?? DateTime.Now.Date,
                        IsActive = chkEmpIsActive.IsChecked ?? true
                    };
                    _context.Employees.Add(_employee);
                    _context.SaveChanges();
                }
                else
                {
                    _employee.LastName = txtLastName.Text.Trim();
                    _employee.FirstName = txtFirstName.Text.Trim();
                    _employee.MiddleName = string.IsNullOrWhiteSpace(txtMiddleName.Text) ? null : txtMiddleName.Text.Trim();
                    _employee.Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();
                    _employee.Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim();
                    _employee.PositionID = (int)cmbPosition.SelectedValue;
                    _employee.DepartmentID = (int)cmbDepartment.SelectedValue;
                    _employee.HireDate = dpHireDate.SelectedDate ?? DateTime.Now.Date;
                    _employee.IsActive = chkEmpIsActive.IsChecked ?? true;
                }

                if (chkCreateAccount.IsChecked == true)
                {
                    if (_userAccount == null)
                    {
                        if (string.IsNullOrWhiteSpace(txtPassword.Password))
                        {
                            ShowError("Для новой учетной записи укажите пароль");
                            return;
                        }

                        _userAccount = new Users
                        {
                            Username = txtUsername.Text.Trim(),
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
                        _userAccount.Username = txtUsername.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(txtPassword.Password))
                            _userAccount.PasswordHash = GetMD5(txtPassword.Password);
                        _userAccount.RoleID = (int)cmbRole.SelectedValue;
                        _userAccount.IsActive = chkUserIsActive.IsChecked ?? true;
                    }
                }
                else
                {
                    if (_userAccount != null) _userAccount.IsActive = false;
                }

                _context.SaveChanges();
                _dirty = false;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private string GetMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
                var sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => DoSave();
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => DoSave();
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = btnSave.IsEnabled;
        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e) => Close();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_dirty)
            {
                var r = MessageBox.Show("Есть несохранённые изменения. Закрыть без сохранения?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.No) e.Cancel = true;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
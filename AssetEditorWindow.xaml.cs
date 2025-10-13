using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace AssetManagment.Windows
{
    public partial class AssetEditorWindow : Window
    {
        private readonly AssetControlDBEntities _context;
        private readonly Users _currentUser;
        private Assets _entity;
        private bool _isDirty;
        private int? _disposedStatusId; // ID статуса "Списан"

        public AssetEditorWindow(AssetControlDBEntities context, Users currentUser, int? assetId = null)
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

            if (assetId.HasValue)
            {
                _entity = _context.Assets.FirstOrDefault(a => a.AssetID == assetId.Value);
                if (_entity != null)
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
            var textControls = new[] { txtCode, txtName, txtModel, txtSerial, txtPrice, txtDescription };
            foreach (var tb in textControls)
                tb.TextChanged += (_, __) => { _isDirty = true; ValidateForm(); };

            var comboControls = new[] { cmbCategory, cmbStatus, cmbManufacturer, cmbLocation, cmbResponsible };
            foreach (var cmb in comboControls)
                cmb.SelectionChanged += (_, __) => { _isDirty = true; ValidateForm(); };

            dpPurchaseDate.SelectedDateChanged += (_, __) => _isDirty = true;

            // chkIsActive - только отображение, без хендлеров
        }

        private void SetModeAdd()
        {
            TitleText.Text = "Новый актив";
            ModeChipText.Text = "добавление";
            SubtitleText.Text = "Заполните обязательные поля со знаком •";
            MetaPanel.Visibility = Visibility.Collapsed;
            txtCode.IsReadOnly = false;
            btnSave.IsEnabled = false; // включится после валидации
            ValidateForm();
        }

        private void SetModeEdit()
        {
            TitleText.Text = "Редактирование актива";
            ModeChipText.Text = "редактирование";
            SubtitleText.Text = "Измените необходимые поля и сохраните";
            MetaPanel.Visibility = Visibility.Visible;
            txtCode.IsReadOnly = true;
        }

        private void LoadLookups()
        {
            cmbCategory.ItemsSource = _context.Categories.OrderBy(c => c.CategoryName).ToList();
            cmbCategory.DisplayMemberPath = "CategoryName";
            cmbCategory.SelectedValuePath = "CategoryID";

            var statuses = _context.AssetStatuses.OrderBy(s => s.StatusName).ToList();
            cmbStatus.ItemsSource = statuses;
            cmbStatus.DisplayMemberPath = "StatusName";
            cmbStatus.SelectedValuePath = "StatusID";

            // найдём ID статуса "Списан"
            _disposedStatusId = statuses
                .Where(s => s.StatusName == "Списан")
                .Select(s => (int?)s.StatusID)
                .FirstOrDefault();

            cmbManufacturer.ItemsSource = _context.Manufacturers.OrderBy(m => m.ManufacturerName).ToList();
            cmbManufacturer.DisplayMemberPath = "ManufacturerName";
            cmbManufacturer.SelectedValuePath = "ManufacturerID";

            cmbLocation.ItemsSource = _context.Locations.OrderBy(l => l.LocationName).ToList();
            cmbLocation.DisplayMemberPath = "LocationName";
            cmbLocation.SelectedValuePath = "LocationID";

            var employees = _context.Employees
                .Where(e => e.IsActive == true)
                .Select(e => new { e.EmployeeID, FullName = e.LastName + " " + e.FirstName })
                .OrderBy(e => e.FullName).ToList();
            cmbResponsible.ItemsSource = employees;
            cmbResponsible.DisplayMemberPath = "FullName";
            cmbResponsible.SelectedValuePath = "EmployeeID";
        }

        private bool ComputeIsActiveFromStatus(int statusId)
        {
            if (_disposedStatusId.HasValue)
                return statusId != _disposedStatusId.Value;
            // если "Списан" не найден в справочнике — считаем активным
            return true;
        }

        private void FillFields()
        {
            txtCode.Text = _entity.AssetCode;
            txtName.Text = _entity.AssetName;
            txtDescription.Text = _entity.Description;
            txtModel.Text = _entity.Model;
            txtSerial.Text = _entity.SerialNumber;
            dpPurchaseDate.SelectedDate = _entity.PurchaseDate;
            txtPrice.Text = _entity.PurchasePrice?.ToString("0.##");

            if (_entity.CategoryID != 0) cmbCategory.SelectedValue = _entity.CategoryID;
            if (_entity.StatusID != 0) cmbStatus.SelectedValue = _entity.StatusID;
            if (_entity.ManufacturerID.HasValue) cmbManufacturer.SelectedValue = _entity.ManufacturerID.Value;
            if (_entity.LocationID != 0) cmbLocation.SelectedValue = _entity.LocationID;
            if (_entity.ResponsibleEmployeeID.HasValue) cmbResponsible.SelectedValue = _entity.ResponsibleEmployeeID.Value;

            // IsActive — вычисляем из статуса
            var sid = _entity.StatusID;
            chkIsActive.IsChecked = ComputeIsActiveFromStatus(sid);

            var creator = _context.Users.FirstOrDefault(u => u.UserID == _entity.CreatedByUserID);
            var creatorName = creator?.Employees != null ? $"{creator.Employees.LastName} {creator.Employees.FirstName}" : "неизвестно";
            var createdDate = _entity.CreatedDate?.ToString("dd.MM.yyyy HH:mm") ?? "неизвестно";
            var modified = _entity.ModifiedDate.HasValue ? $" · Изменено: {_entity.ModifiedDate.Value:dd.MM.yyyy HH:mm}" : "";
            MetaText.Text = $"Создано: {createdDate} · Автор: {creatorName}{modified}";

            _isDirty = false;
            ValidateForm();
        }

        private void ShowError(string text)
        {
            ErrorText.Text = string.IsNullOrWhiteSpace(text) ? "" : $"• {text}";
            ErrorText.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool ValidateForm()
        {
            ShowError(null);
            if (string.IsNullOrWhiteSpace(txtCode.Text)) { ShowError("Введите код актива"); btnSave.IsEnabled = false; return false; }
            if (string.IsNullOrWhiteSpace(txtName.Text)) { ShowError("Введите название"); btnSave.IsEnabled = false; return false; }
            if (cmbCategory.SelectedValue == null) { ShowError("Выберите категорию"); btnSave.IsEnabled = false; return false; }
            if (cmbStatus.SelectedValue == null) { ShowError("Выберите статус"); btnSave.IsEnabled = false; return false; }
            if (cmbLocation.SelectedValue == null) { ShowError("Выберите локацию"); btnSave.IsEnabled = false; return false; }
            btnSave.IsEnabled = true;
            return true;
        }

        private void DoSave()
        {
            if (!ValidateForm()) return;

            try
            {
                decimal? price = null;
                var rawPrice = (txtPrice.Text ?? "").Replace("₽", "").Trim();
                if (!string.IsNullOrEmpty(rawPrice))
                {
                    if (decimal.TryParse(rawPrice, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out decimal p) ||
                        decimal.TryParse(rawPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out p))
                        price = p;
                    else { ShowError("Неверный формат стоимости"); return; }
                }

                if (_entity == null) // Создание
                {
                    if (_context.Assets.Any(a => a.AssetCode == txtCode.Text)) { ShowError("Актив с таким кодом уже существует"); return; }
                    _entity = new Assets
                    {
                        CreatedDate = DateTime.Now,
                        CreatedByUserID = _currentUser.UserID
                    };
                    _context.Assets.Add(_entity);
                }
                else // Редактирование
                {
                    _entity.ModifiedDate = DateTime.Now;
                    _entity.ModifiedByUserID = _currentUser.UserID;
                }

                _entity.AssetCode = txtCode.Text.Trim();
                _entity.AssetName = txtName.Text.Trim();
                _entity.Description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();
                _entity.CategoryID = Convert.ToInt32(cmbCategory.SelectedValue);
                _entity.StatusID = Convert.ToInt32(cmbStatus.SelectedValue);
                _entity.LocationID = Convert.ToInt32(cmbLocation.SelectedValue);

                // фикс: SelectedValue -> int?
                _entity.ManufacturerID = (cmbManufacturer.SelectedValue != null) ? Convert.ToInt32(cmbManufacturer.SelectedValue) : (int?)null;
                _entity.Model = string.IsNullOrWhiteSpace(txtModel.Text) ? null : txtModel.Text.Trim();
                _entity.SerialNumber = string.IsNullOrWhiteSpace(txtSerial.Text) ? null : txtSerial.Text.Trim();
                _entity.PurchaseDate = dpPurchaseDate.SelectedDate;
                _entity.PurchasePrice = price;

                _entity.ResponsibleEmployeeID = (cmbResponsible.SelectedValue != null) ? Convert.ToInt32(cmbResponsible.SelectedValue) : (int?)null;

                // IsActive вычисляем из статуса
                _entity.IsActive = ComputeIsActiveFromStatus(_entity.StatusID);

                _context.SaveChanges();
                _isDirty = false;
                DialogResult = true;
                Close();
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"{x.PropertyName}: {x.ErrorMessage}");
                var fullErrorMessage = string.Join("\n", errorMessages);
                ShowError($"Ошибка валидации:\n{fullErrorMessage}");
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                ShowError($"Ошибка сохранения: {innerMessage}");
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

        private void GenerateCode_Click(object sender, RoutedEventArgs e) =>
            txtCode.Text = $"AST-{DateTime.Now:yyyyMMddHHmmss}";

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Price_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.SourceDataObject.GetData(DataFormats.Text) is string text && !Regex.IsMatch(text, @"^[0-9\.,]+$"))
                e.CancelCommand();
        }

        private void Price_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"[0-9\.,]");
    }
}
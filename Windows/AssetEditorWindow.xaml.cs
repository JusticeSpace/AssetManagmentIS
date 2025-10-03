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
        private bool _dirty;

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

            this.Closing += AssetEditorWindow_Closing;
        }

        private void AttachDirtyHandlers()
        {
            foreach (var tb in new[] { txtCode, txtName, txtModel, txtSerial, txtPrice, txtDescription })
                tb.TextChanged += (_, __) => { _dirty = true; ValidateForm(); };
            cmbCategory.SelectionChanged += (_, __) => { _dirty = true; ValidateForm(); };
            cmbStatus.SelectionChanged += (_, __) => { _dirty = true; ValidateForm(); };
            cmbManufacturer.SelectionChanged += (_, __) => { _dirty = true; };
            cmbLocation.SelectionChanged += (_, __) => { _dirty = true; ValidateForm(); };
            cmbResponsible.SelectionChanged += (_, __) => { _dirty = true; };
            dpPurchaseDate.SelectedDateChanged += (_, __) => { _dirty = true; };
            chkIsActive.Checked += (_, __) => { _dirty = true; };
            chkIsActive.Unchecked += (_, __) => { _dirty = true; };
        }

        private void SetModeAdd()
        {
            TitleText.Text = "Новый актив";
            ModeChipText.Text = "добавление";
            SubtitleText.Text = "Заполните обязательные поля со знаком •";
            MetaPanel.Visibility = Visibility.Collapsed;
            txtCode.IsReadOnly = false;
            chkIsActive.IsChecked = true;
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

            cmbStatus.ItemsSource = _context.AssetStatuses.OrderBy(s => s.StatusName).ToList();
            cmbStatus.DisplayMemberPath = "StatusName";
            cmbStatus.SelectedValuePath = "StatusID";

            cmbManufacturer.ItemsSource = _context.Manufacturers.OrderBy(m => m.ManufacturerName).ToList();
            cmbManufacturer.DisplayMemberPath = "ManufacturerName";
            cmbManufacturer.SelectedValuePath = "ManufacturerID";

            cmbLocation.ItemsSource = _context.Locations.OrderBy(l => l.LocationName).ToList();
            cmbLocation.DisplayMemberPath = "LocationName";
            cmbLocation.SelectedValuePath = "LocationID";

            var employees = _context.Employees
                .Where(e => e.IsActive == true)
                .Select(e => new
                {
                    e.EmployeeID,
                    FullName = e.LastName + " " + e.FirstName + (string.IsNullOrEmpty(e.MiddleName) ? "" : " " + e.MiddleName)
                })
                .OrderBy(e => e.FullName).ToList();

            cmbResponsible.ItemsSource = employees;
            cmbResponsible.DisplayMemberPath = "FullName";
            cmbResponsible.SelectedValuePath = "EmployeeID";
        }

        private void FillFields()
        {
            txtCode.Text = _entity.AssetCode;
            txtName.Text = _entity.AssetName;
            txtDescription.Text = _entity.Description;
            txtModel.Text = _entity.Model;
            txtSerial.Text = _entity.SerialNumber;
            dpPurchaseDate.SelectedDate = _entity.PurchaseDate;

            txtPrice.Text = _entity.PurchasePrice.HasValue ? _entity.PurchasePrice.Value.ToString("0.##") : "";

            if (_entity.CategoryID != 0) cmbCategory.SelectedValue = _entity.CategoryID;
            if (_entity.StatusID != 0) cmbStatus.SelectedValue = _entity.StatusID;
            if (_entity.ManufacturerID.HasValue) cmbManufacturer.SelectedValue = _entity.ManufacturerID.Value;
            if (_entity.LocationID != 0) cmbLocation.SelectedValue = _entity.LocationID;
            if (_entity.ResponsibleEmployeeID.HasValue) cmbResponsible.SelectedValue = _entity.ResponsibleEmployeeID.Value;

            chkIsActive.IsChecked = _entity.IsActive ?? true;

            var creator = _context.Users.FirstOrDefault(u => u.UserID == _entity.CreatedByUserID);
            var creatorName = creator?.Employees != null
                ? (creator.Employees.LastName + " " + creator.Employees.FirstName)
                : "неизвестно";
            var modified = _entity.ModifiedDate.HasValue ? " · Изменено: " + _entity.ModifiedDate.Value.ToString("dd.MM.yyyy HH:mm") : "";

            MetaText.Text = "Создано: " + (_entity.CreatedDate.HasValue ? _entity.CreatedDate.Value.ToString("dd.MM.yyyy HH:mm") : "неизвестно")
                            + " · Автор: " + creatorName + modified;

            _dirty = false;
            ValidateForm();
        }

        private void ShowError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ErrorText.Visibility = Visibility.Collapsed;
                ErrorText.Text = "";
            }
            else
            {
                ErrorText.Text = "• " + text;
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void ValidateForm()
        {
            btnSave.IsEnabled = true;
            ShowError(null);

            if (string.IsNullOrWhiteSpace(txtCode.Text))
            {
                btnSave.IsEnabled = false;
                ShowError("Введите код актива");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                btnSave.IsEnabled = false;
                ShowError("Введите название");
                return;
            }
            if (cmbCategory.SelectedValue == null || cmbStatus.SelectedValue == null || cmbLocation.SelectedValue == null)
            {
                btnSave.IsEnabled = false;
                ShowError("Выберите категорию, статус и локацию");
                return;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => DoSave();

        private void DoSave()
        {
            try
            {
                ValidateForm();
                if (!btnSave.IsEnabled) return;

                decimal? price = null;
                var raw = (txtPrice.Text ?? "").Replace("₽", "").Replace(" ", "").Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    decimal tmp;
                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out tmp) ||
                        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out tmp))
                    {
                        price = tmp;
                    }
                    else
                    {
                        ShowError("Введите корректную стоимость");
                        return;
                    }
                }

                bool exists = _context.Assets.Any(a => a.AssetCode == txtCode.Text && (_entity == null || a.AssetID != _entity.AssetID));
                if (exists)
                {
                    ShowError("Актив с таким кодом уже существует");
                    return;
                }

                if (_entity == null)
                {
                    _entity = new Assets
                    {
                        AssetCode = txtCode.Text.Trim(),
                        AssetName = txtName.Text.Trim(),
                        Description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim(),
                        CategoryID = (int)cmbCategory.SelectedValue,
                        StatusID = (int)cmbStatus.SelectedValue,
                        LocationID = (int)cmbLocation.SelectedValue,
                        ManufacturerID = cmbManufacturer.SelectedValue as int?,
                        Model = string.IsNullOrWhiteSpace(txtModel.Text) ? null : txtModel.Text.Trim(),
                        SerialNumber = string.IsNullOrWhiteSpace(txtSerial.Text) ? null : txtSerial.Text.Trim(),
                        PurchaseDate = dpPurchaseDate.SelectedDate,
                        PurchasePrice = price,
                        ResponsibleEmployeeID = cmbResponsible.SelectedValue as int?,
                        CreatedDate = DateTime.Now,
                        CreatedByUserID = _currentUser.UserID,
                        IsActive = chkIsActive.IsChecked ?? true
                    };
                    _context.Assets.Add(_entity);
                }
                else
                {
                    _entity.AssetName = txtName.Text.Trim();
                    _entity.Description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();
                    _entity.CategoryID = (int)cmbCategory.SelectedValue;
                    _entity.StatusID = (int)cmbStatus.SelectedValue;
                    _entity.LocationID = (int)cmbLocation.SelectedValue;
                    _entity.ManufacturerID = cmbManufacturer.SelectedValue as int?;
                    _entity.Model = string.IsNullOrWhiteSpace(txtModel.Text) ? null : txtModel.Text.Trim();
                    _entity.SerialNumber = string.IsNullOrWhiteSpace(txtSerial.Text) ? null : txtSerial.Text.Trim();
                    _entity.PurchaseDate = dpPurchaseDate.SelectedDate;
                    _entity.PurchasePrice = price;
                    _entity.ResponsibleEmployeeID = cmbResponsible.SelectedValue as int?;
                    _entity.ModifiedDate = DateTime.Now;
                    _entity.ModifiedByUserID = _currentUser.UserID;
                    _entity.IsActive = chkIsActive.IsChecked ?? true;
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

        private void Price_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text)) { e.CancelCommand(); return; }
            var text = e.SourceDataObject.GetData(DataFormats.Text) as string;
            if (!Regex.IsMatch(text ?? "", @"^[0-9\.,\s]+$")) e.CancelCommand();
        }
        private void Price_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9\.,\s]+$");
        }

        private void AssetEditorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_dirty)
            {
                var r = MessageBox.Show("Есть несохранённые изменения. Закрыть без сохранения?",
                    "Подтвердите", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.No) e.Cancel = true;
            }
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => DoSave();
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = btnSave.IsEnabled;
        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e) => Close();
        private void GenerateCode_Click(object sender, RoutedEventArgs e) => txtCode.Text = "AST-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
using CRS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CRS
{
    public partial class AddRowWindow : Window
    {
        private readonly string _baseTable;
        private readonly int _createdByUserId;

        private readonly Dictionary<string, (string ParentTable, string Pk, string Display)>
            _foreignKeys = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, object> _defaults =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private class FieldBinding
        {
            public string ColumnName { get; set; }
            public DataColumn Column { get; set; }
            public Control Input { get; set; }
            public CheckBox NullCheck { get; set; }
            public string FkParentTable { get; set; }
        }

        private readonly List<FieldBinding> _fields = new List<FieldBinding>();
        private DataTable _schema;

        private ComboBox _categoryCombo;
        private ComboBox _manufacturerCombo;
        private ComboBox _modelCombo;

        private ComboBox _departmentCombo;
        private ComboBox _userCombo;
        private string _departmentColumn;
        private string _userColumn;

        private bool _suppressUserDepartmentSync = false;

        public AddRowWindow(string baseTable, int createdByUserId)
        {
            InitializeComponent();
            _baseTable = baseTable;
            _createdByUserId = createdByUserId;

            Title = $"Добавление записи: {baseTable}";
            TxtHeader.Text = $"Новая запись в {baseTable}";

            LoadForeignKeysMap();
            LoadDefaults();
            LoadSchema();
            BuildFields();
        }

        private void LoadForeignKeysMap()
        {
            if (_baseTable == "CRS_Equipment")
            {
                _foreignKeys["ModelId"] = ("CRS_Models", "ModelId", "ModelName");
                _foreignKeys["CategoryId"] = ("CRS_Categories", "CategoryId", "CategoryName");
                _foreignKeys["ManufacturerId"] = ("CRS_Manufacturers", "ManufacturerId", "ManufacturerName");
                _foreignKeys["SupplierId"] = ("CRS_Suppliers", "SupplierId", "CompanyName");
                _foreignKeys["StateId"] = ("CRS_EquipmentStates", "StateId", "StateName");
                _foreignKeys["CurrentUserId"] = ("CRS_Users", "UserId", "FullName");
                _foreignKeys["CurrentDepartmentId"] = ("CRS_Departments", "DepartmentId", "DepartmentName");
            }
            else if (_baseTable == "CRS_Models")
            {
                _foreignKeys["CategoryId"] = ("CRS_Categories", "CategoryId", "CategoryName");
                _foreignKeys["ManufacturerId"] = ("CRS_Manufacturers", "ManufacturerId", "ManufacturerName");
            }
            else if (_baseTable == "CRS_Users")
            {
                _foreignKeys["DepartmentId"] = ("CRS_Departments", "DepartmentId", "DepartmentName");
            }
            else if (_baseTable == "CRS_Movements")
            {
                _foreignKeys["EquipmentId"] = ("CRS_Equipment", "EquipmentId", "Name");
                _foreignKeys["OperationTypeId"] = ("CRS_OperationTypes", "OperationTypeId", "TypeName");
                _foreignKeys["FromUserId"] = ("CRS_Users", "UserId", "FullName");
                _foreignKeys["ToUserId"] = ("CRS_Users", "UserId", "FullName");
                _foreignKeys["FromDepartmentId"] = ("CRS_Departments", "DepartmentId", "DepartmentName");
                _foreignKeys["ToDepartmentId"] = ("CRS_Departments", "DepartmentId", "DepartmentName");
                _foreignKeys["SupplierId"] = ("CRS_Suppliers", "SupplierId", "CompanyName");
            }
            else if (_baseTable == "CRS_Departments")
            {
                _foreignKeys["ParentId"] = ("CRS_Departments", "DepartmentId", "DepartmentName");
            }
        }

        private void LoadDefaults()
        {
            if (_baseTable == "CRS_Equipment")
            {
                _defaults["ReceiptDate"] = DateTime.Today;
                _defaults["StateId"] = GetLookupId("CRS_EquipmentStates", "StateName", "На складе");
                _defaults["CurrentDepartmentId"] = GetLookupId("CRS_Departments", "DepartmentName", "Склад");
            }
            else if (_baseTable == "CRS_Movements")
            {
                _defaults["MovementDate"] = DateTime.Now;
                _defaults["OperationTypeId"] = GetLookupId("CRS_OperationTypes", "TypeName", "Передача");
            }
        }

        private object GetLookupId(string table, string nameCol, string value)
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        $"SELECT TOP 1 {GetPkOf(table)} FROM {table} WHERE {nameCol} = @v", conn))
                    {
                        cmd.Parameters.AddWithValue("@v", value);
                        object r = cmd.ExecuteScalar();
                        return r == null || r == DBNull.Value ? null : r;
                    }
                }
            }
            catch { return null; }
        }

        private string GetPkOf(string table) => table switch
        {
            "CRS_EquipmentStates" => "StateId",
            "CRS_Departments" => "DepartmentId",
            "CRS_OperationTypes" => "OperationTypeId",
            "CRS_Categories" => "CategoryId",
            "CRS_Manufacturers" => "ManufacturerId",
            "CRS_Suppliers" => "SupplierId",
            "CRS_Users" => "UserId",
            "CRS_Models" => "ModelId",
            "CRS_Equipment" => "EquipmentId",
            "CRS_Roles" => "RoleId",
            _ => "Id"
        };

        private void LoadSchema()
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(
                    $"SELECT TOP 0 * FROM {_baseTable}", conn))
                {
                    _schema = new DataTable();
                    adapter.FillSchema(_schema, SchemaType.Source);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось получить схему таблицы:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void BuildFields()
        {
            foreach (DataColumn col in _schema.Columns)
            {
                if (col.AutoIncrement)
                {
                    FieldsPanel.Children.Add(new TextBlock
                    {
                        Text = $"{col.ColumnName} — генерируется автоматически",
                        Foreground = Brushes.Gray,
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(0, 6, 0, 2)
                    });
                    continue;
                }

                bool isFk = _foreignKeys.TryGetValue(col.ColumnName, out var fk);

                var label = new TextBlock
                {
                    Text = col.ColumnName + (col.AllowDBNull ? "" : " *"),
                    Margin = new Thickness(0, 6, 0, 2),
                    FontWeight = col.AllowDBNull ? FontWeights.Normal : FontWeights.SemiBold
                };
                FieldsPanel.Children.Add(label);

                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                Control input;
                object defaultValue = _defaults.TryGetValue(col.ColumnName, out var dv) ? dv : null;

                if (isFk)
                {
                    var cb = new ComboBox
                    {
                        Width = 260,
                        DisplayMemberPath = fk.Display,
                        SelectedValuePath = fk.Pk,
                        Tag = fk
                    };
                    LoadComboItems(cb, fk.ParentTable, fk.Pk, fk.Display);
                    if (defaultValue != null) cb.SelectedValue = defaultValue;

                    if (col.ColumnName == "CategoryId") _categoryCombo = cb;
                    else if (col.ColumnName == "ManufacturerId") _manufacturerCombo = cb;
                    else if (col.ColumnName == "ModelId") _modelCombo = cb;

                    if (col.ColumnName == "CurrentDepartmentId" || col.ColumnName == "ToDepartmentId" || col.ColumnName == "FromDepartmentId")
                    {
                        _departmentCombo = cb;
                        _departmentColumn = col.ColumnName;
                    }
                    else if (col.ColumnName == "CurrentUserId" || col.ColumnName == "ToUserId" || col.ColumnName == "FromUserId")
                    {
                        _userCombo = cb;
                        _userColumn = col.ColumnName;
                    }

                    input = cb;
                }
                else if (col.DataType == typeof(bool))
                {
                    input = new CheckBox
                    {
                        IsChecked = defaultValue is bool b ? b : false,
                        Width = 160
                    };
                }
                else if (col.DataType == typeof(DateTime))
                {
                    input = new DatePicker
                    {
                        SelectedDate = defaultValue is DateTime dt ? dt : (DateTime?)null,
                        Width = 200
                    };
                }
                else
                {
                    input = new TextBox
                    {
                        Text = defaultValue?.ToString() ?? "",
                        Width = 260
                    };
                }

                var field = new FieldBinding
                {
                    ColumnName = col.ColumnName,
                    Column = col,
                    Input = input,
                    FkParentTable = isFk ? fk.ParentTable : null
                };

                if (col.AllowDBNull && col.DataType != typeof(bool) && !isFk)
                {
                    var nullCheck = new CheckBox
                    {
                        Content = "(не задано)",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0),
                        IsChecked = false,
                        Tag = field
                    };
                    nullCheck.Checked += NullCheck_Changed;
                    nullCheck.Unchecked += NullCheck_Changed;

                    field.NullCheck = nullCheck;
                    rowPanel.Children.Add(input);
                    rowPanel.Children.Add(nullCheck);
                }
                else
                {
                    rowPanel.Children.Add(input);
                }

                FieldsPanel.Children.Add(rowPanel);
                _fields.Add(field);
            }

            if (_categoryCombo != null)
                _categoryCombo.SelectionChanged += CategoryCombo_SelectionChanged;
            if (_manufacturerCombo != null)
                _manufacturerCombo.SelectionChanged += ManufacturerCombo_SelectionChanged;

            if (_departmentCombo != null)
                _departmentCombo.SelectionChanged += DepartmentCombo_SelectionChanged;
            if (_userCombo != null)
                _userCombo.SelectionChanged += UserCombo_SelectionChanged;

            RefreshManufacturersByCategory(false);
            RefreshModels(false);
            RefreshUsers(false);

            if (_departmentCombo?.SelectedValue != null)
                RefreshUsers(false);
        }

        private void LoadComboItems(ComboBox cb, string table, string pk, string display)
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(
                    $"SELECT {pk}, {display} FROM {table} ORDER BY {display}", conn))
                {
                    var t = new DataTable();
                    adapter.Fill(t);
                    cb.ItemsSource = t.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить {table}:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void NullCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is FieldBinding field)
            {
                bool isNull = cb.IsChecked == true;
                field.Input.IsEnabled = !isNull;

                if (isNull)
                {
                    if (field.Input is TextBox tb) tb.Text = string.Empty;
                    else if (field.Input is DatePicker dp) dp.SelectedDate = null;
                }
            }
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshManufacturersByCategory(false);
            RefreshModels(false);
        }

        private void ManufacturerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshModels(false);
        }

        private void RefreshManufacturersByCategory(bool preserveCurrent)
        {
            if (_manufacturerCombo == null) return;

            object catId = _categoryCombo?.SelectedValue;
            object currentManId = preserveCurrent ? _manufacturerCombo.SelectedValue : null;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (catId == null)
                    {
                        cmd.CommandText =
                            "SELECT ManufacturerId, ManufacturerName FROM CRS_Manufacturers " +
                            "ORDER BY ManufacturerName";
                    }
                    else
                    {
                        cmd.CommandText = @"
                            SELECT mf.ManufacturerId, mf.ManufacturerName
                            FROM CRS_Manufacturers mf
                            JOIN CRS_ManufacturerCategories mc
                              ON mc.ManufacturerId = mf.ManufacturerId
                            WHERE mc.CategoryId = @CatId
                            ORDER BY mf.ManufacturerName";
                        cmd.Parameters.AddWithValue("@CatId", catId);
                    }

                    var table = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                        adapter.Fill(table);

                    _manufacturerCombo.ItemsSource = table.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить производителей:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentManId != null && _manufacturerCombo.ItemsSource is DataView view)
            {
                bool stillPresent = false;
                foreach (DataRowView r in view)
                    if (Equals(r["ManufacturerId"], currentManId)) { stillPresent = true; break; }
                _manufacturerCombo.SelectedValue = stillPresent ? currentManId : null;
            }
            else if (!preserveCurrent)
            {
                _manufacturerCombo.SelectedValue = null;
            }
        }

        private void RefreshModels(bool preserveCurrent)
        {
            if (_modelCombo == null) return;

            object catId = _categoryCombo?.SelectedValue;
            object manId = _manufacturerCombo?.SelectedValue;
            object currentModel = preserveCurrent ? _modelCombo.SelectedValue : null;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT ModelId, ModelName
                        FROM CRS_Models
                        WHERE (@CatId IS NULL OR CategoryId = @CatId)
                          AND (@ManId IS NULL OR ManufacturerId = @ManId)
                        ORDER BY ModelName";
                    cmd.Parameters.AddWithValue("@CatId", catId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ManId", manId ?? (object)DBNull.Value);

                    var table = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                        adapter.Fill(table);

                    _modelCombo.ItemsSource = table.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить модели:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentModel != null && _modelCombo.ItemsSource is DataView view)
            {
                bool stillPresent = false;
                foreach (DataRowView r in view)
                    if (Equals(r["ModelId"], currentModel)) { stillPresent = true; break; }
                _modelCombo.SelectedValue = stillPresent ? currentModel : null;
            }
            else if (!preserveCurrent)
            {
                _modelCombo.SelectedValue = null;
            }
        }

        private void DepartmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshUsers(false);
        }

        private void UserCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUserDepartmentSync) return;
            if (_userCombo.SelectedValue == null) return;
            if (_departmentCombo == null) return;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT DepartmentId FROM CRS_Users WHERE UserId = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _userCombo.SelectedValue);
                        object deptId = cmd.ExecuteScalar();

                        if (deptId != null && deptId != DBNull.Value)
                        {
                            _suppressUserDepartmentSync = true;
                            _departmentCombo.SelectedValue = deptId;
                            _suppressUserDepartmentSync = false;

                            RefreshUsers(true);
                            _userCombo.SelectedValue = GetCurrentUserIdSelection();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось определить отдел сотрудника:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private object GetCurrentUserIdSelection()
        {
            return _userCombo?.SelectedValue;
        }


        private void RefreshUsers(bool preserveCurrent)
        {
            if (_userCombo == null) return;

            object deptId = _departmentCombo?.SelectedValue;
            object currentUserId = preserveCurrent ? _userCombo.SelectedValue : null;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    if (deptId == null)
                    {
                        cmd.CommandText = @"
                            SELECT UserId, FullName
                            FROM CRS_Users
                            WHERE IsActive = 1
                            ORDER BY FullName";
                    }
                    else
                    {
                        cmd.CommandText = @"
                            SELECT UserId, FullName
                            FROM CRS_Users
                            WHERE IsActive = 1 AND DepartmentId = @DeptId
                            ORDER BY FullName";
                        cmd.Parameters.AddWithValue("@DeptId", deptId);
                    }

                    var table = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                        adapter.Fill(table);

                    _suppressUserDepartmentSync = true;
                    _userCombo.ItemsSource = table.DefaultView;

                    if (currentUserId != null)
                    {
                        bool stillPresent = false;
                        foreach (DataRowView r in table.DefaultView)
                            if (Equals(r["UserId"], currentUserId)) { stillPresent = true; break; }
                        _userCombo.SelectedValue = stillPresent ? currentUserId : null;
                    }
                    else
                    {
                        _userCombo.SelectedValue = null;
                    }
                    _suppressUserDepartmentSync = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить сотрудников:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            foreach (var field in _fields)
            {
                bool isNull = field.NullCheck?.IsChecked == true;
                if (!field.Column.AllowDBNull && !isNull && IsEmpty(field.Input))
                {
                    TxtStatus.Text = $"Поле «{field.ColumnName}» обязательно.";
                    return;
                }
            }

            var columns = new List<string>();
            var paramNames = new List<string>();
            var parameters = new List<SqlParameter>();
            var fkColumns = new List<string>();

            foreach (var field in _fields)
            {
                bool isNull = field.NullCheck?.IsChecked == true;
                object value = isNull ? DBNull.Value : ReadValue(field);

                if (value == null)
                {
                    TxtStatus.Text = $"Не удалось обработать поле «{field.ColumnName}».";
                    return;
                }

                string paramName = "@p_" + field.ColumnName;
                columns.Add($"[{field.ColumnName}]");
                paramNames.Add(paramName);
                parameters.Add(new SqlParameter(paramName, value));

                if (field.FkParentTable != null)
                    fkColumns.Add(field.ColumnName);
            }

            if (columns.Count == 0)
            {
                TxtStatus.Text = "Нет полей для вставки.";
                return;
            }

            string message =
                $"Добавить новую запись в {_baseTable}?\n\n" +
                $"Заполнено полей: {columns.Count}\n";
            if (fkColumns.Count > 0)
                message += $"Связанные ссылки: {string.Join(", ", fkColumns)}\n";
            message += "\nПродолжить?";

            if (MessageBox.Show(message, "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string sql =
                $"INSERT INTO {_baseTable} ({string.Join(", ", columns)}) " +
                $"VALUES ({string.Join(", ", paramNames)}); " +
                "SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            long newId = 0;
                            using (var cmd = new SqlCommand(sql, conn, tran))
                            {
                                foreach (var p in parameters) cmd.Parameters.Add(p);
                                object result = cmd.ExecuteScalar();
                                if (result != null && result != DBNull.Value)
                                    newId = Convert.ToInt64(result);
                            }

                            using (var audit = new SqlCommand(@"
                                INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                                VALUES (@UserId, N'INSERT', @Entity, @EntityId, @Details)",
                                conn, tran))
                            {
                                audit.Parameters.AddWithValue("@UserId",
                                    _createdByUserId > 0 ? (object)_createdByUserId : DBNull.Value);
                                audit.Parameters.AddWithValue("@Entity", _baseTable);
                                audit.Parameters.AddWithValue("@EntityId",
                                    newId > 0 ? newId.ToString() : "?");
                                audit.Parameters.AddWithValue("@Details",
                                    $"Добавлена новая запись в {_baseTable} (полей: {columns.Count})");
                                audit.ExecuteNonQuery();
                            }

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show("Запись добавлена.", "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Ошибка сохранения: " + ex.Message;
            }
        }

        private bool IsEmpty(Control input)
        {
            if (input is TextBox tb) return string.IsNullOrWhiteSpace(tb.Text);
            if (input is DatePicker dp) return dp.SelectedDate == null;
            if (input is ComboBox cb) return cb.SelectedValue == null;
            return false;
        }

        private object ReadValue(FieldBinding field)
        {
            Control input = field.Input;
            DataColumn column = field.Column;

            if (input is ComboBox cb)
                return cb.SelectedValue ?? (column.AllowDBNull ? (object)DBNull.Value : null);

            if (input is CheckBox chk)
                return chk.IsChecked == true;

            if (input is DatePicker dp)
            {
                if (dp.SelectedDate == null)
                    return column.AllowDBNull ? (object)DBNull.Value : null;
                return dp.SelectedDate.Value;
            }

            string text = ((TextBox)input).Text;
            if (string.IsNullOrWhiteSpace(text))
                return column.AllowDBNull ? (object)DBNull.Value : null;

            try
            {
                return Convert.ChangeType(text, column.DataType);
            }
            catch
            {
                TxtStatus.Text = $"Нельзя преобразовать «{text}» " +
                                 $"к типу {column.DataType.Name} для «{column.ColumnName}».";
                return null;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
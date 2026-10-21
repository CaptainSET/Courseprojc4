using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRS
{
    public partial class StorekeeperWindow : Window
    {
        private readonly int _userId;
        private readonly string _userName;
        private readonly string _userRoles;

        private class TableItem
        {
            public string DisplayName { get; set; }
            public string SqlQuery { get; set; }
            public string BaseTable { get; set; }
            public string PkColumn { get; set; }
        }

        private TableItem[] _tables;

        public StorekeeperWindow(int userId, string fullName, string roles)
        {
            InitializeComponent();
            _userId = userId;
            _userName = fullName;
            _userRoles = roles;
            TxtUserInfo.Text = $"Пользователь: {_userName}";
            TxtUserRoles.Text = $"Роли: {_userRoles}";

            InitTables();
            CmbTables.ItemsSource = _tables;
            CmbTables.SelectedIndex = 0;
        }

        private void InitTables()
        {
            _tables = new[]
            {
                new TableItem { DisplayName = "Категории",
                    SqlQuery = @"SELECT c.CategoryId, c.CategoryName
                               FROM CRS_Categories c",
                    BaseTable = "CRS_Categories", PkColumn = "CategoryId" },
                new TableItem { DisplayName = "Оргтехника",
                    SqlQuery = @"SELECT e.EquipmentId, e.Name,
                                        ISNULL(m.ModelName, N'(нет)') AS Model,
                                        e.SerialNumber,
                                        ISNULL(c.CategoryName, N'(нет)') AS Category,
                                        ISNULL(mf.ManufacturerName, N'(нет)') AS Manufacturer,
                                        ISNULL(s.CompanyName, N'(нет)') AS Supplier,
                                        e.ReceiptDate, e.WarrantyMonths, e.Price,
                                        st.StateName AS State,
                                        ISNULL(u.FullName, N'(нет)') AS ResponsibleUser,
                                        ISNULL(d.DepartmentName, N'(нет)') AS Department,
                                        e.Notes
                                 FROM CRS_Equipment e
                                 LEFT JOIN CRS_Models m ON m.ModelId = e.ModelId
                                 LEFT JOIN CRS_Categories c ON c.CategoryId = e.CategoryId
                                 LEFT JOIN CRS_Manufacturers mf ON mf.ManufacturerId = e.ManufacturerId
                                 LEFT JOIN CRS_Suppliers s ON s.SupplierId = e.SupplierId
                                 LEFT JOIN CRS_EquipmentStates st ON st.StateId = e.StateId
                                 LEFT JOIN CRS_Users u ON u.UserId = e.CurrentUserId
                                 LEFT JOIN CRS_Departments d ON d.DepartmentId = e.CurrentDepartmentId
                                 ORDER BY e.EquipmentId",
                    BaseTable = "CRS_Equipment", PkColumn = "EquipmentId" },
                new TableItem { DisplayName = "Модели",
                    SqlQuery = @"SELECT m.ModelId, m.ModelName,
                                        c.CategoryName AS Category,
                                        ISNULL(mf.ManufacturerName, N'(нет)') AS Manufacturer,
                                        m.Description
                                 FROM CRS_Models m
                                 LEFT JOIN CRS_Categories c ON c.CategoryId = m.CategoryId
                                 LEFT JOIN CRS_Manufacturers mf ON mf.ManufacturerId = m.ManufacturerId
                                 ORDER BY m.ModelId",
                    BaseTable = "CRS_Models", PkColumn = "ModelId" },
                new TableItem { DisplayName = "Поставщики",
                    SqlQuery = @"SELECT SupplierId, CompanyName, LegalAddress, ContactPhone,
                                        DirectorFullName, AccountNumber FROM CRS_Suppliers ORDER BY SupplierId",
                    BaseTable = "CRS_Suppliers", PkColumn = "SupplierId" },
                new TableItem { DisplayName = "Перемещения",
                    SqlQuery = @"SELECT mv.MovementId, mv.MovementDate,
                                        ot.TypeName AS OperationType,
                                        e.Name AS Equipment, e.SerialNumber,
                                        ISNULL(uf.FullName, N'(нет)') AS FromUser,
                                        ISNULL(ut.FullName, N'(нет)') AS ToUser,
                                        ISNULL(df.DepartmentName, N'(нет)') AS FromDepartment,
                                        ISNULL(dt.DepartmentName, N'(нет)') AS ToDepartment,
                                        ISNULL(s.CompanyName, N'(нет)') AS Supplier,
                                        mv.Comment
                                 FROM CRS_Movements mv
                                 JOIN CRS_OperationTypes ot ON ot.OperationTypeId = mv.OperationTypeId
                                 JOIN CRS_Equipment e ON e.EquipmentId = mv.EquipmentId
                                 LEFT JOIN CRS_Users uf ON uf.UserId = mv.FromUserId
                                 LEFT JOIN CRS_Users ut ON ut.UserId = mv.ToUserId
                                 LEFT JOIN CRS_Departments df ON df.DepartmentId = mv.FromDepartmentId
                                 LEFT JOIN CRS_Departments dt ON dt.DepartmentId = mv.ToDepartmentId
                                 LEFT JOIN CRS_Suppliers s ON s.SupplierId = mv.SupplierId
                                 ORDER BY mv.MovementId DESC",
                    BaseTable = "CRS_Movements", PkColumn = "MovementId" }
            };
        }

        private void CmbTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => LoadSelectedTable();

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => LoadSelectedTable();

        private void LoadSelectedTable()
        {
            if (!(CmbTables.SelectedItem is TableItem item)) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(item.SqlQuery, conn))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    GridData.ItemsSource = table.DefaultView;
                    TxtRowCount.Text = $"Строк: {table.Rows.Count}";
                    TxtTableName.Text = item.DisplayName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки:\n" + ex.Message);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(GridData.ItemsSource is DataView view)) return;
            string filter = TxtFilter.Text.Trim();
            if (string.IsNullOrEmpty(filter)) { view.RowFilter = ""; return; }
            string safe = filter.Replace("'", "''");
            var parts = new System.Collections.Generic.List<string>();
            foreach (DataColumn col in view.Table.Columns)
                parts.Add($"Convert([{col.ColumnName}], 'System.String') LIKE '%{safe}%'");
            view.RowFilter = string.Join(" OR ", parts);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!(CmbTables.SelectedItem is TableItem item)) return;

            var dlg = new AddRowWindow(item.BaseTable, _userId) { Owner = this };
            if (dlg.ShowDialog() == true) LoadSelectedTable();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!(CmbTables.SelectedItem is TableItem item)) return;
            if (!(GridData.SelectedItem is DataRowView row))
            { MessageBox.Show("Выберите строку."); return; }

            var dlg = new EditRowWindow(item.BaseTable, item.PkColumn, row, isAdmin: false)
            { Owner = this };
            if (dlg.ShowDialog() == true) LoadSelectedTable();
        }

        private void BtnMovement_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MovementWindow(_userId) { Owner = this };
            if (dlg.ShowDialog() == true) LoadSelectedTable();
        }

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            new ProfileWindow(_userId) { Owner = this }.ShowDialog();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            new LoginWindow().Show();
            Close();
        }
    }
}
using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRS
{
    public partial class MainWindow : Window
    {
        private readonly int _currentUserId;
        private readonly string _currentUserName;
        private readonly string _currentUserRoles;

        private class TableItem
        {
            public string DisplayName { get; set; }
            public string SqlQuery { get; set; }
        }

        private TableItem[] _tables;

        public MainWindow() : this(0, "Гость", "(без роли)") { }

        public MainWindow(int userId, string fullName, string roles)
        {
            InitializeComponent();

            _currentUserId = userId;
            _currentUserName = fullName;
            _currentUserRoles = roles;

            TxtUserInfo.Text = $"Пользователь: {_currentUserName}";
            TxtUserRoles.Text = $"Роли: {_currentUserRoles}";

            InitTables();
            CmbTables.ItemsSource = _tables;
            CmbTables.SelectedIndex = 0;
        }

        private void InitTables()
        {
            _tables = new[]
            {
                new TableItem { DisplayName = "Роли",
                    SqlQuery = "SELECT RoleId, RoleName, Description FROM CRS_Roles ORDER BY RoleId" },

                new TableItem { DisplayName = "Подразделения",
                    SqlQuery = @"
                        SELECT d.DepartmentId,
                               d.DepartmentName,
                               ISNULL(p.DepartmentName, N'(нет)') AS ParentDepartment
                        FROM CRS_Departments d
                        LEFT JOIN CRS_Departments p ON p.DepartmentId = d.ParentId
                        ORDER BY d.DepartmentId" },

                new TableItem { DisplayName = "Категории",
                    SqlQuery = "SELECT CategoryId, CategoryName FROM CRS_Categories ORDER BY CategoryId" },

                new TableItem { DisplayName = "Производители",
                    SqlQuery = "SELECT ManufacturerId, ManufacturerName, Country FROM CRS_Manufacturers ORDER BY ManufacturerId" },

                new TableItem { DisplayName = "Поставщики",
                    SqlQuery = @"SELECT SupplierId, CompanyName, LegalAddress, ContactPhone,
                                        DirectorFullName, AccountNumber
                                 FROM CRS_Suppliers ORDER BY SupplierId" },

                new TableItem { DisplayName = "Состояния оргтехники",
                    SqlQuery = "SELECT StateId, StateName FROM CRS_EquipmentStates ORDER BY StateId" },

                new TableItem { DisplayName = "Типы операций",
                    SqlQuery = "SELECT OperationTypeId, TypeName FROM CRS_OperationTypes ORDER BY OperationTypeId" },

                new TableItem { DisplayName = "Пользователи",
                    SqlQuery = @"
                        SELECT u.UserId, u.Login, u.Password, u.FullName, u.Position,
                               ISNULL(d.DepartmentName, N'(нет)') AS Department,
                               u.Phone, u.IsActive,
                               STUFF((
                                   SELECT N', ' + r.RoleName
                                   FROM CRS_UserRoles ur
                                   JOIN CRS_Roles r ON r.RoleId = ur.RoleId
                                   WHERE ur.UserId = u.UserId
                                   FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'') AS Roles
                        FROM CRS_Users u
                        LEFT JOIN CRS_Departments d ON d.DepartmentId = u.DepartmentId
                        ORDER BY u.UserId" },

                new TableItem { DisplayName = "Модели",
                    SqlQuery = @"
                        SELECT m.ModelId, m.ModelName,
                               c.CategoryName AS Category,
                               ISNULL(mf.ManufacturerName, N'(нет)') AS Manufacturer,
                               m.Description
                        FROM CRS_Models m
                        LEFT JOIN CRS_Categories c    ON c.CategoryId      = m.CategoryId
                        LEFT JOIN CRS_Manufacturers mf ON mf.ManufacturerId = m.ManufacturerId
                        ORDER BY m.ModelId" },

                new TableItem { DisplayName = "Оргтехника",
                    SqlQuery = @"
                        SELECT e.EquipmentId, e.Name,
                               ISNULL(m.ModelName, N'(нет)')         AS Model,
                               e.SerialNumber,
                               ISNULL(c.CategoryName, N'(нет)')      AS Category,
                               ISNULL(mf.ManufacturerName, N'(нет)') AS Manufacturer,
                               ISNULL(s.CompanyName, N'(нет)')       AS Supplier,
                               e.ReceiptDate, e.WarrantyMonths, e.Price,
                               st.StateName                          AS State,
                               ISNULL(u.FullName, N'(нет)')          AS ResponsibleUser,
                               ISNULL(d.DepartmentName, N'(нет)')    AS Department,
                               e.Notes
                        FROM CRS_Equipment e
                        LEFT JOIN CRS_Models          m  ON m.ModelId         = e.ModelId
                        LEFT JOIN CRS_Categories      c  ON c.CategoryId      = e.CategoryId
                        LEFT JOIN CRS_Manufacturers   mf ON mf.ManufacturerId = e.ManufacturerId
                        LEFT JOIN CRS_Suppliers       s  ON s.SupplierId      = e.SupplierId
                        LEFT JOIN CRS_EquipmentStates st ON st.StateId        = e.StateId
                        LEFT JOIN CRS_Users           u  ON u.UserId          = e.CurrentUserId
                        LEFT JOIN CRS_Departments     d  ON d.DepartmentId    = e.CurrentDepartmentId
                        ORDER BY e.EquipmentId" },

                new TableItem { DisplayName = "Перемещения",
                    SqlQuery = @"
                        SELECT mv.MovementId, mv.MovementDate,
                               ot.TypeName                          AS OperationType,
                               e.Name                               AS Equipment,
                               e.SerialNumber,
                               ISNULL(uf.FullName, N'(нет)')        AS FromUser,
                               ISNULL(ut.FullName, N'(нет)')        AS ToUser,
                               ISNULL(df.DepartmentName, N'(нет)')  AS FromDepartment,
                               ISNULL(dt.DepartmentName, N'(нет)')  AS ToDepartment,
                               ISNULL(s.CompanyName, N'(нет)')      AS Supplier,
                               mv.Comment,
                               ISNULL(uc.FullName, N'(нет)')        AS CreatedBy
                        FROM CRS_Movements mv
                        JOIN CRS_OperationTypes ot ON ot.OperationTypeId = mv.OperationTypeId
                        JOIN CRS_Equipment      e  ON e.EquipmentId     = mv.EquipmentId
                        LEFT JOIN CRS_Users       uf ON uf.UserId        = mv.FromUserId
                        LEFT JOIN CRS_Users       ut ON ut.UserId        = mv.ToUserId
                        LEFT JOIN CRS_Departments df ON df.DepartmentId  = mv.FromDepartmentId
                        LEFT JOIN CRS_Departments dt ON dt.DepartmentId  = mv.ToDepartmentId
                        LEFT JOIN CRS_Suppliers   s  ON s.SupplierId     = mv.SupplierId
                        LEFT JOIN CRS_Users       uc ON uc.UserId        = mv.CreatedByUserId
                        ORDER BY mv.MovementId DESC" },

                new TableItem { DisplayName = "Журнал аудита",
                    SqlQuery = @"
                        SELECT a.AuditId, a.ActionDate,
                               ISNULL(u.FullName, N'(система)') AS UserName,
                               a.Action, a.EntityName, a.EntityId, a.Details
                        FROM CRS_AuditLog a
                        LEFT JOIN CRS_Users u ON u.UserId = a.UserId
                        ORDER BY a.AuditId DESC" }
            };
        }

        private void CmbTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSelectedTable();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadSelectedTable();
        }

        private void LoadSelectedTable()
        {
            if (!(CmbTables.SelectedItem is TableItem item))
                return;

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
                    TxtStatus.Text = $"Загружено: {DateTime.Now:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных:\n\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Ошибка загрузки";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(GridData.ItemsSource is DataView view))
                return;

            string filter = TxtFilter.Text.Trim();

            if (string.IsNullOrEmpty(filter))
            {
                view.RowFilter = string.Empty;
                return;
            }

            string safe = filter.Replace("'", "''");

            var parts = new System.Collections.Generic.List<string>();
            foreach (DataColumn col in view.Table.Columns)
            {
                parts.Add($"Convert([{col.ColumnName}], 'System.String') LIKE '%{safe}%'");
            }

            view.RowFilter = string.Join(" OR ", parts);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                "Выйти из учётной записи?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                        VALUES (@UserId, N'LOGOUT', N'CRS_Users', @EntityId, N'Выход из системы')", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@EntityId", _currentUserId.ToString());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch {}

            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}
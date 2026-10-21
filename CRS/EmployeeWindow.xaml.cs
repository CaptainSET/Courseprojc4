using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRS
{
    public partial class EmployeeWindow : Window
    {
        private readonly int _userId;
        private readonly string _userName;
        private readonly string _userRoles;

        private class SectionItem
        {
            public string DisplayName { get; set; }
            public string SqlQuery { get; set; }
        }

        private SectionItem[] _sections;

        public EmployeeWindow(int userId, string fullName, string roles)
        {
            InitializeComponent();
            _userId = userId;
            _userName = fullName;
            _userRoles = roles;

            TxtUserInfo.Text = $"Пользователь: {_userName}";
            TxtUserRoles.Text = $"Роли: {_userRoles}";
            TxtDepartment.Text = $"Подразделение: {GetDepartmentName()}";

            InitSections();
            CmbSections.ItemsSource = _sections;
            CmbSections.SelectedIndex = 0;
        }

        private string GetDepartmentName()
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(d.DepartmentName, N'(не задано)')
                        FROM CRS_Users u
                        LEFT JOIN CRS_Departments d ON d.DepartmentId = u.DepartmentId
                        WHERE u.UserId = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _userId);
                        return cmd.ExecuteScalar() as string ?? "(не задано)";
                    }
                }
            }
            catch { return "(не задано)"; }
        }

        private void InitSections()
        {
            _sections = new[]
            {
                new SectionItem { DisplayName = "Моя оргтехника",
                    SqlQuery = @"
                        SELECT e.EquipmentId, e.Name,
                               ISNULL(m.ModelName, N'(нет)') AS Model,
                               e.SerialNumber,
                               ISNULL(c.CategoryName, N'(нет)') AS Category,
                               ISNULL(mf.ManufacturerName, N'(нет)') AS Manufacturer,
                               st.StateName AS State,
                               e.ReceiptDate, e.WarrantyMonths, e.Price, e.Notes
                        FROM CRS_Equipment e
                        LEFT JOIN CRS_Models m ON m.ModelId = e.ModelId
                        LEFT JOIN CRS_Categories c ON c.CategoryId = e.CategoryId
                        LEFT JOIN CRS_Manufacturers mf ON mf.ManufacturerId = e.ManufacturerId
                        LEFT JOIN CRS_EquipmentStates st ON st.StateId = e.StateId
                        WHERE e.CurrentUserId = @UserId
                        ORDER BY e.Name" },
                new SectionItem { DisplayName = "Мои перемещения",
                    SqlQuery = @"
                        SELECT mv.MovementId, mv.MovementDate,
                               ot.TypeName AS OperationType,
                               e.Name AS Equipment, e.SerialNumber,
                               ISNULL(df.DepartmentName, N'(нет)') AS FromDepartment,
                               ISNULL(dt.DepartmentName, N'(нет)') AS ToDepartment,
                               mv.Comment
                        FROM CRS_Movements mv
                        JOIN CRS_OperationTypes ot ON ot.OperationTypeId = mv.OperationTypeId
                        JOIN CRS_Equipment e ON e.EquipmentId = mv.EquipmentId
                        LEFT JOIN CRS_Departments df ON df.DepartmentId = mv.FromDepartmentId
                        LEFT JOIN CRS_Departments dt ON dt.DepartmentId = mv.ToDepartmentId
                        WHERE mv.ToUserId = @UserId OR mv.FromUserId = @UserId
                        ORDER BY mv.MovementDate DESC" },
                new SectionItem { DisplayName = "Моя личная информация",
                    SqlQuery = @"
                        SELECT u.UserId, u.Login, u.FullName, u.Position,
                               ISNULL(d.DepartmentName, N'(нет)') AS Department,
                               u.Phone, u.IsActive
                        FROM CRS_Users u
                        LEFT JOIN CRS_Departments d ON d.DepartmentId = u.DepartmentId
                        WHERE u.UserId = @UserId" }
            };
        }

        private void CmbSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => LoadSelectedSection();

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => LoadSelectedSection();

        private void LoadSelectedSection()
        {
            if (!(CmbSections.SelectedItem is SectionItem item)) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var cmd = new SqlCommand(item.SqlQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", _userId);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        var table = new DataTable();
                        adapter.Fill(table);
                        GridData.ItemsSource = table.DefaultView;
                        TxtRowCount.Text = $"Строк: {table.Rows.Count}";
                        TxtSectionName.Text = item.DisplayName;
                        TxtStatus.Text = $"Загружено: {DateTime.Now:HH:mm:ss}";
                    }
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

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
            => new ProfileWindow(_userId) { Owner = this }.ShowDialog();

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            new LoginWindow().Show();
            Close();
        }
    }
}
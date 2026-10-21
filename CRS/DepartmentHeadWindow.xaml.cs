using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRS
{
    public partial class DepartmentHeadWindow : Window
    {
        private readonly int _userId;
        private readonly string _userName;
        private readonly string _userRoles;
        private int _departmentId;

        private class ReportItem
        {
            public string DisplayName { get; set; }
            public string SqlQuery { get; set; }
        }

        private ReportItem[] _reports;

        public DepartmentHeadWindow(int userId, string fullName, string roles)
        {
            InitializeComponent();
            _userId = userId;
            _userName = fullName;
            _userRoles = roles;

            TxtUserInfo.Text = $"Пользователь: {_userName}";
            TxtUserRoles.Text = $"Роли: {_userRoles}";

            _departmentId = GetDepartmentId();
            TxtDepartment.Text = $"Подразделение: {GetDepartmentName(_departmentId)}";

            InitReports();
            CmbReports.ItemsSource = _reports;
            CmbReports.SelectedIndex = 0;
        }

        private int GetDepartmentId()
        {
            using (var conn = new SqlConnection(App.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(DepartmentId, 0) FROM CRS_Users WHERE UserId = @UserId", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", _userId);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private string GetDepartmentName(int id)
        {
            if (id == 0) return "(не задано)";
            using (var conn = new SqlConnection(App.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT DepartmentName FROM CRS_Departments WHERE DepartmentId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    return cmd.ExecuteScalar() as string ?? "(не найдено)";
                }
            }
        }

        private void InitReports()
        {

            _reports = new[]
            {
                new ReportItem { DisplayName = "Оргтехника подразделения",
                    SqlQuery = @"
                        SELECT e.EquipmentId, e.Name,
                               ISNULL(m.ModelName, N'(нет)') AS Model,
                               e.SerialNumber,
                               ISNULL(c.CategoryName, N'(нет)') AS Category,
                               st.StateName AS State,
                               ISNULL(u.FullName, N'(нет)') AS ResponsibleUser,
                               e.ReceiptDate, e.Price
                        FROM CRS_Equipment e
                        LEFT JOIN CRS_Models m ON m.ModelId = e.ModelId
                        LEFT JOIN CRS_Categories c ON c.CategoryId = e.CategoryId
                        LEFT JOIN CRS_EquipmentStates st ON st.StateId = e.StateId
                        LEFT JOIN CRS_Users u ON u.UserId = e.CurrentUserId
                        WHERE e.CurrentDepartmentId = @DeptId
                        ORDER BY e.Name" },
                new ReportItem { DisplayName = "Сотрудники подразделения",
                    SqlQuery = @"
                        SELECT u.UserId, u.Login, u.FullName, u.Position, u.Phone, u.IsActive
                        FROM CRS_Users u
                        WHERE u.DepartmentId = @DeptId
                        ORDER BY u.FullName" },
                new ReportItem { DisplayName = "История перемещений подразделения",
                    SqlQuery = @"
                        SELECT mv.MovementId, mv.MovementDate,
                               ot.TypeName AS OperationType,
                               e.Name AS Equipment, e.SerialNumber,
                               ISNULL(uf.FullName, N'(нет)') AS FromUser,
                               ISNULL(ut.FullName, N'(нет)') AS ToUser,
                               ISNULL(df.DepartmentName, N'(нет)') AS FromDepartment,
                               ISNULL(dt.DepartmentName, N'(нет)') AS ToDepartment,
                               mv.Comment
                        FROM CRS_Movements mv
                        JOIN CRS_OperationTypes ot ON ot.OperationTypeId = mv.OperationTypeId
                        JOIN CRS_Equipment e ON e.EquipmentId = mv.EquipmentId
                        LEFT JOIN CRS_Users uf ON uf.UserId = mv.FromUserId
                        LEFT JOIN CRS_Users ut ON ut.UserId = mv.ToUserId
                        LEFT JOIN CRS_Departments df ON df.DepartmentId = mv.FromDepartmentId
                        LEFT JOIN CRS_Departments dt ON dt.DepartmentId = mv.ToDepartmentId
                        WHERE mv.FromDepartmentId = @DeptId OR mv.ToDepartmentId = @DeptId
                        ORDER BY mv.MovementDate DESC" },
                new ReportItem { DisplayName = "Отчёт о наличии по категориям",
                    SqlQuery = @"
                        SELECT c.CategoryName,
                               COUNT(*) AS EquipmentCount,
                               SUM(e.Price) AS TotalPrice
                        FROM CRS_Equipment e
                        LEFT JOIN CRS_Categories c ON c.CategoryId = e.CategoryId
                        WHERE e.CurrentDepartmentId = @DeptId
                          AND e.StateId <> (SELECT StateId FROM CRS_EquipmentStates WHERE StateName = N'Списано')
                        GROUP BY c.CategoryName
                        ORDER BY c.CategoryName" }
            };
        }

        private void CmbReports_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => LoadSelectedReport();

        private void BtnShowReport_Click(object sender, RoutedEventArgs e)
            => LoadSelectedReport();

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => LoadSelectedReport();

        private void LoadSelectedReport()
        {
            if (!(CmbReports.SelectedItem is ReportItem item)) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var cmd = new SqlCommand(item.SqlQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@DeptId", _departmentId);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        var table = new DataTable();
                        adapter.Fill(table);
                        GridData.ItemsSource = table.DefaultView;
                        TxtRowCount.Text = $"Строк: {table.Rows.Count}";
                        TxtReportName.Text = item.DisplayName;
                        TxtStatus.Text = $"Загружено: {DateTime.Now:HH:mm:ss}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки отчёта:\n" + ex.Message);
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
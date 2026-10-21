using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;

namespace CRS
{
    public partial class ProfileWindow : Window
    {
        private readonly int _userId;

        public ProfileWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadDepartments();
            LoadProfile();
        }

        private void LoadDepartments()
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(
                    "SELECT DepartmentId, DepartmentName FROM CRS_Departments ORDER BY DepartmentName",
                    conn))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    CmbDepartment.ItemsSource = table.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить подразделения:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadProfile()
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    const string sql = @"
                        SELECT Login, FullName, Position, DepartmentId, Phone
                        FROM CRS_Users WHERE UserId = @UserId";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _userId);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read()) { Close(); return; }

                            TxtLogin.Text = r.GetString(0);
                            TxtFullName.Text = r.GetString(1);
                            TxtPosition.Text = r.IsDBNull(2) ? "" : r.GetString(2);
                            CmbDepartment.SelectedValue = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
                            TxtPhone.Text = r.IsDBNull(4) ? "" : r.GetString(4);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки профиля:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string fullName = TxtFullName.Text.Trim();
            string position = TxtPosition.Text.Trim();
            string phone = TxtPhone.Text.Trim();
            int? deptId = CmbDepartment.SelectedValue as int?;
            string newPwd = TxtNewPassword.Password;
            string newPwd2 = TxtNewPassword2.Password;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TxtStatus.Text = "ФИО не может быть пустым.";
                return;
            }

            bool changePassword = !string.IsNullOrEmpty(newPwd);
            if (changePassword)
            {
                if (newPwd != newPwd2)
                {
                    TxtStatus.Text = "Пароли не совпадают.";
                    return;
                }
                if (newPwd.Length < 4)
                {
                    TxtStatus.Text = "Пароль должен быть не менее 4 символов.";
                    return;
                }
            }

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();

                    string sql = changePassword
                        ? @"UPDATE CRS_Users
                            SET FullName = @FullName, Position = @Position,
                                DepartmentId = @DepartmentId, Phone = @Phone,
                                Password = @Password
                            WHERE UserId = @UserId"
                        : @"UPDATE CRS_Users
                            SET FullName = @FullName, Position = @Position,
                                DepartmentId = @DepartmentId, Phone = @Phone
                            WHERE UserId = @UserId";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", fullName);
                        cmd.Parameters.AddWithValue("@Position",
                            string.IsNullOrWhiteSpace(position) ? (object)DBNull.Value : position);
                        cmd.Parameters.AddWithValue("@DepartmentId",
                            deptId.HasValue ? (object)deptId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Phone",
                            string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);
                        if (changePassword)
                            cmd.Parameters.AddWithValue("@Password", newPwd);
                        cmd.Parameters.AddWithValue("@UserId", _userId);
                        cmd.ExecuteNonQuery();
                    }

                    using (var audit = new SqlCommand(@"
                        INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                        VALUES (@UserId, N'UPDATE', N'CRS_Users', @EntityId, @Details)", conn))
                    {
                        audit.Parameters.AddWithValue("@UserId", _userId);
                        audit.Parameters.AddWithValue("@EntityId", _userId.ToString());
                        audit.Parameters.AddWithValue("@Details",
                            changePassword ? "Изменён профиль и пароль" : "Изменён профиль");
                        audit.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Профиль сохранён.", "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
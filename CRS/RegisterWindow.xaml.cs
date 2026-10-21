using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;

namespace CRS
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
            LoadDepartments();
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
                    CmbDepartment.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить подразделения:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string login = TxtLogin.Text.Trim();
            string pwd1 = TxtPassword.Password;
            string pwd2 = TxtPassword2.Password;
            string fullName = TxtFullName.Text.Trim();
            string position = TxtPosition.Text.Trim();
            string phone = TxtPhone.Text.Trim();
            int? departmentId = CmbDepartment.SelectedValue as int?;

            if (string.IsNullOrWhiteSpace(login) ||
                string.IsNullOrWhiteSpace(pwd1) ||
                string.IsNullOrWhiteSpace(fullName))
            {
                TxtStatus.Text = "Поля со звёздочкой (*) обязательны.";
                return;
            }

            if (pwd1 != pwd2)
            {
                TxtStatus.Text = "Пароли не совпадают.";
                return;
            }

            if (login.Length < 3)
            {
                TxtStatus.Text = "Логин должен содержать не менее 3 символов.";
                return;
            }

            if (pwd1.Length < 4)
            {
                TxtStatus.Text = "Пароль должен содержать не менее 4 символов.";
                return;
            }

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();

                    using (var check = new SqlCommand(
                        "SELECT COUNT(*) FROM CRS_Users WHERE Login = @Login", conn))
                    {
                        check.Parameters.AddWithValue("@Login", login);
                        int exists = (int)check.ExecuteScalar();
                        if (exists > 0)
                        {
                            TxtStatus.Text = "Такой логин уже занят.";
                            return;
                        }
                    }

                    int defaultRoleId = GetRoleId(conn, "Employee");

                    const string insertUser = @"
                        INSERT INTO CRS_Users
                            (Login, Password, FullName, Position, DepartmentId, Phone, IsActive)
                        OUTPUT INSERTED.UserId
                        VALUES
                            (@Login, @Password, @FullName, @Position, @DepartmentId, @Phone, 1)";

                    int newUserId;
                    using (var cmd = new SqlCommand(insertUser, conn))
                    {
                        cmd.Parameters.AddWithValue("@Login", login);
                        cmd.Parameters.AddWithValue("@Password", pwd1);
                        cmd.Parameters.AddWithValue("@FullName", fullName);
                        cmd.Parameters.AddWithValue("@Position",
                            string.IsNullOrWhiteSpace(position) ? (object)DBNull.Value : position);
                        cmd.Parameters.AddWithValue("@DepartmentId",
                            departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Phone",
                            string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);

                        newUserId = (int)cmd.ExecuteScalar();
                    }

                    using (var roleCmd = new SqlCommand(
                        "INSERT INTO CRS_UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)", conn))
                    {
                        roleCmd.Parameters.AddWithValue("@UserId", newUserId);
                        roleCmd.Parameters.AddWithValue("@RoleId", defaultRoleId);
                        roleCmd.ExecuteNonQuery();
                    }

                    using (var audit = new SqlCommand(@"
                        INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                        VALUES (@UserId, N'INSERT', N'CRS_Users', @EntityId, @Details)", conn))
                    {
                        audit.Parameters.AddWithValue("@UserId", newUserId);
                        audit.Parameters.AddWithValue("@EntityId", newUserId.ToString());
                        audit.Parameters.AddWithValue("@Details",
                            $"Регистрация нового пользователя: {login}");
                        audit.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Учётная запись успешно создана. Теперь можно войти.",
                                "Регистрация",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка регистрации:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetRoleId(SqlConnection conn, string roleName)
        {
            using (var cmd = new SqlCommand(
                "SELECT RoleId FROM CRS_Roles WHERE RoleName = @Name", conn))
            {
                cmd.Parameters.AddWithValue("@Name", roleName);
                object result = cmd.ExecuteScalar();
                if (result == null)
                    throw new InvalidOperationException(
                        $"Роль '{roleName}' не найдена в справочнике CRS_Roles.");
                return (int)result;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
using System;
using System.Data.SqlClient;
using System.Windows;

namespace CRS
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = TxtLogin.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                TxtStatus.Text = "Введите логин и пароль.";
                return;
            }

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();

                    const string sql = @"
                        SELECT u.UserId,
                               u.FullName,
                               ISNULL(
                                   STUFF((
                                       SELECT N', ' + r.RoleName
                                       FROM CRS_UserRoles ur
                                       JOIN CRS_Roles r ON r.RoleId = ur.RoleId
                                       WHERE ur.UserId = u.UserId
                                       FOR XML PATH(''), TYPE
                                   ).value('.', 'NVARCHAR(MAX)'), 1, 2, N''),
                                   N'(без роли)') AS Roles
                        FROM CRS_Users u
                        WHERE u.Login = @Login
                          AND u.Password = @Password
                          AND u.IsActive = 1";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Login", login);
                        cmd.Parameters.AddWithValue("@Password", password);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                TxtStatus.Text = "Неверный логин или пароль, либо учётная запись отключена.";
                                return;
                            }

                            int userId = reader.GetInt32(0);
                            string fullName = reader.GetString(1);
                            string roles = reader.GetString(2);
                            reader.Close();

                            LogAudit(conn, userId, "LOGIN", "CRS_Users", userId.ToString(),
                                     "Успешный вход в систему");

                            string primaryRole = GetPrimaryRole(conn, userId);

                            Window target = primaryRole switch
                            {
                                "Администратор" => new AdminWindow(userId, fullName, roles),
                                "Кладовщик" => new StorekeeperWindow(userId, fullName, roles),
                                "Руководитель" => new DepartmentHeadWindow(userId, fullName, roles),
                                _ => new EmployeeWindow(userId, fullName, roles)
                            };

                            target.Show();
                            Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            var reg = new RegisterWindow();
            reg.Owner = this;
            reg.ShowDialog();
        }

        private void LogAudit(SqlConnection conn, int userId, string action,
                              string entity, string entityId, string details)
        {
            const string sql = @"
                INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                VALUES (@UserId, @Action, @Entity, @EntityId, @Details)";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Action", action);
                cmd.Parameters.AddWithValue("@Entity", entity);
                cmd.Parameters.AddWithValue("@EntityId", (object)entityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Details", details);
                cmd.ExecuteNonQuery();
            }
        }

        private string GetPrimaryRole(SqlConnection conn, int userId)
        {
            const string sql = @"
        SELECT TOP 1 r.RoleName
        FROM CRS_UserRoles ur
        JOIN CRS_Roles r ON r.RoleId = ur.RoleId
        WHERE ur.UserId = @UserId
        ORDER BY CASE r.RoleName
                    WHEN N'Administrator'  THEN 1
                    WHEN N'Storekeeper'    THEN 2
                    WHEN N'DepartmentHead' THEN 3
                    WHEN N'Employee'       THEN 4
                    ELSE 5
                 END";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                object result = cmd.ExecuteScalar();
                return result as string ?? "Employee";
            }
        }
    }
}
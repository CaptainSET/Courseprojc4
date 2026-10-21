using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace CRS
{
    public partial class MovementWindow : Window
    {
        private readonly int _userId;

        public MovementWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadEquipment();
            LoadOperationTypes();
            LoadUsers();
            LoadDepartments();
            LoadSuppliers();
        }

        private void LoadEquipment()
        {
            using (var conn = new SqlConnection(App.ConnectionString))
            using (var adapter = new SqlDataAdapter(@"
                SELECT EquipmentId,
                       Name + N' [' + SerialNumber + N']' AS DisplayText
                FROM CRS_Equipment
                WHERE StateId <> (SELECT StateId FROM CRS_EquipmentStates WHERE StateName = N'Списано')
                ORDER BY Name", conn))
            {
                var t = new DataTable();
                adapter.Fill(t);
                CmbEquipment.ItemsSource = t.DefaultView;
            }
        }

        private void LoadOperationTypes()
        {
            using (var conn = new SqlConnection(App.ConnectionString))
            using (var adapter = new SqlDataAdapter(
                "SELECT OperationTypeId, TypeName FROM CRS_OperationTypes ORDER BY OperationTypeId", conn))
            {
                var t = new DataTable();
                adapter.Fill(t);
                CmbOperationType.ItemsSource = t.DefaultView;
            }
        }

        private void LoadUsers()
        {
            using (var conn = new SqlConnection(App.ConnectionString))
            using (var adapter = new SqlDataAdapter(
                "SELECT UserId, FullName FROM CRS_Users WHERE IsActive = 1 ORDER BY FullName", conn))
            {
                var t = new DataTable();
                adapter.Fill(t);
                CmbToUser.ItemsSource = t.DefaultView;
            }
        }

        private void LoadDepartments()
        {
            using (var conn = new SqlConnection(App.ConnectionString))
            using (var adapter = new SqlDataAdapter(
                "SELECT DepartmentId, DepartmentName FROM CRS_Departments ORDER BY DepartmentName", conn))
            {
                var t = new DataTable();
                adapter.Fill(t);
                CmbToDepartment.ItemsSource = t.DefaultView;
            }
        }

        private void LoadSuppliers()
        {
            using (var conn = new SqlConnection(App.ConnectionString))
            using (var adapter = new SqlDataAdapter(
                "SELECT SupplierId, CompanyName FROM CRS_Suppliers ORDER BY CompanyName", conn))
            {
                var t = new DataTable();
                adapter.Fill(t);
                CmbSupplier.ItemsSource = t.DefaultView;
            }
        }

        private void CmbOperationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEquipment.SelectedValue == null)
            { TxtStatus.Text = "Выберите оргтехнику."; return; }
            if (CmbOperationType.SelectedValue == null)
            { TxtStatus.Text = "Выберите тип операции."; return; }

            int equipmentId = (int)CmbEquipment.SelectedValue;
            int opTypeId = (int)CmbOperationType.SelectedValue;
            int? toUserId = CmbToUser.SelectedValue as int?;
            int? toDeptId = CmbToDepartment.SelectedValue as int?;
            int? supplierId = CmbSupplier.SelectedValue as int?;
            string comment = TxtComment.Text.Trim();

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO CRS_Movements
                                    (EquipmentId, OperationTypeId, MovementDate,
                                     FromUserId, ToUserId, FromDepartmentId, ToDepartmentId,
                                     SupplierId, Comment, CreatedByUserId)
                                OUTPUT INSERTED.MovementId
                                SELECT @EquipmentId, @OpTypeId, SYSUTCDATETIME(),
                                       e.CurrentUserId, @ToUserId,
                                       e.CurrentDepartmentId, @ToDepartmentId,
                                       @SupplierId, @Comment, @CreatedBy
                                FROM CRS_Equipment e WHERE e.EquipmentId = @EquipmentId", conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                                cmd.Parameters.AddWithValue("@OpTypeId", opTypeId);
                                cmd.Parameters.AddWithValue("@ToUserId",
                                    toUserId.HasValue ? (object)toUserId.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@ToDepartmentId",
                                    toDeptId.HasValue ? (object)toDeptId.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@SupplierId",
                                    supplierId.HasValue ? (object)supplierId.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@Comment",
                                    string.IsNullOrWhiteSpace(comment) ? (object)DBNull.Value : comment);
                                cmd.Parameters.AddWithValue("@CreatedBy", _userId);
                                cmd.ExecuteScalar();
                            }

                            string updateSql;
                            switch (opTypeId)
                            {
                                case 1: 
                                    updateSql = @"UPDATE CRS_Equipment
                                                  SET StateId = (SELECT StateId FROM CRS_EquipmentStates WHERE StateName = N'На складе'),
                                                      CurrentUserId = NULL,
                                                      CurrentDepartmentId = @ToDepartmentId,
                                                      UpdatedAt = SYSUTCDATETIME()
                                                  WHERE EquipmentId = @EquipmentId";
                                    break;
                                case 2: 
                                    updateSql = @"UPDATE CRS_Equipment
                                                  SET StateId = (SELECT StateId FROM CRS_EquipmentStates WHERE StateName = N'В работе'),
                                                      CurrentUserId = @ToUserId,
                                                      CurrentDepartmentId = @ToDepartmentId,
                                                      UpdatedAt = SYSUTCDATETIME()
                                                  WHERE EquipmentId = @EquipmentId";
                                    break;
                                case 3:
                                    updateSql = @"UPDATE CRS_Equipment
                                                  SET StateId = (SELECT StateId FROM CRS_EquipmentStates WHERE StateName = N'Списано'),
                                                      CurrentUserId = NULL,
                                                      CurrentDepartmentId = NULL,
                                                      UpdatedAt = SYSUTCDATETIME()
                                                  WHERE EquipmentId = @EquipmentId";
                                    break;
                                default: 
                                    updateSql = @"UPDATE CRS_Equipment
                                                  SET StateId = (SELECT StateId FROM CRS_EquipmentStates WHERE StateName = N'На складе'),
                                                      CurrentUserId = NULL,
                                                      CurrentDepartmentId = @ToDepartmentId,
                                                      UpdatedAt = SYSUTCDATETIME()
                                                  WHERE EquipmentId = @EquipmentId";
                                    break;
                            }

                            using (var cmd = new SqlCommand(updateSql, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                                cmd.Parameters.AddWithValue("@ToUserId",
                                    toUserId.HasValue ? (object)toUserId.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@ToDepartmentId",
                                    toDeptId.HasValue ? (object)toDeptId.Value : DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new SqlCommand(@"
                                INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                                VALUES (@UserId, N'INSERT', N'CRS_Movements', @EntityId, @Details)",
                                conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@UserId", _userId);
                                cmd.Parameters.AddWithValue("@EntityId", equipmentId.ToString());
                                cmd.Parameters.AddWithValue("@Details",
                                    $"Оформлено перемещение (тип {opTypeId}) для техники {equipmentId}");
                                cmd.ExecuteNonQuery();
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

                MessageBox.Show("Перемещение оформлено.", "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Ошибка: " + ex.Message;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
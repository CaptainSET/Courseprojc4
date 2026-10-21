using CRS;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;

namespace CRS
{
    public partial class ManufacturerCategoriesWindow : Window
    {
        public ManufacturerCategoriesWindow()
        {
            InitializeComponent();
            LoadLookups();
            LoadLinks();
        }

        private void LoadLookups()
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(
                    "SELECT ManufacturerId, ManufacturerName FROM CRS_Manufacturers ORDER BY ManufacturerName", conn))
                {
                    var t = new DataTable();
                    adapter.Fill(t);
                    CmbManufacturer.ItemsSource = t.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить производителей:\n" + ex.Message);
            }

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(
                    "SELECT CategoryId, CategoryName FROM CRS_Categories ORDER BY CategoryName", conn))
                {
                    var t = new DataTable();
                    adapter.Fill(t);
                    CmbCategory.ItemsSource = t.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить категории:\n" + ex.Message);
            }
        }

        private void LoadLinks()
        {
            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                using (var adapter = new SqlDataAdapter(@"
                    SELECT mc.ManufacturerId, mf.ManufacturerName,
                           mc.CategoryId,     c.CategoryName
                    FROM CRS_ManufacturerCategories mc
                    JOIN CRS_Manufacturers mf ON mf.ManufacturerId = mc.ManufacturerId
                    JOIN CRS_Categories    c  ON c.CategoryId      = mc.CategoryId
                    ORDER BY mf.ManufacturerName, c.CategoryName", conn))
                {
                    var t = new DataTable();
                    adapter.Fill(t);
                    GridLinks.ItemsSource = t.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить связи:\n" + ex.Message);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => LoadLinks();

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (CmbManufacturer.SelectedValue == null || CmbCategory.SelectedValue == null)
            {
                TxtStatus.Text = "Выберите производителя и категорию.";
                return;
            }

            int manId = (int)CmbManufacturer.SelectedValue;
            int catId = (int)CmbCategory.SelectedValue;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM CRS_ManufacturerCategories
                                       WHERE ManufacturerId = @ManId AND CategoryId = @CatId)
                            INSERT INTO CRS_ManufacturerCategories (ManufacturerId, CategoryId)
                            VALUES (@ManId, @CatId)", conn))
                    {
                        cmd.Parameters.AddWithValue("@ManId", manId);
                        cmd.Parameters.AddWithValue("@CatId", catId);
                        cmd.ExecuteNonQuery();
                    }

                    using (var audit = new SqlCommand(@"
                        INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                        VALUES (NULL, N'INSERT', N'CRS_ManufacturerCategories',
                                @EntityId, @Details)", conn))
                    {
                        audit.Parameters.AddWithValue("@EntityId", $"{manId}-{catId}");
                        audit.Parameters.AddWithValue("@Details",
                            $"Добавлена связь производитель={manId}, категория={catId}");
                        audit.ExecuteNonQuery();
                    }
                }

                TxtStatus.Text = "Связь добавлена.";
                LoadLinks();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Ошибка: " + ex.Message;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!(GridLinks.SelectedItem is DataRowView row))
            {
                TxtStatus.Text = "Выберите связь для удаления.";
                return;
            }

            int manId = Convert.ToInt32(row["ManufacturerId"]);
            int catId = Convert.ToInt32(row["CategoryId"]);

            if (MessageBox.Show(
                    $"Удалить связь производитель={row["ManufacturerName"]}, " +
                    $"категория={row["CategoryName"]}?",
                    "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new SqlConnection(App.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        DELETE FROM CRS_ManufacturerCategories
                        WHERE ManufacturerId = @ManId AND CategoryId = @CatId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ManId", manId);
                        cmd.Parameters.AddWithValue("@CatId", catId);
                        cmd.ExecuteNonQuery();
                    }

                    using (var audit = new SqlCommand(@"
                        INSERT INTO CRS_AuditLog (UserId, Action, EntityName, EntityId, Details)
                        VALUES (NULL, N'DELETE', N'CRS_ManufacturerCategories',
                                @EntityId, @Details)", conn))
                    {
                        audit.Parameters.AddWithValue("@EntityId", $"{manId}-{catId}");
                        audit.Parameters.AddWithValue("@Details",
                            $"Удалена связь производитель={manId}, категория={catId}");
                        audit.ExecuteNonQuery();
                    }
                }

                TxtStatus.Text = "Связь удалена.";
                LoadLinks();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Ошибка: " + ex.Message;
            }
        }
    }
}
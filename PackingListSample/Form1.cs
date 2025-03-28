﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using PackingListSample;
using Microsoft.Reporting.WinForms;
using System.Data.SqlClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using Org.BouncyCastle.Asn1.Esf;

namespace PackingListSample
{
    public partial class MainForm : Form
    {
        private List<PackingListItem> packingListItems = new List<PackingListItem>();
        private BindingSource bindingSource = new BindingSource();
        int cartonIndex = 1;
        public MainForm()
        {
            InitializeComponent();
            InitializeDataGridView();
            
        }

        private void InitializeDataGridView()
        {
            dgvOutput.Columns.Clear();
            dgvOutput.AllowUserToAddRows = false;
            dgvOutput.AllowUserToDeleteRows = true;
            dgvOutput.MultiSelect = false;
            dgvOutput.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvOutput.ReadOnly = false;
            bindingSource.DataSource = packingListItems;
            dgvOutput.DataSource = bindingSource;
            dateTimePicker1.MinDate = DateTime.Today.AddDays(-3);
        }
        // Import XML functionality
        private void btnImportXML_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "XML files (*.xml)|*.xml";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ImportXMLFile(openFileDialog.FileName);
                }
            }
        }

        private void ImportXMLFile(string filePath)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<PackingListItem>));
                using (FileStream fs = new FileStream(filePath, FileMode.Open))
                {
                    packingListItems = (List<PackingListItem>)serializer.Deserialize(fs);
                    bindingSource.DataSource = packingListItems;
                    dgvOutput.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing XML: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddItem_Click(object sender, EventArgs e)
        {
            try
            {

                
                LoadDataTemp();
                CheckValidInput();
                DataTable dtFinish = new DataTable();
                List<(string Item, int Quantity)> inputItems = new List<(string, int)>();

                foreach (DataGridViewRow row in dgvInput.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        string item = row.Cells["Item"].Value?.ToString();
                        int quantity;
                        if (item != null && int.TryParse(row.Cells["Quantity"].Value?.ToString(), out quantity))
                        {
                            if (item.Length > 6 && quantity != 0)
                            {
                                DataTable dtItemResult = GetDataFromItem(item, quantity);
                                if (dtFinish.Columns.Count == 0)
                                {
                                    dtFinish = dtItemResult.Clone();
                                }
                                foreach (DataRow itemRow in dtItemResult.Rows)
                                {
                                    dtFinish.ImportRow(itemRow);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Check the item again!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }

                dgvOutput.DataSource = dtFinish;
                DataSet1 dataset = new DataSet1();
                dataset = LoadData(dtFinish);
                // Gán dữ liệu cho ReportViewer
                reportViewer1.LocalReport.DataSources.Clear();
                reportViewer1.LocalReport.ReportEmbeddedResource = "PackingListSample.PackingListReport.rdlc";
                reportViewer1.LocalReport.DataSources.Add(new ReportDataSource("PackingListItem", dataset.Tables["PackingListItem"]));
                reportViewer1.LocalReport.DataSources.Add(new ReportDataSource("PackingListData", dataset.PackingListData.AsEnumerable()));
                reportViewer1.ZoomMode = ZoomMode.PageWidth; // Hiển thị full width trang
                reportViewer1.SetDisplayMode(DisplayMode.PrintLayout); // Chế độ xem in ấn
                reportViewer1.DocumentMapCollapsed = false; // Mở rộng document map nếu có
                reportViewer1.ShowPageNavigationControls = true; // Hiển thị điều khiển chuyển trang
                reportViewer1.ShowToolBar = true;
                reportViewer1.RefreshReport();
                cartonIndex = 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DataTable GetDataFromItem(string item, int quantity)
        {
            string batch = item.Substring(0, 6);
            string colorno = item.Substring(6);
            string strconnect = @"Data Source=172.19.2.101;Initial Catalog=GZ;User ID=sa;Password=Admin@123;";
            DataTable dt_boxGroup = new DataTable();
            string query_boxGroup = string.Format(@"SELECT  [Matkl],boxgroup ,[WH_Matkl].[BoxCode] ,[NetWeight],[CoreWeight],BoxWeight FROM [WH_Matkl],[WH_BOX] 
                                     where [WH_Matkl].BoxCode=[WH_BOX].BoxCode 
                                     and Matkl = '{0}'
                                     order by BoxGroup ,Matkl", batch);
            using (SqlConnection connection = new SqlConnection(strconnect))
            {
                SqlCommand cmd = new SqlCommand(query_boxGroup, connection);
                cmd.Connection.Open();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt_boxGroup);
                cmd.Connection.Close();
            }

            string boxgroup = dt_boxGroup.Rows[0].Field<string>("boxgroup");
            DataTable dt_WH_Box_Carton = new DataTable();
            string query_WH_Box_Carton = string.Format(@"SELECT *  FROM [WH_Box_Carton] 
                                                            where  boxgroup = '{0}'
                                                            order by BoxGroup,Max_Box desc ", boxgroup);
            using (SqlConnection connection = new SqlConnection(strconnect))
            {
                SqlCommand cmd = new SqlCommand(query_WH_Box_Carton, connection);
                cmd.Connection.Open();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt_WH_Box_Carton);
                cmd.Connection.Close();
            }
            DataTable dt_WH_Carton = new DataTable();
            string query_WH_Carton = string.Format(@" SELECT *  FROM [WH_Carton]");
            using (SqlConnection connection = new SqlConnection(strconnect))
            {
                SqlCommand cmd = new SqlCommand(query_WH_Carton, connection);
                cmd.Connection.Open();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt_WH_Carton);
                cmd.Connection.Close();
            }

            DataTable dt_Daima = new DataTable();
            string query_Daima = string.Format(@"select DAI, YSBH, MA, TEX, BRAND
                                                from DAIMA
                                                where dai = '{0}'", batch);
            using (SqlConnection connection = new SqlConnection(strconnect))
            {
                SqlCommand cmd = new SqlCommand(query_Daima, connection);
                cmd.Connection.Open();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt_Daima);
                cmd.Connection.Close();
            }
            DataTable dtOptimize = OptimizeBoxPacking(dt_WH_Box_Carton, dt_WH_Carton, dt_boxGroup, dt_Daima, quantity, boxgroup, colorno);
            DataTable dtcaculator = IndexCarton(dtOptimize);
            return dtcaculator;
        }
        void LoadDataTemp()
        {
            dgvInput.Columns.Clear();
            DataTable dtInput = new DataTable();
            dtInput.Columns.Add("Item", typeof(string));
            dtInput.Columns.Add("Quantity", typeof(int));

            Random rand = new Random();

            for (int i = 0; i < 10; i++)
            {
                string item = "PBPM5G" + i.ToString("D3");
                int quantity = rand.Next(100, 999); 

                dtInput.Rows.Add(item, quantity);
            }
            dgvInput.DataSource = dtInput;
        }
        private DataTable IndexCarton(DataTable dtFinish)
        {
            DataTable dtResult = dtFinish.Clone();

            foreach (DataRow row in dtFinish.Rows)
            {
                int cartonCount = Convert.ToInt32(row["Carton"]);
                string cartonRange = cartonCount == 1 ? cartonIndex.ToString()
                                                      : $"{cartonIndex} - {cartonIndex + cartonCount - 1}";

                DataRow newRow = dtResult.NewRow();
                newRow.ItemArray = row.ItemArray.Clone() as object[];
                newRow["Carton"] = cartonRange;
                dtResult.Rows.Add(newRow);

                cartonIndex += cartonCount;
            }

            return dtResult;
        }

        public static double CalculateCBM(int totalQty, int qty, string sizeCTN)
        {
            if (string.IsNullOrWhiteSpace(sizeCTN) || totalQty <= 0 || qty <= 0)
                return 0;
            string[] dimensions = sizeCTN.Split('*');
            if (dimensions.Length != 3)
                return 0;

            if (!int.TryParse(dimensions[0], out int length) ||
                !int.TryParse(dimensions[1], out int width) ||
                !int.TryParse(dimensions[2], out int height))
                return 0;
            int totalCartons = totalQty / qty;
            double cbmPerCarton = (length * width * height) / 1000000.0;
            return cbmPerCarton * totalCartons;
        }

        public DataTable OptimizeBoxPacking(DataTable dt_WH_Box_Carton, DataTable dt_WH_Carton, DataTable dt_ProductData, DataTable dt_Daima, int Quantity, string boxgroup, string colorno)
        {
            DataTable resultTable = new DataTable();
            resultTable.Columns.Add("Carton", typeof(string));
            resultTable.Columns.Add("PONoAndIONo", typeof(string));
            resultTable.Columns.Add("Description", typeof(string));
            resultTable.Columns.Add("ColorNo", typeof(string));
            resultTable.Columns.Add("QtyCone", typeof(int));
            resultTable.Columns.Add("TotalQtyCone", typeof(int));
            resultTable.Columns.Add("GrossWeightPerCTN", typeof(double));
            resultTable.Columns.Add("NetWeightPerCTN", typeof(double));
            resultTable.Columns.Add("SizeCTN", typeof(string));
            resultTable.Columns.Add("MemoNo", typeof(string));
            resultTable.Columns.Add("Measurement", typeof(double));
            var productRow = dt_ProductData.AsEnumerable().FirstOrDefault(r => r.Field<string>("boxgroup") == boxgroup);
            if (productRow == null)
                return resultTable;
            dt_WH_Box_Carton = dt_WH_Box_Carton
                .AsEnumerable()
                .GroupBy(r => new { BoxGroup = r["BoxGroup"].ToString(), Carton = Convert.ToInt32(r["Carton"]) })
                .Select(g =>
                {
                    var firstRow = g.First();
                    firstRow["Min_Box"] = g.Min(r => Convert.ToInt32(r["Min_Box"]));
                    firstRow["Max_Box"] = g.Max(r => Convert.ToInt32(r["Max_Box"]));
                    return firstRow;
                })
                .CopyToDataTable();


            double netWeight = Convert.ToDouble(productRow["NetWeight"]);
            double coreWeight = Convert.ToDouble(productRow["CoreWeight"]);
            double boxWeight = Convert.ToDouble(productRow["BoxWeight"]);

            var cartonDetails = dt_WH_Carton.AsEnumerable().ToDictionary(
                row => Convert.ToInt32(row["Carton"]),
                row => row["Size"].ToString()
            );

            var cartonWeightMapping = dt_WH_Carton.AsEnumerable().ToDictionary(
                row => Convert.ToInt32(row["Carton"]),
                row => Convert.ToDouble(row["CartonWeight"])
            );
            var boxList = new List<(int MinBox, int MaxBox, int Carton)>();

            foreach (DataRow row in dt_WH_Box_Carton.Rows)
            {
                int minBox = Convert.ToInt32(row["Min_Box"]) * 10;
                int maxBox = Convert.ToInt32(row["Max_Box"]) * 10;
                int carton = Convert.ToInt32(row["Carton"]);
                boxList.Add((minBox, maxBox, carton));
            }
            var sortedBoxes = boxList.OrderByDescending(b => b.MaxBox).ToList();

            int remaining = Quantity;

            string dai = dt_Daima.Rows[0]["DAI"].ToString();
            string ysbh = dt_Daima.Rows[0]["YSBH"].ToString();
            string ma = dt_Daima.Rows[0]["MA"].ToString();
            string tex = dt_Daima.Rows[0]["TEX"].ToString();
            string brand = dt_Daima.Rows[0]["BRAND"].ToString();
            bool flag = false;
            while (remaining > 0)
            {
                bool packed = false;

                for (int i = 0; i < sortedBoxes.Count; i++)
                {
                    var box = sortedBoxes[i];
                    int maxBoxes = remaining / box.MaxBox;
                    int maxBox = box.MaxBox;
                    int carton = box.Carton;
                    string cartonSize = cartonDetails.ContainsKey(carton) ? cartonDetails[carton] : "Unknown";
                    double cartonWeight = cartonWeightMapping.ContainsKey(carton) ? cartonWeightMapping[carton] : 0;
                    double measurement = 0;
                    int usedQuantity = 0;
                    double nwPerCarton = 0;
                    double gwPerCarton = 0; 
                    string description = FormatDescription(brand, dai, tex, ysbh, ma);
                    if (maxBoxes > 0 && flag == false)
                    {
                        usedQuantity = maxBoxes * maxBox;
                        nwPerCarton = (usedQuantity / maxBoxes) * netWeight;
                        gwPerCarton = nwPerCarton + (12 * boxWeight) + ((usedQuantity / maxBoxes) * coreWeight) + cartonWeight;
                        measurement = CalculateCBM(usedQuantity, maxBox, cartonSize.TrimEnd());
                        resultTable.Rows.Add(
                        $"{maxBoxes}",
                        "Sample",
                        description,
                        colorno,
                        maxBox,
                        usedQuantity,
                        Math.Round(gwPerCarton, 3),
                        Math.Round(nwPerCarton, 3),
                        cartonSize,
                        "",
                        measurement
                        );
                        int packQty = maxBoxes * box.MaxBox;
                        remaining -= packQty;
                        packed = true;
                        flag = true;
                        break;
                    }
                    if (remaining > 0 && box.MaxBox >= remaining && box.MinBox <= remaining)
                    {
                        usedQuantity = remaining;
                        nwPerCarton = usedQuantity * netWeight;
                        gwPerCarton = nwPerCarton + (12 * boxWeight) + (usedQuantity * coreWeight) + cartonWeight;
                        measurement = CalculateCBM(usedQuantity, usedQuantity, cartonSize.TrimEnd());
                        resultTable.Rows.Add(
                        $"{1}",
                        "Sample",
                        description,
                        colorno,
                        usedQuantity,
                        usedQuantity,
                        Math.Round(gwPerCarton, 3),
                        Math.Round(nwPerCarton, 3),
                        cartonSize,
                        ""
                        );
                        remaining = 0;
                        packed = true;
                        flag = true;
                        break;
                    }
                }
                if (!packed)
                {
                    int rounded = (int)Math.Ceiling((double)remaining / 10) * 10;
                    var sortedBoxes2 = boxList.OrderBy(box => box.MaxBox).ToList();
                    var largerBox = sortedBoxes2.FirstOrDefault(b => b.MinBox >= rounded);
                    if (largerBox.Carton != 0)
                    {
                        int carton = largerBox.Carton;
                        string cartonSize = cartonDetails.ContainsKey(carton) ? cartonDetails[carton] : "Unknown";
                        double cartonWeight = cartonWeightMapping.ContainsKey(carton) ? cartonWeightMapping[carton] : 0;

                        int usedQuantity = remaining;
                        double nwPerCarton = usedQuantity * netWeight;
                        double gwPerCarton = nwPerCarton + (12 * boxWeight) + (usedQuantity * coreWeight) + cartonWeight;
                        string description = FormatDescription(brand, dai, tex, ysbh, ma);
                        double measurement = CalculateCBM(usedQuantity, usedQuantity, cartonSize.TrimEnd());
                        resultTable.Rows.Add(
                            $"{1}",
                            "Sample",
                            description,
                            colorno,
                            usedQuantity,
                            usedQuantity,
                            Math.Round(gwPerCarton, 3),
                            Math.Round(nwPerCarton, 3),
                            cartonSize,
                            "",
                            measurement
                        );
                        remaining = 0;
                    }
                    else
                    {
                        // If no larger box is found, we can't pack the remaining items
                        break;
                    }
                }
            }

            return resultTable;
        }

        
        /// <summary>
        /// format description
        /// </summary>
        /// <param name="brand"></param>
        /// <param name="dai"></param>
        /// <param name="tex"></param>
        /// <param name="ysbh"></param>
        /// <param name="ma"></param>
        /// <returns></returns>
        public static string FormatDescription(string brand, string dai, string tex, string ysbh, string ma)
        {
            brand = brand.TrimEnd();
            dai = dai.TrimEnd();
            tex = tex.TrimEnd();
            ysbh = ysbh.TrimEnd();
            ma = ma.TrimEnd();
            string secondLine = $"Tex{tex}".PadRight(8) +
                                ysbh.PadRight(5) +
                                ma;
            secondLine = secondLine.TrimEnd();
            int secondLineLength = secondLine.Length;

            if (brand.Length > secondLineLength)
            {
                secondLine = secondLine.PadRight(brand.Length);
            }
            int extraSpaces = secondLineLength - brand.Length - dai.Length;
            int leftPadding = extraSpaces / 2;
            int rightPadding = extraSpaces - leftPadding;
            string firstLine = new string(' ', leftPadding) +
                               brand +
                               new string(' ', rightPadding) +
                               dai;

            return firstLine.TrimEnd() + "\n" + secondLine;
        }
        private void ClearInputFields()
        {
        }
        private void CheckValidInput()
        {
            if (string.IsNullOrWhiteSpace(txtCusName.Text))
            {
                throw new Exception("Tên khách hàng không được để trống.");
            }
            if (string.IsNullOrWhiteSpace(txtCusNo.Text))
            {
                throw new Exception("Mã khách hàng không được để trống.");
            }
            //if (string.IsNullOrWhiteSpace(txtShipAdd.Text))
            //{
            //    throw new Exception("Địa chỉ giao hàng không được để trống.");
            //}
            if (dateTimePicker1.Value == DateTime.MinValue)
            {
                throw new Exception("Vui lòng chọn ngày hợp lệ.");
            }
            if (dgvInput.Rows.Count == 0 || dgvInput.Rows.Cast<DataGridViewRow>().All(row => row.IsNewRow))
            {
                throw new Exception("Bảng dữ liệu không được để trống.");
            }
        }

        private DataSet1 LoadData(DataTable dtFinish)
        {
            DataSet1 dataset = new DataSet1();
            dataset.PackingListItem.Clear(); // Clear existing data
            dataset.PackingListItem.Merge(dtFinish); // Or ImportRow
            dataset.PackingListData.Rows.Add(
                txtCusName.Text.TrimEnd(),
                txtCusNo.Text.TrimEnd(),
                dateTimePicker1.Value,
                txtShipAdd.Text.TrimEnd(),
                ""
            ); ;
            return dataset;
        }

        // Modify the existing btnGeneratePDF_Click method
        private void btnGeneratePDF_Click(object sender, EventArgs e)
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Generate filename with current date and time
                string today = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string pdfFileName = $"PackingList_{today}.pdf";

                // Combine the application directory with the filename
                string pdfFilePath = Path.Combine(appDirectory, pdfFileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating PDF: {ex.Message}", "PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // XML Export
        private void btnExportXML_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "XML files (*.xml)|*.xml";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<PackingListItem>));
                    using (FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        serializer.Serialize(fs, packingListItems);
                    }
                    MessageBox.Show("XML exported successfully!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // Delete selected row
        private void btnDeleteItem_Click(object sender, EventArgs e)
        {
            if (dgvOutput.SelectedRows.Count > 0)
            {
                int selectedIndex = dgvOutput.SelectedRows[0].Index;
                packingListItems.RemoveAt(selectedIndex);
                bindingSource.ResetBindings(false);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

            this.reportViewer1.RefreshReport();
        }
    }
    

    
}

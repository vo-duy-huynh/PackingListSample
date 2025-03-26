using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PackingListSample;
using Microsoft.Reporting.WinForms;
using System.Data.SqlClient;

namespace PackingListSample
{
    public partial class MainForm : Form
    {
        private List<PackingListItem> packingListItems = new List<PackingListItem>();
        private BindingSource bindingSource = new BindingSource();
        private int index = 0;
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
                DataTable dtWithRowID = AddRowIDColumn(dtFinish);
                DataSet1 dataset = new DataSet1();
                dataset = LoadData(dtWithRowID);
                // Gán dữ liệu cho ReportViewer
                reportViewer1.LocalReport.DataSources.Clear();
                reportViewer1.LocalReport.ReportEmbeddedResource = "PackingListSample.PackingListReport.rdlc";
                reportViewer1.LocalReport.DataSources.Add(new ReportDataSource("PackingListItem", dataset.Tables["PackingListItem"]));
                reportViewer1.LocalReport.DataSources.Add(new ReportDataSource("PackingListData", dataset.PackingListData.AsEnumerable()));
                reportViewer1.ZoomMode = ZoomMode.PageWidth;
                reportViewer1.RefreshReport();
                index = 0;
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
                //cmd.ExecuteNonQuery();
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
                //cmd.ExecuteNonQuery();
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
                //cmd.ExecuteNonQuery();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt_Daima);
                cmd.Connection.Close();
            }
            DataTable dtcaculator = CaculatorBox(dt_WH_Box_Carton, dt_WH_Carton, dt_boxGroup, dt_Daima, quantity, boxgroup, colorno);
            return dtcaculator;
        }
        //thêm 1 cột để phân trang
        public DataTable AddRowIDColumn(DataTable dt)
        {
            DataTable newTable = dt.Copy();
            newTable.Columns.Add("RowID", typeof(int)); 

            for (int i = 0; i < newTable.Rows.Count; i++)
            {
                newTable.Rows[i]["RowID"] = i + 1;
            }

            return newTable;
        }
        void LoadDataTemp()
        {
            dgvInput.Columns.Clear();
            DataTable dtInput = new DataTable();
            dtInput.Columns.Add("Item", typeof(string));
            dtInput.Columns.Add("Quantity", typeof(int));

            Random rand = new Random();

            for (int i = 0; i < 20; i++)
            {
                string item = "LELM5G" + i.ToString("D3"); // Định dạng 'iii' thành 3 chữ số (000, 001, ..., 019)
                int quantity = rand.Next(100, 1000); // Random 3 chữ số (100 - 999)

                dtInput.Rows.Add(item, quantity);
            }
            dgvInput.DataSource = dtInput;
        }

        private DataTable CaculatorBox(DataTable dt_WH_Box_Carton, DataTable dt_WH_Carton, DataTable dt_ProductData, DataTable dt_Daima, int Quantity, string boxgroup, string colorno)
        {
            DataTable resultTable = new DataTable();
            resultTable.Columns.Add("Carton", typeof(System.String));
            resultTable.Columns.Add("PONoAndIONo", typeof(System.String));
            resultTable.Columns.Add("Description", typeof(System.String));
            resultTable.Columns.Add("ColorNo", typeof(System.String));
            resultTable.Columns.Add("QtyCone", typeof(System.Int32));
            resultTable.Columns.Add("TotalQtyCone", typeof(System.Int32));
            resultTable.Columns.Add("GrossWeightPerCTN", typeof(System.Double));
            resultTable.Columns.Add("NetWeightPerCTN", typeof(System.Double));
            resultTable.Columns.Add("SizeCTN", typeof(System.String));
            resultTable.Columns.Add("MemoNo", typeof(System.String));

            var productRow = dt_ProductData.AsEnumerable()
                .FirstOrDefault(r => r.Field<string>("boxgroup") == boxgroup);
            if (productRow == null)
                return resultTable;

            double netWeight = Convert.ToDouble(productRow["NetWeight"]);
            double coreWeight = Convert.ToDouble(productRow["CoreWeight"]);
            double boxWeight = Convert.ToDouble(productRow["BoxWeight"]);

            var cartonDetails = dt_WH_Carton.AsEnumerable()
                .ToDictionary(
                    row => Convert.ToInt32(row["Carton"]),
                    row => row["Size"].ToString()
                );
            var cartonWeightMapping = dt_WH_Carton.AsEnumerable()
                .ToDictionary(
                    row => Convert.ToInt32(row["Carton"]),
                    row => Convert.ToDouble(row["CartonWeight"])
                );
            var filteredRows = dt_WH_Box_Carton.AsEnumerable()
                .Where(row => row.Field<string>("BoxGroup") == boxgroup)
                .OrderByDescending(row => Convert.ToInt32(row["Max_Box"]))
                .ToList();

            int remainingQuantity = Quantity;
            int currentCartonIndex = 0;
            string dai = dt_Daima.Rows[0]["DAI"].ToString();
            string ysbh = dt_Daima.Rows[0]["YSBH"].ToString();
            string ma = dt_Daima.Rows[0]["MA"].ToString();
            string tex = dt_Daima.Rows[0]["TEX"].ToString();
            string brand = dt_Daima.Rows[0]["BRAND"].ToString();
            while (remainingQuantity > 0 && currentCartonIndex < filteredRows.Count)
            {
                var row = filteredRows[currentCartonIndex];
                int carton = Convert.ToInt32(row["Carton"]);
                int maxBox = Convert.ToInt32(row["Max_Box"]) * 10;
                int minBox = Convert.ToInt32(row["Min_Box"]) * 10;
                int partialCartonQuantity = 0;

                // Case 1: Remaining quantity is between min and max
                if (remainingQuantity >= minBox && remainingQuantity < maxBox)
                {
                    double cartonWeight = cartonWeightMapping.ContainsKey(carton)? cartonWeightMapping[carton]: 0;
                    partialCartonQuantity = remainingQuantity % minBox;
                    int fullCartons = remainingQuantity / minBox;
                    int usedQuantity = fullCartons * minBox;
                    double nwPerCarton = minBox * netWeight;
                    double gwPerCarton = nwPerCarton + (12 * boxWeight) + (minBox * coreWeight) + cartonWeight;

                    int startRange = index + 1;
                    int endRange = startRange + fullCartons - 1;
                    resultTable.Rows.Add(
                        $"{startRange}-{endRange}",
                        "Sample",
                        string.Format( $"{brand} {dai} Tex{tex} {ysbh} {ma}"),
                        colorno,
                        maxBox,
                        usedQuantity,
                        Math.Round(gwPerCarton, 3),
                        Math.Round(nwPerCarton, 3),
                        cartonDetails.ContainsKey(carton) ? cartonDetails[carton] : "Unknown",
                        ""
                    );

                    remainingQuantity -= usedQuantity;
                    index = endRange;
                }
                // Case 2: Remaining quantity is larger than max
                else if (remainingQuantity >= maxBox)
                {
                    double cartonWeight = cartonWeightMapping.ContainsKey(carton) ? cartonWeightMapping[carton] : 0;
                    int fullCartons = remainingQuantity / maxBox;
                    int usedQuantity = fullCartons * maxBox;
                    double nwPerCarton = maxBox * netWeight;
                    double gwPerCarton = nwPerCarton + (12 * boxWeight) + (maxBox * coreWeight) + cartonWeight;

                    int startRange = index + 1;
                    int endRange = startRange + fullCartons - 1;

                    resultTable.Rows.Add(
                        $"{startRange}-{endRange}",
                        "Sample",
                        string.Format($"{brand} {dai} Tex{tex} {ysbh} {ma}"),
                        colorno,
                        maxBox,
                        usedQuantity,
                        Math.Round(gwPerCarton, 2),
                        Math.Round(nwPerCarton, 2),
                        cartonDetails.ContainsKey(carton) ? cartonDetails[carton] : "Unknown",
                        ""
                    );

                    remainingQuantity -= usedQuantity;
                    index = endRange;
                }

                // Handle last incomplete carton
                if (partialCartonQuantity > 0 && partialCartonQuantity < maxBox)
                {
                    currentCartonIndex++;
                    continue;
                }

                currentCartonIndex++;
            }

            return resultTable;
        }


        private void ClearInputFields()
        {
        }

        
        private void GeneratePDFPreview(string filePath)
        {
            DataSet1 dataset = new DataSet1();
            // Gán dữ liệu cho ReportViewer
            reportViewer1.LocalReport.DataSources.Clear();
            reportViewer1.LocalReport.ReportEmbeddedResource = "PackingListSample.PackingListReport.rdlc";
            reportViewer1.LocalReport.DataSources.Add(new ReportDataSource("PackingListItem", dataset.Tables["PackingListItem"]));
            reportViewer1.LocalReport.DataSources.Add(new ReportDataSource("PackingListData", dataset.PackingListData.AsEnumerable()));

            reportViewer1.RefreshReport();
        }
        private DataSet1 LoadData(DataTable dtFinish)
        {
            DataSet1 dataset = new DataSet1();
            dataset.PackingListItem.Clear(); // Clear existing data
            dataset.PackingListItem.Merge(dtFinish); // Or ImportRow
            dataset.PackingListData.Rows.Add(
                "LEADING STAR (CAMBODIA) GARMENT CO.",
                "71PER111 /71LES11 []",
                DateTime.Parse("03/11/2025"),
                "Lot 447, Street 193DT, Phun PreySor Lech, Sangkat Prey Sor, Khan Dongkor",
                "116235",
                5000, // TotalQuantity
                42,   // TotalPackings
                693.00,  // NetWeight
                877.74,  // GrossWeight
                2.80     // Measurement
            );
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

                GeneratePDFPreview(pdfFilePath);
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

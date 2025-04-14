using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PackingListSample
{
    public partial class Form2 : Form
    {
        private DataTable dtItem;
        private DataTable dtHead;

        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CreateSampleData(out dtHead, out dtItem);
            var valid = GetValidCtnnos(dtHead, dtItem);

            // Bước 2: Gom toàn bộ phần dư lại thành 1 kiện nếu nhỏ hơn content
            HandleRemainingItems_GomHetThanhMot(ref dtHead, ref dtItem, valid);

            // Bước 3: Gom thông minh các item có qty = 10
            HandleRemainingItems_GomTheoNhom10(ref dtHead, ref dtItem, valid);
            int a = 0;
        }

        public static List<(int packno, int ctnno)> GetValidCtnnos(DataTable dtHead, DataTable dtItem)
        {
            var validCtnnos = new List<(int packno, int ctnno)>();

            // Nhóm item theo packno + ctnno
            var grouped = dtItem.AsEnumerable()
                .GroupBy(row => new
                {
                    PackNo = row.Field<int>("packno"),
                    CtnNo = row.Field<int>("ctnno")
                });

            foreach (var group in grouped)
            {
                int packno = group.Key.PackNo;
                int ctnno = group.Key.CtnNo;
                var qtyList = group.Select(r => r.Field<int>("qty")).ToList();

                bool all10 = qtyList.All(q => q == 10);
                int sumQty = qtyList.Sum();

                // Tìm content từ head
                var headRow = dtHead.AsEnumerable()
                    .FirstOrDefault(r => r.Field<int>("packno") == packno && r.Field<int>("ctnno") == ctnno);

                if (headRow == null)
                    continue;

                int content = headRow.Field<int>("content");

                if (all10 || sumQty == content)
                {
                    validCtnnos.Add((packno, ctnno));
                }
            }

            return validCtnnos;
        }
        public static void HandleRemainingItems_GomHetThanhMot(
    ref DataTable dtHead,
    ref DataTable dtItem,
    List<(int packno, int ctnno)> validCtnnos)
        {
            var validSet = new HashSet<(int, int)>(validCtnnos);

            // Lấy item chưa hợp lệ
            var remainItems = dtItem.AsEnumerable()
                .Where(r => !validSet.Contains((r.Field<int>("packno"), r.Field<int>("ctnno"))))
                .ToList();

            if (remainItems.Count == 0) return;

            int newPackno = remainItems[0].Field<int>("packno");
            int totalQty = remainItems.Sum(r => r.Field<int>("qty"));

            // ✅ Lấy danh sách các content còn lại chưa xử lý
            var remainingHeadContents = dtHead.AsEnumerable()
                .Where(r => !validSet.Contains((r.Field<int>("packno"), r.Field<int>("ctnno"))) &&
                            r.Field<int>("packno") == newPackno)
                .Select(r => r.Field<int>("content"))
                .Distinct()
                .OrderByDescending(c => c) // Ưu tiên content lớn nhất có thể
                .ToList();

            // ✅ Tìm content phù hợp nhất
            int? chosenContent = remainingHeadContents.FirstOrDefault(c => c >= totalQty);

            if (chosenContent == null || chosenContent == 0)
            {
                // ❌ Không có content nào phù hợp để gộp
                return;
            }

            int contentValue = chosenContent.Value;

            // ⬇ Gộp: lấy tất cả ctnno sẽ bị gộp
            var oldCtnnos = remainItems.Select(r => r.Field<int>("ctnno")).Distinct().ToList();
            int newCtnno = oldCtnnos.Min(); // ✅ dùng ctnno nhỏ nhất

            // ❌ Xóa các dòng head cũ liên quan
            var rowsToDelete = dtHead.AsEnumerable()
                .Where(r => oldCtnnos.Contains(r.Field<int>("ctnno")) &&
                            r.Field<int>("packno") == newPackno)
                .ToList();

            foreach (var row in rowsToDelete)
                dtHead.Rows.Remove(row);

            // ✅ Thêm dòng head mới
            var newHeadRow = dtHead.NewRow();
            newHeadRow["packno"] = newPackno;
            newHeadRow["ctnno"] = newCtnno;
            newHeadRow["qty"] = totalQty;
            newHeadRow["content"] = contentValue;
            dtHead.Rows.Add(newHeadRow);

            // ✅ Gộp lại item vào newCtnno, cập nhật boxno và chỉ dùng đủ số lượng
            var itemsToUse = remainItems.OrderBy(r => r.Field<int>("ctnno")).ThenBy(r => r.Field<int>("boxno")).ToList();

            int currentQty = 0;
            int boxIndex = 1;

            foreach (var item in itemsToUse)
            {
                if (currentQty >= contentValue) break;

                int itemQty = item.Field<int>("qty");
                int qtyToUse = Math.Min(itemQty, contentValue - currentQty);

                item["ctnno"] = newCtnno;
                item["boxno"] = boxIndex++;

                if (itemQty > qtyToUse)
                {
                    // Nếu item lớn hơn cần dùng, tách dòng
                    var newRow = dtItem.NewRow();
                    newRow.ItemArray = item.ItemArray.Clone() as object[];

                    newRow["qty"] = qtyToUse;
                    item["qty"] = itemQty - qtyToUse;

                    dtItem.Rows.Add(newRow);

                    // Gán lại cho dòng mới đúng ctnno và boxno
                    newRow["ctnno"] = newCtnno;
                    newRow["boxno"] = boxIndex - 1;
                }

                currentQty += qtyToUse;
            }
        }



        public static void HandleRemainingItems_GomTheoNhom10(
    ref DataTable dtHead,
    ref DataTable dtItem,
    List<(int packno, int ctnno)> validCtnnos,
    List<int> allowedContents = null)
        {
            if (allowedContents == null)
                allowedContents = new List<int> { 40, 60 };

            var validSet = new HashSet<(int, int)>(validCtnnos);

            // Lấy các item qty = 10 chưa xử lý
            var remainItems = dtItem.AsEnumerable()
                .Where(r => !validSet.Contains((r.Field<int>("packno"), r.Field<int>("ctnno"))) &&
                            r.Field<int>("qty") == 10)
                .OrderBy(r => r.Field<int>("ctnno"))
                .ThenBy(r => r.Field<int>("boxno"))
                .ToList();

            if (remainItems.Count == 0) return;

            int newPackno = remainItems[0].Field<int>("packno");
            // Đảm bảo có cột "IsProcessed" trong dtItem
            if (!dtItem.Columns.Contains("IsProcessed"))
            {
                dtItem.Columns.Add("IsProcessed", typeof(bool));
            }

            // Đặt giá trị ban đầu là false cho tất cả các dòng trong dtItem
            foreach (DataRow row in dtItem.Rows)
            {
                row["IsProcessed"] = false;
            }

            int boxCount = 0;
            List<DataRow> buffer = new List<DataRow>();

            // Sử dụng while để xử lý các remainItems và gom vào content
            while (remainItems.Any(r => !r.Field<bool>("IsProcessed")))
            {
                // Lấy dòng chưa được xử lý đầu tiên
                var row = remainItems.First(r => !r.Field<bool>("IsProcessed"));

                // Tiến hành xử lý dòng đó
                buffer.Add(row);
                boxCount += row.Field<int>("qty");

                // Tìm content phù hợp với boxCount
                int targetContent = allowedContents.LastOrDefault(c => c >= boxCount);

                // Nếu đã gom đủ content
                if (targetContent != 0 && boxCount == targetContent)
                {
                    // Lấy ctnno nhỏ nhất từ nhóm
                    int newCtnno = buffer.Select(r => r.Field<int>("ctnno")).Min();

                    // Xoá các dòng head cũ liên quan
                    var ctnnoToDelete = buffer.Select(r => r.Field<int>("ctnno")).Distinct().ToList();
                    var rowsToDelete = dtHead.AsEnumerable()
                        .Where(r => ctnnoToDelete.Contains(r.Field<int>("ctnno")) &&
                                    r.Field<int>("packno") == newPackno)
                        .ToList();

                    foreach (var r in rowsToDelete)
                        dtHead.Rows.Remove(r);

                    // Cập nhật lại item: gán ctnno và boxno
                    for (int i = 0; i < buffer.Count; i++)
                    {
                        buffer[i]["ctnno"] = newCtnno;
                        buffer[i]["boxno"] = i + 1;
                    }

                    // Thêm dòng head mới
                    var newRow = dtHead.NewRow();
                    newRow["packno"] = newPackno;
                    newRow["ctnno"] = newCtnno;
                    newRow["qty"] = boxCount;
                    newRow["content"] = boxCount;
                    dtHead.Rows.Add(newRow);

                    // Đánh dấu các dòng đã xử lý
                    foreach (var processedRow in buffer)
                    {
                        processedRow["IsProcessed"] = true;
                    }

                    // Reset lại buffer và boxCount
                    buffer.Clear();
                    boxCount = 0;
                }
                else if (boxCount > targetContent)
                {
                    // Trường hợp phần dư không đủ để gom, kiểm tra phần dư
                    int remainingQty = boxCount;

                    // Tạo dòng head cho phần dư này nếu có
                    int newCtnno = buffer.Select(r => r.Field<int>("ctnno")).Min();
                    var newRow = dtHead.NewRow();
                    newRow["packno"] = newPackno;
                    newRow["ctnno"] = newCtnno;
                    newRow["qty"] = remainingQty;
                    newRow["content"] = targetContent; // sử dụng content lớn nhất đã chọn
                    dtHead.Rows.Add(newRow);

                    // Gộp tiếp các remainItems còn lại có qty nhỏ hơn 10 vào nhóm này
                    var remainToFill = dtItem.AsEnumerable()
                        .Where(r => !validSet.Contains((r.Field<int>("packno"), r.Field<int>("ctnno"))) &&
                                    r.Field<int>("qty") < 10 && !r.Field<bool>("IsProcessed"))
                        .OrderBy(r => r.Field<int>("ctnno"))
                        .ThenBy(r => r.Field<int>("boxno"))
                        .ToList();

                    foreach (var item in remainToFill)
                    {
                        if (remainingQty < targetContent)
                        {
                            int itemQty = item.Field<int>("qty");
                            int qtyToFill = Math.Min(itemQty, targetContent - remainingQty);

                            remainingQty += qtyToFill;

                            // Cập nhật lại ctnno và boxno
                            item["ctnno"] = newCtnno;
                            item["boxno"] = newCtnno;  // Sử dụng boxno = ctnno cho đơn giản, hoặc bạn có thể làm lại boxno
                            if (itemQty > qtyToFill)
                            {
                                // Nếu item còn dư, tách thành dòng mới
                                var newItem = dtItem.NewRow();
                                newItem.ItemArray = item.ItemArray.Clone() as object[];
                                newItem["qty"] = itemQty - qtyToFill;

                                dtItem.Rows.Add(newItem);
                            }

                            // Đánh dấu item này đã được xử lý
                            item["IsProcessed"] = true;
                        }
                    }

                    // Sau khi gộp tiếp, kiểm tra lại xem liệu có đủ để gom hết 1 content nữa không
                    if (remainingQty >= targetContent)
                    {
                        // Tạo head mới cho content đã đủ
                        newRow["qty"] = remainingQty;
                        newRow["content"] = remainingQty;
                    }

                    // Reset
                    buffer.Clear();
                    boxCount = 0;
                }
            }
            // Nếu vẫn còn các dòng chưa được xử lý, gom phần còn lại để đủ 1 ctnno
            var remainingUnprocessed = dtItem.AsEnumerable()
            .Where(r => !validSet.Contains((r.Field<int>("packno"), r.Field<int>("ctnno"))) &&
                        !r.Field<bool>("IsProcessed"))
            .OrderBy(r => r.Field<int>("ctnno"))
            .ThenBy(r => r.Field<int>("boxno"))
            .ToList();

            if (remainingUnprocessed.Any())
            {
                int finalBoxCount = 0;
                List<DataRow> finalBuffer = new List<DataRow>();
                foreach (var row in remainingUnprocessed)
                {
                    finalBuffer.Add(row);
                    finalBoxCount += row.Field<int>("qty");
                }
                while (finalBoxCount > 0)
                {
                    int targetContent = allowedContents.LastOrDefault(c => c <= finalBoxCount);
                    int newCtnno = finalBuffer.Select(r => r.Field<int>("ctnno")).Min();
                    // Nếu không đủ để gom thành content lớn nhất, phải gom phần còn lại thành một ctnno mới
                    if (targetContent == 0)
                    {
                        var finalNewRow = dtHead.NewRow();
                        finalNewRow["packno"] = newPackno;
                        finalNewRow["ctnno"] = newCtnno;
                        finalNewRow["qty"] = targetContent;
                        finalNewRow["content"] = targetContent;
                        dtHead.Rows.Add(finalNewRow);
                        targetContent = finalBoxCount; // Gom hết phần còn lại vào 1 ctnno
                    }


                    int remainingQty = targetContent;
                    List<DataRow> itemsToUpdate = new List<DataRow>();

                    foreach (var row in finalBuffer)
                    {
                        if (remainingQty <= 0) break;

                        int itemQty = row.Field<int>("qty");
                        if (itemQty <= remainingQty)
                        {
                            row["ctnno"] = newCtnno;
                            row["boxno"] = finalBuffer.IndexOf(row) + 1; // Cập nhật boxno
                            row["IsProcessed"] = true;

                            remainingQty -= itemQty;
                            itemsToUpdate.Add(row);
                        }
                        else
                        {
                            // Nếu item quá lớn, chia thành nhiều dòng
                            var newItem = dtItem.NewRow();
                            newItem.ItemArray = row.ItemArray.Clone() as object[];
                            newItem["qty"] = remainingQty;
                            row["qty"] = itemQty - remainingQty;

                            newItem["ctnno"] = newCtnno;
                            newItem["boxno"] = finalBuffer.IndexOf(row) + 1; // Cập nhật boxno cho item mới
                            newItem["IsProcessed"] = true;
                            dtItem.Rows.Add(newItem);

                            remainingQty = 0;
                        }
                    }

                    // Xoá các item đã xử lý xong khỏi buffer
                    finalBuffer = finalBuffer.Except(itemsToUpdate).ToList();

                    // Cập nhật lại tổng số lượng còn lại
                    finalBoxCount -= targetContent;

                    // Nếu còn phần dư nhỏ hơn content, sẽ tạo thêm các ctnno nhỏ hơn cho phần dư
                    if (finalBoxCount > 0)
                    {
                        // Xử lý dư nếu còn
                        if (finalBoxCount > 0)
                        {
                            targetContent = finalBoxCount; // Gom tất cả phần dư vào 1 ctnno
                        }
                    }
                }
            }

        }



        public static void CreateSampleData(out DataTable dt_packing_head, out DataTable dt_packing_item)
        {
            // Tạo bảng dt_packing_head
            dt_packing_head = new DataTable();
            dt_packing_head.Columns.Add("packno", typeof(int));
            dt_packing_head.Columns.Add("ctnno", typeof(int));
            dt_packing_head.Columns.Add("qty", typeof(int));
            dt_packing_head.Columns.Add("content", typeof(int));

            dt_packing_head.Rows.Add(1, 1, 80, 80);
            dt_packing_head.Rows.Add(1, 2, 60, 60);
            dt_packing_head.Rows.Add(1, 3, 40, 40);
            dt_packing_head.Rows.Add(1, 4, 25, 40);
            dt_packing_head.Rows.Add(1, 5, 45, 60);

            // Tạo bảng dt_packing_item
            dt_packing_item = new DataTable();
            dt_packing_item.Columns.Add("packno", typeof(int));
            dt_packing_item.Columns.Add("ctnno", typeof(int));
            dt_packing_item.Columns.Add("boxno", typeof(int));
            dt_packing_item.Columns.Add("qty", typeof(int));

            // Dữ liệu mẫu giống bảng trong ảnh
            int[,] itemData = new int[,]
            {
            {1, 1, 1, 10},
            {1, 1, 2, 10},
            {1, 1, 3, 10},
            {1, 1, 4, 10},
            {1, 1, 5, 10},
            {1, 1, 6, 10},
            {1, 1, 7, 10},
            {1, 1, 8, 10},

            {1, 2, 1, 10},
            {1, 2, 2, 10},
            {1, 2, 3, 10},
            {1, 2, 4, 10},
            {1, 2, 5, 10},
            {1, 2, 6, 10},

            {1, 3, 1, 10},
            {1, 3, 2, 10},
            {1, 3, 3, 10},
            {1, 3, 4, 10},

            {1, 4, 1, 10},
            {1, 4, 2, 10},
            {1, 4, 3, 5},

            {1, 5, 1, 10},
            {1, 5, 2, 10},
            {1, 5, 3, 10},
            {1, 5, 4, 10},
            {1, 5, 5, 5},
            };

            for (int i = 0; i < itemData.GetLength(0); i++)
            {
                dt_packing_item.Rows.Add(
                    itemData[i, 0],
                    itemData[i, 1],
                    itemData[i, 2],
                    itemData[i, 3]
                );
            }
        }
    }
}

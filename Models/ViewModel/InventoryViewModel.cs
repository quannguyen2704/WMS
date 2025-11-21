namespace WMS.Models
{
    public class InventoryViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductTypeName { get; set; }

        public string Unit { get; set; }

        public decimal ProductPrice { get; set; }       // 💰 Đơn giá
        public decimal InitialQuantity { get; set; }    // Ban đầu
        public decimal TotalImported { get; set; }      // Tổng nhập
        public decimal TotalExported { get; set; }      // Tổng xuất
        public decimal Stock { get; set; }              // Tồn kho

        // 💰 Tổng tiền nhập / xuất / tồn kho
        public decimal TotalImportValue { get; set; }
        public decimal TotalExportValue { get; set; }
        public decimal TotalStockValue { get; set; }
    }
}
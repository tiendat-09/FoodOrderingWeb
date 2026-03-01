namespace FoodOrderingWeb.Models
{
    public class CartItem
    {
        public Guid CartItemId { get; set; }
        public int FoodId { get; set; }
        public string FoodName { get; set; }
        public string ImageUrl { get; set; }

        // Giá đơn vị (Đã bao gồm tiền Size)
        public decimal Price { get; set; }

        public int Quantity { get; set; }
        public string Size { get; set; }
        public string Note { get; set; }
        public List<string> Toppings { get; set; } = new List<string>();

        // 🔥 SỬA DÒNG NÀY: Tự động tính, không bao giờ sai 🔥
        public decimal TotalPrice => Price * Quantity;
    }
}
using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FoodOrderingWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly FoodOrderingDbContext _context;

        public HomeController(FoodOrderingDbContext context)
        {
            _context = context;
        }

        // 1. TRANG CHỦ: Hiện món ăn của quán ĐANG MỞ VÀ CÒN MÓN (IsActive == true)
        public IActionResult Index(string searchString)
        {
            var foods = _context.Foods
                .Include(f => f.Store)
                .Where(f => f.Store.IsActive == true && f.Store.IsOpen == true && f.IsActive == true) // 🔥 Đã thêm kiểm tra f.IsActive == true
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                foods = foods.Where(s => s.FoodName.Contains(searchString));
                ViewBag.SearchString = searchString;
            }
            else
            {
                foods = foods.OrderBy(r => Guid.NewGuid());
            }

            return View(foods.ToList());
        }

        // 2. CHI TIẾT MÓN ĂN: (Gộp từ FoodController sang)
        public IActionResult FoodDetails(int id)
        {
            var food = _context.Foods
                .Include(f => f.Store)
                .FirstOrDefault(m => m.FoodId == id);

            if (food == null) return NotFound();

            // Sẽ gọi file Views/Home/FoodDetails.cshtml
            return View(food);
        }

        // 3. API LẤY THÔNG TIN MÓN (Dùng cho Modal ở trang chủ)
        [HttpGet]
        public IActionResult GetFoodInfo(int id)
        {
            var food = _context.Foods.Include(f => f.Store).FirstOrDefault(f => f.FoodId == id);
            if (food == null) return Json(new { success = false });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = food.FoodId,
                    name = food.FoodName,
                    price = food.Price,
                    image = food.ImageUrl,
                    description = food.Description ?? "Chưa có mô tả.",
                    storeName = food.Store?.StoreName ?? "Giao nhanh",
                    isOpen = food.Store?.IsOpen ?? false
                }
            });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult SubmitStoreRating(int orderId, int rating, string review)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order != null)
            {
                order.StoreRating = rating;
                order.StoreReview = review;
                _context.SaveChanges(); // Lệnh này mới quyết định lưu vào Database nè!
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
        }

        [HttpPost]
        public IActionResult SubmitDriverRating(int orderId, int rating, string review)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order != null)
            {
                order.DriverRating = rating;
                order.DriverReview = review;
                _context.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
        }

        [HttpPost]
        public IActionResult CancelOrder(int orderId, string reason)
        {
            try
            {
                // 1. Lấy user hiện tại (ví dụ)
                // int userId = int.Parse(HttpContext.Session.GetString("UserId"));

                // 2. Tìm đơn hàng
                var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId && o.Status == "pending");

                if (order == null)
                {
                    return Json(new { success = false, message = "Đơn hàng không tồn tại hoặc quán đã nhận đơn (không thể hủy)." });
                }

                // 3. Cập nhật trạng thái và lý do
                order.Status = "cancelled";
                order.CancelReason = "Khách hủy: " + reason;

                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}
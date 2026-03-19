using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace FoodOrderingWeb.Controllers
{
    public class StoreManagerController : Controller
    {
        private readonly FoodOrderingDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StoreManagerController(FoodOrderingDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private int? GetCurrentStoreId()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return null;

            var store = _context.Stores.FirstOrDefault(s => s.OwnerId == userId);

            if (store != null)
            {
                // Cập nhật lại Avatar vào Session mỗi khi truy cập để Sidebar luôn đúng ảnh mới nhất
                string avatarUrl = string.IsNullOrEmpty(store.ImageUrl) ? "/images/stores/default_store.jpg" : store.ImageUrl;
                avatarUrl += "?v=" + DateTime.Now.Ticks;
                HttpContext.Session.SetString("Avatar", avatarUrl);
            }
            return store?.StoreId;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("Role") != "StoreOwner") return RedirectToAction("Index", "Home");
            int? storeId = GetCurrentStoreId();
            if (storeId == null) return RedirectToAction("RegisterStore", "Account");

            var storeOrders = _context.Orders.Where(o => o.StoreId == storeId).ToList();

            // Chỉ tính trên các đơn hàng đã hoàn thành
            var completedOrders = storeOrders
                .Where(o => !string.IsNullOrEmpty(o.Status) && o.Status.ToLower() == "completed")
                .ToList();

            ViewBag.TotalOrders = storeOrders.Count;

            ViewBag.TotalRevenue = completedOrders
                .Sum(o => o.TotalAmount - o.ShippingFee);

            // 2. Doanh thu Hôm nay
            ViewBag.TodayRevenue = completedOrders
                .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Date == DateTime.Today)
                .Sum(o => o.TotalAmount - o.ShippingFee);

            // 3. Doanh thu Tháng này
            ViewBag.MonthRevenue = completedOrders
                .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Month == DateTime.Now.Month && o.OrderDate.Value.Year == DateTime.Now.Year)
                .Sum(o => o.TotalAmount - o.ShippingFee);
            // ----------------------------

            var ratings = _context.Orders.Where(o => o.StoreId == storeId && o.StoreRating > 0);
            int totalReviews = ratings.Count();
            double averageRating = totalReviews > 0 ? ratings.Average(o => (double)o.StoreRating) : 5.0;

            ViewBag.TotalReviews = totalReviews;
            ViewBag.AverageRating = Math.Round(averageRating, 1);

            var recentOrders = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Driver)
                .Where(o => o.StoreId == storeId)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();

            return View(recentOrders);
        }

        public IActionResult Menu()
        {
            if (HttpContext.Session.GetString("Role") != "StoreOwner") return RedirectToAction("Index", "Home");
            int? storeId = GetCurrentStoreId();
            if (storeId == null) return RedirectToAction("RegisterStore", "Account");

            return View(_context.Foods.Where(f => f.StoreId == storeId).OrderByDescending(f => f.FoodId).ToList());
        }

        public IActionResult Orders()
        {
            if (HttpContext.Session.GetString("Role") != "StoreOwner") return RedirectToAction("Index", "Home");
            int? storeId = GetCurrentStoreId();
            if (storeId == null) return RedirectToAction("RegisterStore", "Account");

            var allOrders = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Driver)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Food)
                .Where(o => o.StoreId == storeId)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(allOrders);
        }

        [HttpGet]
        public IActionResult Info()
        {
            if (HttpContext.Session.GetString("Role") != "StoreOwner") return RedirectToAction("Index", "Home");
            int? storeId = GetCurrentStoreId();
            if (storeId == null) return RedirectToAction("RegisterStore", "Account");
            return View(_context.Stores.Find(storeId));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInfo(Store model, IFormFile imageFile)
        {
            try
            {
                var store = await _context.Stores.FindAsync(model.StoreId);
                if (store == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy cửa hàng!" });
                }

                store.StoreName = model.StoreName;
                store.PhoneNumber = model.PhoneNumber;
                store.Address = model.Address;
                store.Description = model.Description;
                store.OperatingDays = model.OperatingDays;
                store.IsActive = model.IsActive;
                store.Latitude = model.Latitude;
                store.Longitude = model.Longitude;

                if (imageFile != null && imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "stores");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    var filePath = Path.Combine(uploadPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    store.ImageUrl = "/images/stores/" + fileName;
                }

                _context.Stores.Update(store);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddFood([FromForm] Food food, IFormFile ImageFile)
        {
            int? storeId = GetCurrentStoreId();
            if (storeId == null) return Json(new { success = false, message = "Không tìm thấy quán!" });

            if (string.IsNullOrWhiteSpace(food.FoodName)) return Json(new { success = false, message = "Tên món không hợp lệ!" });
            if (food.Price <= 0) return Json(new { success = false, message = "Giá món phải lớn hơn 0!" });

            try
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    if (ImageFile.Length > 5 * 1024 * 1024) return Json(new { success = false, message = "File ảnh không vượt quá 5MB!" });

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension)) return Json(new { success = false, message = "Chỉ chấp nhận JPG, PNG, GIF, WEBP!" });

                    string fileName = Guid.NewGuid() + fileExtension;
                    string uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images/foods");

                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    string filePath = Path.Combine(uploadPath, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await ImageFile.CopyToAsync(stream);

                    food.ImageUrl = "/images/foods/" + fileName;
                }
                else
                {
                    food.ImageUrl = "/images/default_food.jpg";
                }

                food.StoreId = storeId.Value;
                // Mặc định món mới thêm vào sẽ hiển thị luôn (Còn món)
                food.IsActive = true;

                _context.Foods.Add(food);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditFood([FromForm] Food model, IFormFile ImageFile)
        {
            var food = _context.Foods.Find(model.FoodId);
            if (food == null) return Json(new { success = false, message = "Không tìm thấy món!" });
            if (string.IsNullOrWhiteSpace(model.FoodName)) return Json(new { success = false, message = "Tên món không được trống!" });
            if (model.Price <= 0) return Json(new { success = false, message = "Giá món phải lớn hơn 0!" });

            try
            {
                food.FoodName = model.FoodName;
                food.Price = model.Price;
                food.Description = model.Description;

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    if (ImageFile.Length > 5 * 1024 * 1024) return Json(new { success = false, message = "File ảnh không vượt quá 5MB!" });

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension)) return Json(new { success = false, message = "Chỉ chấp nhận JPG, PNG, GIF, WEBP!" });

                    string fileName = Guid.NewGuid() + fileExtension;
                    string uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images/foods");

                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    string filePath = Path.Combine(uploadPath, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await ImageFile.CopyToAsync(stream);

                    food.ImageUrl = "/images/foods/" + fileName;
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteFood(int id)
        {
            try
            {
                var food = _context.Foods.Find(id);
                if (food == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy món ăn." });
                }

                // kiểm món xem ai đã đặt chứ
                bool isOrdered = _context.OrderDetails.Any(od => od.FoodId == id);

                if (isOrdered)
                {
                    // khong xoá nếu có ng đặt
                    return Json(new
                    {
                        success = false,
                        message = "Không thể xóa! Món ăn này đã từng được khách đặt mua. Bạn không thể xóa để tránh làm hỏng lịch sử đơn hàng cũ."
                    });
                }

                // chưa ai đặt -> cho phép xóa thoải mái
                _context.Foods.Remove(food);
                _context.SaveChanges();

                return Json(new { success = true, message = "Đã xóa món ăn thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            } 
        }

        // API CẬP NHẬT TRẠNG THÁI HẾT MÓN/CÒN MÓN TỪ NÚT GẠT
        [HttpPost]
        public IActionResult ToggleFoodStatus(int id, bool isActive)
        {
            try
            {
                int? storeId = GetCurrentStoreId();
                var food = _context.Foods.FirstOrDefault(f => f.FoodId == id && f.StoreId == storeId);

                if (food == null) return Json(new { success = false, message = "Không tìm thấy món ăn hoặc bạn không có quyền!" });

                food.IsActive = isActive;
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Reviews()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "StoreOwner") return RedirectToAction("Index", "Home");

            var userId = HttpContext.Session.GetInt32("UserId");
            var store = _context.Stores.FirstOrDefault(s => s.OwnerId == userId);

            if (store == null) return RedirectToAction("RegisterStore", "Account");

            var reviews = _context.Orders
                .Include(o => o.User)
                .Where(o => o.StoreId == store.StoreId && o.StoreRating != null)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(reviews);
        }

        // ==========================================================
        // CÁC HÀM XỬ LÝ ĐƠN HÀNG (AJAX - KHÔNG CHỚP TRANG)
        // ==========================================================

        [HttpPost]
        public IActionResult AcceptOrder(int orderId)
        {
            var order = _context.Orders.Find(orderId);
            if (order != null && order.Status == "Pending")
            {
                order.Status = "Preparing";
                order.AcceptTime = DateTime.Now;
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã nhận đơn!" });
            }
            return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
        }

        [HttpPost]
        public IActionResult RejectOrder(int orderId, string cancelReason)
        {
            var order = _context.Orders.Find(orderId);
            if (order != null && order.Status == "Pending")
            {
                order.Status = "Cancelled";
                // 🔥 ĐÃ CẬP NHẬT: Gắn prefix "Quán hủy" để phân biệt với User hủy
                order.CancelReason = "Quán hủy: " + cancelReason;
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã từ chối đơn hàng!" });
            }
            return Json(new { success = false, message = "Không thể hủy đơn!" });
        }

        // 🔥 HÀM MỚI: API ĐỂ LẤY CHI TIẾT CÁC MÓN TRONG ĐƠN HÀNG (Dùng cho Modal Popup)
        [HttpGet]
        public IActionResult GetOrderDetails(int orderId)
        {
            var details = _context.OrderDetails
                .Include(od => od.Food)
                .Where(od => od.OrderId == orderId)
                .Select(od => new {
                    FoodName = od.Food.FoodName,
                    ImageUrl = string.IsNullOrEmpty(od.Food.ImageUrl) ? "/images/default_food.jpg" : od.Food.ImageUrl,
                    Price = od.Price,
                    Quantity = od.Quantity,
                    Note = od.Note
                }).ToList();

            if (!details.Any())
            {
                return Json(new { success = false, message = "Không tải được danh sách món ăn!" });
            }

            return Json(new { success = true, data = details });
        }
    }
}
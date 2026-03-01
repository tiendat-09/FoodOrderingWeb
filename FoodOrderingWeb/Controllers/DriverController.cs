using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FoodOrderingWeb.Controllers
{
    [Route("Driver")]
    public class DriverController : Controller
    {
        private readonly FoodOrderingDbContext _context;

        public DriverController(FoodOrderingDbContext context)
        {
            _context = context;
        }

        private bool IsDriver() => HttpContext.Session.GetString("Role") == "Driver";

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            if (!IsDriver()) return RedirectToAction("Login", "Account");

            var driverId = HttpContext.Session.GetInt32("UserId");

            // 1. Lấy thống kê
            ViewBag.TodayOrders = _context.Orders.Count(o => o.DriverId == driverId && o.OrderDate.HasValue && o.OrderDate.Value.Date == DateTime.Today && o.Status == "Completed");
            ViewBag.TodayIncome = _context.Orders.Where(o => o.DriverId == driverId && o.OrderDate.HasValue && o.OrderDate.Value.Date == DateTime.Today && o.Status == "Completed").Sum(o => (decimal?)o.ShippingFee) ?? 0;
            ViewBag.Rating = 5.0;

            // 2. TÌM ĐƠN HÀNG ĐANG ÔM (Shipping hoặc Delivering)
            var activeOrder = _context.Orders
                .Include(o => o.Store)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Food)
                .FirstOrDefault(o => o.DriverId == driverId && (o.Status == "Shipping" || o.Status == "Arrived" || o.Status == "Delivering"));

            ViewBag.ActiveOrder = activeOrder;

            // 3. NẾU KHÔNG ÔM ĐƠN NÀO -> TÌM ĐƠN QUÁN ĐÃ NHẬN NHƯNG CHƯA CÓ TÀI XẾ (Preparing)
            if (activeOrder == null)
            {
                var pendingOrders = _context.Orders
                    .Include(o => o.Store)
                    // 🔥 ĐÃ SỬA: Cho phép Tài xế thấy cả đơn Pending (vừa đặt xong) và Preparing (quán đang làm)
                    .Where(o => o.DriverId == null && o.Status == "Preparing")
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();
                return View(pendingOrders);
            }

            return View(new System.Collections.Generic.List<Order>());
        }

        [HttpPost("AcceptOrder")]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            var driverId = HttpContext.Session.GetInt32("UserId");
            if (driverId == null) return Json(new { success = false, message = "Vui lòng đăng nhập lại!" });

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

            if (order.DriverId != null)
            {
                return Json(new { success = false, message = "Rất tiếc! Đơn hàng này vừa được tài xế khác nhận mất rồi." });
            }

            order.DriverId = driverId;
            order.Status = "Shipping"; // 🔥 TÀI XẾ NHẬN -> ĐỔI THÀNH ĐANG ĐI LẤY/GIAO
            order.AcceptTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Nhận đơn thành công! Hãy di chuyển đến quán lấy đồ nhé." });
        }

        [HttpPost("UpdateOrderStatus")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string newStatus)
        {
            var driverId = HttpContext.Session.GetInt32("UserId");
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null || order.DriverId != driverId)
                return Json(new { success = false, message = "Lỗi: Không tìm thấy đơn hàng hoặc bạn không có quyền!" });

            // 🔥 CẬP NHẬT LOGIC THỜI GIAN MỚI
            if (newStatus == "Arrived")
            {
                // Có thể lưu thêm ArrivedTime nếu muốn, hiện tại chưa cần thiết
            }
            else if (newStatus == "Delivering")
            {
                order.PickupTime = DateTime.Now; // Tính là lúc đã lấy món xong và bắt đầu đi giao
            }
            else if (newStatus == "Completed")
            {
                order.DeliveryTime = DateTime.Now;
            }

            order.Status = newStatus;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet("History")]
        public IActionResult History()
        {
            if (!IsDriver()) return RedirectToAction("Login", "Account");
            var driverId = HttpContext.Session.GetInt32("UserId");

            var historyOrders = _context.Orders
                .Include(o => o.Store)
                .Where(o => o.DriverId == driverId)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(historyOrders);
        }

        [HttpGet("Income")]
        public IActionResult Income()
        {
            if (!IsDriver()) return RedirectToAction("Login", "Account");
            var driverId = HttpContext.Session.GetInt32("UserId");

            var completedOrders = _context.Orders
                .Include(o => o.Store)
                .Where(o => o.DriverId == driverId && o.Status == "Completed" && o.OrderDate.HasValue)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = today.AddDays(-1 * diff);
            DateTime startOfMonth = new DateTime(today.Year, today.Month, 1);

            ViewBag.TodayIncome = completedOrders.Where(o => o.OrderDate.Value.Date == today).Sum(o => (decimal)o.ShippingFee);
            ViewBag.WeekIncome = completedOrders.Where(o => o.OrderDate.Value.Date >= startOfWeek).Sum(o => (decimal)o.ShippingFee);
            ViewBag.MonthIncome = completedOrders.Where(o => o.OrderDate.Value.Date >= startOfMonth).Sum(o => (decimal)o.ShippingFee);

            return View(completedOrders);
        }

        [HttpGet("Reviews")]
        public IActionResult Reviews()
        {
            if (!IsDriver()) return RedirectToAction("Login", "Account");
            var driverId = HttpContext.Session.GetInt32("UserId");

            var reviews = _context.Orders
                .Include(o => o.User)
                .Where(o => o.DriverId == driverId && o.DriverRating != null)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(reviews);
        }
        // ==========================================
        // 🔥 TRANG HỒ SƠ TÀI XẾ (PROFILE)
        // ==========================================
        [HttpGet("Profile")]
        public async Task<IActionResult> Profile()
        {
            if (!IsDriver()) return RedirectToAction("Login", "Account");
            var driverId = HttpContext.Session.GetInt32("UserId");

            var driver = await _context.Users.FindAsync(driverId);
            if (driver == null) return NotFound("Không tìm thấy tài khoản");

            return View(driver);
        }

        [HttpPost("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile(string fullName, string phoneNumber, string citizenId, string licensePlate)
        {
            if (!IsDriver()) return Json(new { success = false, message = "Lỗi xác thực" });
            var driverId = HttpContext.Session.GetInt32("UserId");

            var driver = await _context.Users.FindAsync(driverId);
            if (driver == null) return Json(new { success = false, message = "Không tìm thấy tài khoản" });

            // Cập nhật thông tin
            driver.FullName = fullName;
            driver.PhoneNumber = phoneNumber;
            driver.CitizenId = citizenId;
            driver.LicensePlate = licensePlate;

            // Cập nhật lại tên vào Session phòng trường hợp tài xế đổi tên
            HttpContext.Session.SetString("FullName", fullName);

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã lưu hồ sơ thành công!" });
        }
    }
}
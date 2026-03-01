using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FoodOrderingWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly FoodOrderingDbContext _context;

        public AdminController(FoodOrderingDbContext context)
        {
            _context = context;
        }

        // Kiểm tra quyền Admin dựa trên Session
        private bool IsAdmin() => HttpContext.Session.GetString("Role") == "Admin";

        // ==========================================
        // 1. TỔNG QUAN HỆ THỐNG (DASHBOARD)
        // ==========================================
        public IActionResult Index()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.TotalOrders = _context.Orders.Count();
            ViewBag.TotalRevenue = _context.Orders.Where(o => o.Status == "Completed").Sum(o => (decimal?)o.TotalAmount) ?? 0;

            // 🔥 FIX LOGIC: Đếm và lấy chính xác các quán mà User đang là PendingStoreOwner
            ViewBag.PendingStoresCount = _context.Stores.Include(s => s.Owner).Count(s => s.Owner.Role == "PendingStoreOwner");
            ViewBag.PendingStoresList = _context.Stores.Include(s => s.Owner).Where(s => s.Owner.Role == "PendingStoreOwner").ToList();

            ViewBag.PendingDriverCount = _context.Users.Count(u => u.Role == "PendingDriver");
            ViewBag.PendingDriversList = _context.Users.Where(u => u.Role == "PendingDriver").ToList();

            var recentOrders = _context.Orders.OrderByDescending(o => o.OrderDate).Take(5).ToList();
            return View(recentOrders);
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                try
                {
                    _context.Users.Remove(user);
                    _context.SaveChanges();
                    return Json(new { success = true, message = "Đã xóa tài khoản vĩnh viễn!" });
                }
                catch (Exception)
                {
                    return Json(new { success = false, message = "Không thể xóa! Người dùng này đang có Đơn hàng, Giỏ hàng hoặc Quán ăn trong hệ thống." });
                }
            }
            return Json(new { success = false, message = "Không tìm thấy người dùng!" });
        }

        // ==========================================
        // 2. QUẢN LÝ CỬA HÀNG 
        // ==========================================
        [HttpPost]
        public IActionResult ToggleStoreStatus(int id, bool isActive)
        {
            var store = _context.Stores.Find(id);
            if (store != null)
            {
                store.IsActive = isActive;
                _context.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Không tìm thấy cửa hàng!" });
        }

        [Route("Admin/ManageStores")]
        public IActionResult ManageStores(string filter = "")
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var query = _context.Stores.Include(s => s.Owner).AsQueryable();

            // 🔥 FIX LOGIC: Lọc quán chờ duyệt CHUẨN XÁC
            if (filter == "pending")
            {
                query = query.Where(s => s.Owner.Role == "PendingStoreOwner");
            }
            else
            {
                // Nếu xem bình thường thì KHÔNG hiển thị quán đang chờ duyệt để tránh lú lẫn
                query = query.Where(s => s.Owner.Role != "PendingStoreOwner");
            }

            var stores = query.OrderByDescending(s => s.StoreId).ToList();
            ViewBag.Filter = filter;
            return View(stores);
        }

        [HttpGet]
        [Route("Admin/CreateStore")]
        public IActionResult CreateStore()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [Route("Admin/CreateStore")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStore(Store store, IFormFile? imageFile)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "stores");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    var filePath = Path.Combine(uploadPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create)) { await imageFile.CopyToAsync(stream); }
                    store.ImageUrl = "/images/stores/" + fileName;
                }
                else { store.ImageUrl = "/images/stores/default_store.jpg"; }

                store.CreatedAt = DateTime.Now;
                store.IsActive = true;
                _context.Stores.Add(store);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm cửa hàng thành công!";
                return RedirectToAction("ManageStores");
            }
            return View(store);
        }

        [HttpGet]
        [Route("Admin/EditStore/{id}")]
        public async Task<IActionResult> EditStore(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var store = await _context.Stores.FindAsync(id);
            if (store == null) return NotFound();
            return View(store);
        }

        [HttpPost]
        [Route("Admin/EditStore/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStore(int id, Store store, IFormFile? imageFile)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            if (id != store.StoreId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var oldStore = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.StoreId == id);
                    if (oldStore == null) return NotFound();

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "stores", fileName);
                        using (var stream = new FileStream(uploadPath, FileMode.Create)) { await imageFile.CopyToAsync(stream); }
                        store.ImageUrl = "/images/stores/" + fileName;
                    }
                    else { store.ImageUrl = oldStore.ImageUrl; }

                    store.CreatedAt = oldStore.CreatedAt;
                    _context.Update(store);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Cập nhật thành công!";
                    return RedirectToAction("ManageStores");
                }
                catch (Exception) { throw; }
            }
            return View(store);
        }

        // ==========================================
        // 3. QUẢN LÝ TÀI KHOẢN (USERS)
        // ==========================================
        [HttpGet]
        public IActionResult Users(string tab = "Customer", string search = "")
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.CurrentTab = tab;
            ViewBag.Search = search;

            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search) || u.PhoneNumber.Contains(search));
            }

            if (tab == "Customer")
            {
                query = query.Where(u => u.Role == "Customer" || u.Role == "PendingStoreOwner");
            }
            else if (tab == "StoreOwner")
            {
                query = query.Where(u => u.Role == "StoreOwner");
            }
            else if (tab == "Driver")
            {
                query = query.Where(u => u.Role == "Driver" || u.Role == "PendingDriver");
            }

            var users = query.ToList();
            return View(users);
        }

        // ==========================================
        // 4. DUYỆT ĐỐI TÁC 
        // ==========================================
        [HttpPost]
        public IActionResult ApproveStore(int id)
        {
            if (!IsAdmin()) return Json(new { success = false });
            var store = _context.Stores.Find(id);
            if (store != null)
            {
                store.IsActive = true;
                var owner = _context.Users.Find(store.OwnerId);
                if (owner != null) owner.Role = "StoreOwner"; // Khách hàng chính thức thành Chủ quán
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã duyệt quán thành công!" });
            }
            return Json(new { success = false });
        }

        [HttpGet]
        public IActionResult Profile()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var userId = HttpContext.Session.GetInt32("UserId");
            var user = _context.Users.Find(userId);
            return View(user);
        }

        [HttpGet]
        public IActionResult GetUserJson(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return Json(new { success = false });
            return Json(new { success = true, data = new { userId = user.UserId, fullName = user.FullName, phoneNumber = user.PhoneNumber, role = user.Role } });
        }

        [HttpGet]
        [Route("Admin/GetPendingStoresJson")]
        public IActionResult GetPendingStoresJson()
        {
            if (!IsAdmin()) return Unauthorized();

            var pendingStores = _context.Stores
                .Include(s => s.Owner)
                .Where(s => s.Owner.Role == "PendingStoreOwner") // 🔥 Bắt chặt bằng ROLE
                .Select(s => new {
                    storeId = s.StoreId,
                    storeName = s.StoreName,
                    ownerName = s.Owner != null ? s.Owner.FullName : "Không rõ",
                    phoneNumber = s.PhoneNumber,
                    address = s.Address,
                    createdAt = s.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToList();

            return Json(new { success = true, data = pendingStores });
        }

        [HttpGet]
        public IActionResult GetStoreMenu(int id)
        {
            var foods = _context.Foods
                                .Where(f => f.StoreId == id)
                                .Select(f => new {
                                    f.FoodName,
                                    f.Price,
                                    f.ImageUrl
                                }).ToList();

            return Json(new { success = true, data = foods });
        }

        [HttpPost]
        public IActionResult ApproveDriver(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null && user.Role == "PendingDriver")
            {
                user.Role = "Driver";
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã duyệt tài xế thành công!" });
            }
            return Json(new { success = false, message = "Không tìm thấy hồ sơ hoặc đã được duyệt!" });
        }

        [HttpPost]
        public IActionResult EditUser(User model)
        {
            var user = _context.Users.Find(model.UserId);
            if (user != null)
            {
                // 1. KIỂM TRA NẾU ADMIN MUỐN ĐỔI THÀNH TÀI XẾ
                if (model.Role == "Driver" && user.Role != "Driver")
                {
                    // Nếu chưa có CCCD hoặc Biển số xe -> Chặn ngay!
                    if (string.IsNullOrEmpty(user.CitizenId) || string.IsNullOrEmpty(user.LicensePlate))
                    {
                        return Json(new { success = false, message = "Lỗi: Tài khoản này chưa nộp hồ sơ đăng ký đối tác Tài xế!" });
                    }
                }

                // 2. KIỂM TRA NẾU ADMIN MUỐN ĐỔI THÀNH CHỦ QUÁN
                if (model.Role == "StoreOwner" && user.Role != "StoreOwner")
                {
                    // Kiểm tra xem user này đã có quán ăn nào trong bảng Stores chưa
                    bool hasStore = _context.Stores.Any(s => s.OwnerId == user.UserId);
                    if (!hasStore)
                    {
                        return Json(new { success = false, message = "Lỗi: Tài khoản này chưa tạo thông tin Quán ăn nào trên hệ thống!" });
                    }
                }

                // 3. NẾU VƯỢ QUA HẾT CÁC BÀI KIỂM TRA -> CHO PHÉP LƯU
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber;
                user.Role = model.Role;

                _context.SaveChanges();
                return Json(new { success = true, message = "Đã cập nhật quyền thành công!" });
            }
            return Json(new { success = false, message = "Không tìm thấy người dùng!" });
        }

        [HttpPost]
        public IActionResult RejectDriver(int id, string? reason)
        {
            var user = _context.Users.Find(id);
            if (user != null && user.Role == "PendingDriver")
            {
                user.Role = "Customer";
                user.RejectReason = reason ?? "Hồ sơ không đạt yêu cầu";
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã từ chối tài xế!" });
            }
            return Json(new { success = false, message = "Lỗi khi xử lý!" });
        }

        [HttpPost]
        public IActionResult RejectStore(int id, string? reason)
        {
            var store = _context.Stores.Find(id);
            if (store != null)
            {
                var user = _context.Users.Find(store.OwnerId);
                if (user != null && user.Role == "PendingStoreOwner")
                {
                    user.Role = "Customer";
                    user.RejectReason = reason ?? "Menu hoặc thông tin quán không hợp lệ";
                }

                _context.Stores.Remove(store);
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã từ chối mở quán!" });
            }
            return Json(new { success = false, message = "Không tìm thấy quán!" });
        }
    }
}
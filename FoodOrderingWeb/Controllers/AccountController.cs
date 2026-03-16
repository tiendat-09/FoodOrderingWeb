using FoodOrderingWeb.Helpers;
using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace FoodOrderingWeb.Controllers
{
    [Route("Account")]
    public class AccountController : Controller
    {
        private readonly FoodOrderingDbContext _context;

        public AccountController(FoodOrderingDbContext context)
        {
            _context = context;
        }

        [HttpGet("Login")]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated && HttpContext.Session.GetInt32("UserId") != null)
            {
                string role = HttpContext.Session.GetString("Role");
                if (role == "Admin") return RedirectToAction("Index", "Admin");
                if (role == "StoreOwner") return RedirectToAction("Index", "StoreManager");
                if (role == "Driver") return RedirectToAction("Index", "Driver");
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(string email, string matKhau, bool rememberMe)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(matKhau))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            var md5Pass = GetMD5(matKhau);
            var user = _context.Users.FirstOrDefault(u => (u.Email == email || u.Username == email) && u.PasswordHash == md5Pass);

            if (user != null)
            {
                // kiểm tra xem quán có bị Admin khóa không
                string activeRole = user.Role;
                bool isLockedStore = false;

                if (user.Role == "StoreOwner")
                {
                    var store = _context.Stores.FirstOrDefault(s => s.OwnerId == user.UserId);
                    if (store != null && (store.IsActive == false || store.IsActive == null))
                    {
                        activeRole = "Customer"; // Ép quyền Session về Khách hàng
                        isLockedStore = true;
                    }
                }

                // Truyền quyền thực tế (activeRole) vào hàm tạo Session
                await DoLoginAsync(user, rememberMe, activeRole);

                if (!string.IsNullOrEmpty(user.RejectionMessage))
                {
                    TempData["GlobalMessage"] = user.RejectionMessage;
                    TempData["GlobalType"] = "error";
                    user.RejectionMessage = null;
                    await _context.SaveChangesAsync();
                }

                if (isLockedStore)
                {
                    TempData["GlobalMessage"] = "Quán của bạn đang bị Tạm khóa! Bạn đang dùng giao diện Khách hàng.";
                    TempData["GlobalType"] = "warning";
                    return RedirectToAction("Index", "Home");
                }

                if (activeRole == "Admin") return RedirectToAction("Index", "Admin");
                if (activeRole == "StoreOwner") return RedirectToAction("Index", "StoreManager");
                if (activeRole == "Driver") return RedirectToAction("Index", "Driver");
                return RedirectToAction("Index", "Home");
            }

            TempData["Error"] = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }

        [HttpGet("GoogleLogin")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("GoogleResponse")]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                TempData["Error"] = "Đăng nhập Google thất bại.";
                return RedirectToAction("Login");
            }

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Không lấy được Email từ Google.";
                return RedirectToAction("Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Username = email,
                    FullName = name ?? "Google User",
                    Role = "Customer",
                    CreatedAt = DateTime.Now,
                    Avatar = "/images/avatars/default_user.jpg",
                    PasswordHash = "GOOGLE_AUTH"
                };
                _context.Users.Add(user);
                _context.SaveChanges();
            }

            await DoLoginAsync(user, true);

            if (!string.IsNullOrEmpty(user.RejectionMessage))
            {
                TempData["GlobalMessage"] = user.RejectionMessage;
                TempData["GlobalType"] = "error";
                user.RejectionMessage = null;
                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["SuccessMessage"] = $"Xin chào {user.FullName}!";
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet("Profile")]
        public IActionResult Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");
            var user = _context.Users.Find(userId);
            if (user == null) return RedirectToAction("Login");
            if (user.Role == "Admin") return RedirectToAction("Profile", "Admin");

            if (!string.IsNullOrEmpty(user.RejectionMessage))
            {
                TempData["GlobalMessage"] = user.RejectionMessage;
                TempData["GlobalType"] = "error";
                user.RejectionMessage = null;
                _context.SaveChanges();
            }

            var pendingStore = _context.Stores.FirstOrDefault(s => s.OwnerId == userId && s.IsActive == false);
            if (pendingStore != null) ViewBag.IsPendingStore = true;

            return View(user);
        }

        [HttpPost("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile(User model, string selectedAvatar)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user != null)
            {
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber;
                user.Address = model.Address;
                if (!string.IsNullOrEmpty(selectedAvatar)) user.Avatar = selectedAvatar;
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("Avatar", user.Avatar ?? "/images/avatars/default_user.jpg");
                HttpContext.Session.SetString("FullName", user.FullName);
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            return Json(new { success = false, message = "Lỗi không tìm thấy người dùng" });
        }

        [Route("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("Register")]
        public IActionResult Register() => View();

        [HttpPost("Register")]
        public IActionResult Register(User user, string ConfirmPassword, string Password)
        {
            if (Password != ConfirmPassword) { TempData["Error"] = "Mật khẩu không khớp!"; return View(user); }
            if (_context.Users.Any(u => u.Email == user.Email)) { TempData["Error"] = "Email đã tồn tại!"; return View(user); }
            user.PasswordHash = GetMD5(Password);
            user.Role = "Customer";
            user.CreatedAt = DateTime.Now;
            user.Avatar = "/images/avatars/default_user.jpg";
            _context.Users.Add(user);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đăng ký thành công!";
            return RedirectToAction("Login");
        }

        [HttpPost("ChangePassword")]
        public IActionResult ChangePassword(string CurrentPassword, string NewPassword, string ConfirmNewPassword)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var user = _context.Users.Find(userId);
            if (user.PasswordHash != GetMD5(CurrentPassword)) return Json(new { success = false, message = "Sai mật khẩu cũ!" });
            if (NewPassword != ConfirmNewPassword) return Json(new { success = false, message = "Mật khẩu mới không khớp!" });
            user.PasswordHash = GetMD5(NewPassword);
            _context.SaveChanges();
            return Json(new { success = true, message = "Đổi mật khẩu thành công!" });
        }

        [HttpPost("ChangeEmail")]
        public IActionResult ChangeEmail(string NewEmail, string PasswordConfirm)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var user = _context.Users.Find(userId);
            if (user.PasswordHash != GetMD5(PasswordConfirm)) return Json(new { success = false, message = "Sai mật khẩu!" });
            if (_context.Users.Any(u => u.Email == NewEmail)) return Json(new { success = false, message = "Email đã tồn tại!" });
            user.Email = NewEmail; user.Username = NewEmail;
            _context.SaveChanges();
            return Json(new { success = true, message = "Đổi email thành công!" });
        }

        [HttpGet("RegisterStore")]
        public IActionResult RegisterStore()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _context.Users.Find(userId);

            bool hasPendingStore = _context.Stores.Any(s => s.OwnerId == userId.Value && (s.IsActive == false || s.IsActive == null)); // kiểm tra có đơn nào đã gửi hay không
            if (hasPendingStore)
            {
                TempData["Error"] = "Hồ sơ mở quán của bạn đang chờ duyệt, vui lòng không gửi lại!";
                return RedirectToAction("Index", "Home");
            }

            if (user.Role == "PendingDriver")
            {
                TempData["Error"] = "Bạn đã gửi hồ sơ đăng ký tài xế nên không thể mở quán!";
                return RedirectToAction("Index", "Home");
            }

            if (user.Role == "Driver" || user.Role == "StoreOwner")
            {
                TempData["Error"] = "Bạn không đủ điều kiện đăng ký lúc này!";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost("RegisterStore")]
        public IActionResult RegisterStore(Store store, string OwnerName)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập!" });

                var user = _context.Users.Find(userId);
                if (!string.IsNullOrEmpty(OwnerName)) { user.FullName = OwnerName; }

                var existingStore = _context.Stores.FirstOrDefault(s => s.OwnerId == userId); // xem u này từng mở quán trong DB chưa   
                if (existingStore != null)
                {
                    // cập nhật lại tt quán cũ và đổi trạng thái về chờ duyệt
                    existingStore.StoreName = store.StoreName;
                    existingStore.Address = store.Address;
                    existingStore.Description = store.Description;
                    existingStore.OpeningTime = store.OpeningTime;
                    existingStore.ClosingTime = store.ClosingTime;
                    existingStore.IsActive = false;
                }
                else
                {
                    // Tạo quán mới hoàn toàn
                    store.OwnerId = userId.Value;
                    store.IsActive = false;
                    store.Rating = 5.0;
                    store.ImageUrl = "/images/stores/default_store.jpg";
                    _context.Stores.Add(store);
                }

                user.Role = "PendingStoreOwner";
                _context.SaveChanges();
                HttpContext.Session.SetString("Role", "PendingStoreOwner");

                return Json(new { success = true, message = "Hồ sơ đã gửi thành công!" }); // view khong load lại và gửi popup
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpGet("RegisterDriver")]
        public IActionResult RegisterDriver()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _context.Users.Find(userId);

            bool hasPendingStore = _context.Stores.Any(s => s.OwnerId == userId.Value && (s.IsActive == false || s.IsActive == null));
            if (hasPendingStore)
            {
                TempData["Error"] = "Bạn đã gửi hồ sơ đăng ký quán nên không thể đăng ký tài xế!";
                return RedirectToAction("Index", "Home");
            }

            if (user.Role == "PendingDriver" || user.Role == "Driver" || user.Role == "StoreOwner")
            {
                TempData["Error"] = "Bạn không đủ điều kiện đăng ký lúc này!";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost("RegisterDriver")]
        public async Task<IActionResult> RegisterDriver(string FullName, string PhoneNumber, string CitizenId, string LicensePlate)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập trước!" });
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return Json(new { success = false, message = "Không tìm thấy tài khoản!" });

                user.FullName = FullName;
                user.PhoneNumber = PhoneNumber;
                user.Role = "PendingDriver";
                user.CitizenId = CitizenId;
                user.LicensePlate = LicensePlate;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // ÉP SESSION NHẬN DIỆN QUYỀN MỚI
                HttpContext.Session.SetString("Role", "PendingDriver");

                return Json(new { success = true, message = "Đăng ký thành công! Đang chờ xét duyệt." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message }); }
        }

        private async Task DoLoginAsync(User user, bool isPersistent, string forceRole = null)
        {
            string finalRole = forceRole ?? user.Role ?? "Customer";

            var claims = new List<Claim> {
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Role, finalRole)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = isPersistent });

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Email);
            HttpContext.Session.SetString("Role", finalRole);
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Email);
            string avatar = user.Avatar ?? "/images/avatars/default_user.jpg";
            HttpContext.Session.SetString("Avatar", avatar);

            if (finalRole == "Driver" || finalRole == "StoreOwner")
            {
                HttpContext.Session.Remove("Cart"); 
            }
        }

        public static string GetMD5(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(str);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("X2"));
                return sb.ToString();
            }
        }

        [HttpGet("ForgotPassword")]
        public IActionResult ForgotPassword() => View();

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return Json(new { success = false, message = "Email không tồn tại!" });
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Food Delivery", "taotiet3317@gmail.com"));
                message.To.Add(new MailboxAddress(user.FullName, email));
                message.Subject = "Khôi phục mật khẩu";
                message.Body = new TextPart("html") { Text = "Mật khẩu mới của bạn là: <b>123456</b>. Vui lòng đổi lại sau khi đăng nhập." };

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync("taotiet3317@gmail.com", "oggu rwpw ldac fcuo");
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                user.PasswordHash = GetMD5("123456");
                _context.SaveChanges();
                return Json(new { success = true, message = "Mật khẩu mới đã được gửi về Email của bạn!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi gửi mail: " + ex.Message }); }
        }

        [AllowAnonymous]
        [HttpGet("CheckRegistration")]
        [HttpPost("CheckRegistration")]
        public IActionResult CheckRegistration(string type)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null) return Json(new { allowed = false, redirect = "/Account/Login" });

                var user = _context.Users.Find(userId);
                if (user == null) return Json(new { allowed = false, message = "Không tìm thấy người dùng." });

                if (user.Role == "Driver" || user.Role == "StoreOwner")
                    return Json(new { allowed = false, message = "Bạn đã là đối tác, không thể đăng ký thêm!" });

                bool hasPendingStore = _context.Stores.Any(s => s.OwnerId == userId.Value && (s.IsActive == false || s.IsActive == null));

                if (type == "driver")
                {
                    if (hasPendingStore)
                        return Json(new { allowed = false, message = "Bạn đã gửi hồ sơ đăng ký quán nên không thể đăng ký tài xế!" });

                    if (user.Role == "PendingDriver")
                        return Json(new { allowed = false, message = "Hồ sơ tài xế của bạn đang chờ duyệt!" });
                }
                else if (type == "store")
                {
                    if (user.Role == "PendingDriver")
                        return Json(new { allowed = false, message = "Bạn đã gửi hồ sơ đăng ký tài xế nên không thể mở quán!" });

                    if (hasPendingStore)
                        return Json(new { allowed = false, message = "Hồ sơ mở quán của bạn đang chờ duyệt!" });
                }

                return Json(new { allowed = true });
            }
            catch (Exception ex)
            {
                return Json(new { allowed = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet("OrderHistory")]
        public IActionResult OrderHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");
            var myOrders = _context.Orders
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Food)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToList();
            return View(myOrders);
        }
        [HttpGet("CheckUserStatus")]
        public IActionResult CheckUserStatus()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { hasRejection = false, isApproved = false });

            var user = _context.Users.Find(userId);
            if (user == null) return Json(new { hasRejection = false, isApproved = false });

            // 1. Kiểm tra xem có bị TỪ CHỐI không?
            if (!string.IsNullOrEmpty(user.RejectReason))
            {
                string reason = user.RejectReason;
                user.RejectReason = null; // Xóa để thông báo không bị hiện lặp lại
                _context.SaveChanges();

                // Trả về Customer để hiện lại nút đăng ký
                HttpContext.Session.SetString("Role", user.Role);
                return Json(new { hasRejection = true, reason = reason });
            }

            // 2. Kiểm tra xem có được DUYỆT THÀNH CÔNG không?
            var sessionRole = HttpContext.Session.GetString("Role");
            if ((sessionRole == "PendingStoreOwner" || sessionRole == "PendingDriver") &&
                (user.Role == "StoreOwner" || user.Role == "Driver"))
            {
                // Ép Session nhận quyền mới ngay lập tức
                HttpContext.Session.SetString("Role", user.Role);
                return Json(new { isApproved = true, newRole = user.Role });
            }

            return Json(new { hasRejection = false, isApproved = false });
        }
    }
}
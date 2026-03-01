using FoodOrderingWeb.Helpers;
using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FoodOrderingWeb.Controllers
{
    public class CartController : Controller
    {
        private readonly FoodOrderingDbContext _context;

        public CartController(FoodOrderingDbContext context)
        {
            _context = context;
        }

        private List<CartItem> GetCartItems()
        {
            return HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
        }

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.Set("Cart", cart);
        }

        private decimal CalculateSizePrice(decimal basePrice, string size)
        {
            switch (size)
            {
                case "Vua": return basePrice + 10000;
                case "Lon": return basePrice + 20000;
                default: return basePrice;
            }
        }

        public IActionResult Index()
        {
            var cart = GetCartItems();
            return View(cart);
        }

        [HttpPost]
        public IActionResult AddToCart(int foodId, int quantity, string size, string note)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Json(new { success = false, message = "Bạn cần đăng nhập để mua sắm!" });

            var role = HttpContext.Session.GetString("Role");
            if (role == "Driver" || role == "StoreOwner")
            {
                return Json(new
                {
                    success = false,
                    message = "Tài khoản đối tác không thể đặt hàng. Vui lòng dùng tài khoản Khách hàng!"
                });
            }

            var food = _context.Foods.Find(foodId);
            if (food == null) return Json(new { success = false, message = "Món ăn không tồn tại!" });

            var cart = GetCartItems();

            if (cart.Any())
            {
                var firstItemFoodId = cart.First().FoodId;
                var firstFood = _context.Foods.Find(firstItemFoodId);

                if (firstFood != null && firstFood.StoreId != food.StoreId)
                {
                    var currentStoreName = _context.Stores.Find(firstFood.StoreId)?.StoreName ?? "Quán khác";
                    return Json(new
                    {
                        success = false,
                        conflict = true,
                        message = $"Giỏ hàng đang có món từ '{currentStoreName}'. Bạn có muốn xóa giỏ hàng để đặt món từ quán mới này không?"
                    });
                }
            }

            int safeQuantity = quantity > 0 ? quantity : 1;
            var safeSize = string.IsNullOrEmpty(size) ? "Nho" : size;
            var safeNote = (note ?? "").Trim();

            // 🔥 ĐÃ FIX: Ép kiểu (decimal)food.Price
            decimal finalPrice = CalculateSizePrice((decimal)food.Price, safeSize);

            var existingItem = cart.FirstOrDefault(x =>
                x.FoodId == foodId &&
                (x.Size ?? "Nho") == safeSize &&
                (x.Note ?? "").Trim() == safeNote
            );

            if (existingItem != null)
            {
                existingItem.Quantity += safeQuantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    CartItemId = Guid.NewGuid(),
                    FoodId = food.FoodId,
                    FoodName = food.FoodName,
                    Price = finalPrice,
                    ImageUrl = food.ImageUrl,
                    Quantity = safeQuantity,
                    Size = safeSize,
                    Note = safeNote
                });
            }

            SaveCart(cart);
            return Json(new { success = true, message = "Đã thêm vào giỏ!", totalQty = cart.Sum(x => x.Quantity) });
        }

        [HttpPost]
        public IActionResult ClearAndAddToCart(int foodId, int quantity, string size, string note)
        {
            var food = _context.Foods.Find(foodId);
            if (food == null) return Json(new { success = false, message = "Lỗi sản phẩm!" });

            var newCart = new List<CartItem>();
            int safeQuantity = quantity > 0 ? quantity : 1;
            var safeSize = string.IsNullOrEmpty(size) ? "Nho" : size;

            // 🔥 ĐÃ FIX: Ép kiểu (decimal)food.Price
            decimal finalPrice = CalculateSizePrice((decimal)food.Price, safeSize);

            newCart.Add(new CartItem
            {
                CartItemId = Guid.NewGuid(),
                FoodId = food.FoodId,
                FoodName = food.FoodName,
                Price = finalPrice,
                ImageUrl = food.ImageUrl,
                Quantity = safeQuantity,
                Size = safeSize,
                Note = (note ?? "").Trim()
            });

            SaveCart(newCart);
            return Json(new { success = true, message = "Đã tạo giỏ hàng mới!", totalQty = safeQuantity });
        }

        [HttpPost]
        public IActionResult UpdateQuantityAjax(Guid cartItemId, int quantity)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(x => x.CartItemId == cartItemId);
            if (item != null)
            {
                item.Quantity = quantity;
                if (item.Quantity <= 0) cart.Remove(item);
                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    itemPrice = (item.Price * item.Quantity).ToString("#,##0") + " đ",
                    cartTotal = cart.Sum(x => x.Price * x.Quantity).ToString("#,##0") + " đ",
                    totalQty = cart.Sum(x => x.Quantity)
                });
            }
            return Json(new { success = false });
        }

        public IActionResult Remove(Guid id)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(p => p.CartItemId == id);
            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }
            return RedirectToAction("Index");
        }

        // 🔥 ĐÃ THÊM HÀM MỚI ĐỂ LƯU GHI CHÚ VÀ SIZE TỪ POPUP
        [HttpPost]
        public IActionResult UpdateCartItem(Guid cartItemId, string size, string note, int quantity)
        {
            var cart = GetCartItems();
            var currentItem = cart.FirstOrDefault(p => p.CartItemId == cartItemId);

            if (currentItem != null)
            {
                var food = _context.Foods.Find(currentItem.FoodId);
                if (food != null)
                {
                    // 1. Chuẩn hóa dữ liệu mới
                    string safeSize = size ?? "Nho";
                    string safeNote = (note ?? "").Trim();
                    decimal newPrice = CalculateSizePrice((decimal)food.Price, safeSize);

                    // 2. Tìm xem trong giỏ có món nào KHÁC (khác ID) nhưng giống y hệt Size và Ghi chú không?
                    var existingIdenticalItem = cart.FirstOrDefault(x =>
                        x.CartItemId != cartItemId &&
                        x.FoodId == currentItem.FoodId &&
                        (x.Size ?? "Nho") == safeSize &&
                        (x.Note ?? "").Trim() == safeNote
                    );

                    if (existingIdenticalItem != null)
                    {
                        // 3. NẾU TRÙNG: Cộng dồn số lượng vào món kia, và xóa món hiện tại đi
                        existingIdenticalItem.Quantity += quantity;
                        cart.Remove(currentItem);
                    }
                    else
                    {
                        // 4. NẾU KHÔNG TRÙNG: Cập nhật bình thường
                        currentItem.Size = safeSize;
                        currentItem.Note = safeNote;
                        currentItem.Price = newPrice;
                        currentItem.Quantity = quantity;
                    }

                    SaveCart(cart);
                    return Json(new { success = true });
                }
            }
            return Json(new { success = false });
        }

        public IActionResult Checkout()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role == "Driver" || role == "StoreOwner")
            {
                TempData["ErrorMessage"] = "Tài khoản Đối tác không thể thực hiện đặt hàng. Vui lòng dùng tài khoản Người dùng!";
                return RedirectToAction("Index");
            }

            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (cart.Count == 0) return RedirectToAction("Index");

            var model = new Checkout
            {
                FullName = HttpContext.Session.GetString("FullName") ?? "",
                PhoneNumber = HttpContext.Session.GetString("PhoneNumber") ?? "",
                Address = HttpContext.Session.GetString("Address") ?? "",
                CartItems = cart,
                // 🔥 ĐÃ FIX: Ép kiểu an toàn khi tính tổng tiền
                TotalAmount = (decimal)cart.Sum(x => (decimal)x.TotalPrice)
            };

            var firstFoodId = cart.First().FoodId;
            var food = _context.Foods.Find(firstFoodId);
            if (food != null)
            {
                var store = _context.Stores.Find(food.StoreId);
                ViewBag.CurrentStore = store;
            }

            return View(model);
        }

        [HttpPost]
        public IActionResult CalculateShippingFee(double userLat, double userLng, double storeLat, double storeLng)
        {
            try
            {
                var R = 6371;
                var dLat = (storeLat - userLat) * Math.PI / 180.0;
                var dLon = (storeLng - userLng) * Math.PI / 180.0;
                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(userLat * Math.PI / 180.0) * Math.Cos(storeLat * Math.PI / 180.0) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

                var distance = R * c;
                var actualDistance = Math.Round(distance * 1.4, 1);

                // 🔥 ĐÃ SỬA CÔNG THỨC: 10.000đ (Mặc định) + 5.000đ/km
                decimal baseFee = 10000m;
                decimal distanceFee = (decimal)(actualDistance * 5000);
                decimal shipFee = baseFee + distanceFee;

                // Làm tròn tiền cho đẹp (VD: 17.300đ thành 17.000đ)
                shipFee = Math.Round(shipFee / 1000) * 1000;

                return Json(new { success = true, distance = actualDistance, fee = shipFee });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessOrder(string receiverName, string receiverPhone, string address, string paymentMethod, int storeId, decimal shippingFee)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null) return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn!" });

                var cart = GetCartItems();
                if (!cart.Any()) return Json(new { success = false, message = "Giỏ hàng đang trống!" });

                // 🔥 ĐÃ FIX: Ép kiểu an toàn khi tính tổng
                decimal cartTotal = (decimal)cart.Sum(x => (decimal)x.Price * x.Quantity);
                decimal finalTotalAmount = cartTotal + shippingFee;

                var newOrder = new Order
                {
                    UserId = userId.Value,
                    StoreId = storeId,
                    ReceiverName = receiverName,
                    ReceiverPhone = receiverPhone,
                    Address = address,
                    PaymentMethod = paymentMethod,
                    ShippingFee = shippingFee,
                    TotalAmount = finalTotalAmount,
                    OrderDate = DateTime.Now,

                    // 🔥 ĐÃ SỬA TẠI ĐÂY: Đổi từ "Pending" thành "Preparing" để tài xế thấy được luôn
                    Status = "Pending"
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                foreach (var item in cart)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = newOrder.OrderId,
                        FoodId = item.FoodId,
                        Quantity = item.Quantity,
                        Price = (decimal)item.Price, // 🔥 ĐÃ FIX
                        Note = item.Note
                    };
                    _context.OrderDetails.Add(orderDetail);
                }
                await _context.SaveChangesAsync();

                // Đặt hàng xong thì xóa giỏ hàng
                HttpContext.Session.Remove("Cart");

                return Json(new { success = true, isVnPay = false });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xử lý đơn hàng: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Tracking(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.Store)
                .Include(o => o.Driver)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Food)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null) return NotFound("Không tìm thấy đơn hàng, hoặc bạn không có quyền xem đơn này!");

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitDriverReview(int orderId, int rating, string review)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.DriverRating = rating;
                order.DriverReview = review;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitStoreReview(int orderId, int rating, string review)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.StoreRating = rating;
                order.StoreReview = review;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
        // 🔥 HÀM MỚI: API ĐỂ GIAO DIỆN KHÁCH HÀNG HỎI THĂM TRẠNG THÁI 5s/LẦN
        [HttpGet]
        public IActionResult CheckOrderStatus(int orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false });

            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId && o.UserId == userId);
            if (order == null) return Json(new { success = false });

            return Json(new
            {
                success = true,
                status = order.Status
            });
        }
        // 🔥 API ĐỂ TẠO THANH BANNER CHẠY TRỰC TIẾP TRÊN ĐẦU TRANG WEB
        [HttpGet]
        public IActionResult GetLiveOrderBanner()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { hasOrder = false });

            // Tìm đơn hàng gần nhất chưa hoàn thành
            var activeOrder = _context.Orders
                .Where(o => o.UserId == userId && o.Status != "Completed" && o.Status != "Cancelled")
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefault();

            if (activeOrder == null) return Json(new { hasOrder = false });

            // Dịch trạng thái sang câu văn tự nhiên
            string statusMessage = activeOrder.Status switch
            {
                "Pending" => "Đơn hàng đang chờ quán xác nhận...",
                "Preparing" => "Quán đang chuẩn bị món cho bạn...",
                "Shipping" => "Tài xế đang trên đường đến quán...",
                "Arrived" => "Tài xế đã đến quán, đang chờ lấy món...",
                "Delivering" => "Tài xế đã lấy món, đang trên đường giao đến bạn!",
                _ => "Đang xử lý đơn hàng..."
            };

            return Json(new
            {
                hasOrder = true,
                orderId = activeOrder.OrderId,
                message = statusMessage
            });
        }
    }
}
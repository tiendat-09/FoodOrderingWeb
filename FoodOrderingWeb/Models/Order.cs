using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodOrderingWeb.Models
{
    public partial class Order
    {
        public int OrderId { get; set; }
        public int UserId { get; set; } // ai là đặt -> bảng User
        public int? StoreId { get; set; } // nấu quán nào -> bảng Store
        public DateTime? OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Note { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal ShippingFee { get; set; }
        public int? DriverId { get; set; } // ai ship -> bảng User role Driver
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }

        public int? StoreRating { get; set; }
        public string? StoreReview { get; set; }
        public int? DriverRating { get; set; }
        public string? DriverReview { get; set; }

        public DateTime? AcceptTime { get; set; }
        public DateTime? PickupTime { get; set; }
        public DateTime? DeliveryTime { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
        public virtual Store? Store { get; set; }

        [ForeignKey("DriverId")]
        public virtual User? Driver { get; set; }
        public string? CancelReason { get; set; }
    }
}
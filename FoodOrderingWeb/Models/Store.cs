using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodOrderingWeb.Models
{
    [Table("Stores")]
    public class Store
    {
        [Key]
        public int StoreId { get; set; }

        [Required(ErrorMessage = "Tên cửa hàng không được để trống")]
        public string StoreName { get; set; }

        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ImageUrl { get; set; }

        public bool? IsActive { get; set; } = false;
        public bool? IsOpen { get; set; } = true;
        public bool? IsHighlyRated { get; set; }
        public string? OperatingDays { get; set; }

        public string? OpeningTime { get; set; }
        public string? ClosingTime { get; set; }

        public string? BankName { get; set; }
        public string? BankNumber { get; set; }
        public string? BankOwner { get; set; }

        public double Rating { get; set; } = 0;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? RejectReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? OwnerId { get; set; }
        [ForeignKey("OwnerId")]
        public virtual User? Owner { get; set; }

        public virtual ICollection<Food> Foods { get; set; } = new List<Food>();
    }
}
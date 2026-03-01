using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodOrderingWeb.Models
{
    public partial class User
    {
        public User()
        {
            CustomerOrders = new HashSet<Order>();
            DriverOrders = new HashSet<Order>();
        }

        [Key]
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Avatar { get; set; }
        public string? ResetToken { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string? CitizenId { get; set; } // CCCD cho Driver
        public string? LicensePlate { get; set; } // Biển số cho Driver

        public string? RejectReason { get; set; }

        public string? RejectionMessage { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Order> CustomerOrders { get; set; } = new HashSet<Order>();

        [InverseProperty("Driver")]
        public virtual ICollection<Order> DriverOrders { get; set; } = new HashSet<Order>();
    }
}
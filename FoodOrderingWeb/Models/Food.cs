using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodOrderingWeb.Models
{
    [Table("Foods")]
    public class Food
    {
        [Key]
        public int FoodId { get; set; }

        [Required]
        public string FoodName { get; set; }

        public string? Description { get; set; }

        public double Price { get; set; } // giá hiện tại

        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public int? StoreId { get; set; }
        [ForeignKey("StoreId")]
        public virtual Store? Store { get; set; }

        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
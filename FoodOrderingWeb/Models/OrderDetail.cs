using System;
using System.Collections.Generic;

namespace FoodOrderingWeb.Models;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int OrderId { get; set; }

    public int FoodId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; } // thêm để tăng giá món thì kh thay đổi

    public string? Note { get; set; }

    public virtual Food Food { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
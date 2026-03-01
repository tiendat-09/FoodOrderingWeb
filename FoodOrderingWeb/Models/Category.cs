using System;
using System.Collections.Generic;

namespace FoodOrderingWeb.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<Food> Foods { get; set; } = new List<Food>();
}

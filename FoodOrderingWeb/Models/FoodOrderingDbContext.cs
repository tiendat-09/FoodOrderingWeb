using Microsoft.EntityFrameworkCore;

namespace FoodOrderingWeb.Models
{
    public partial class FoodOrderingDbContext : DbContext
    {
        public FoodOrderingDbContext(DbContextOptions<FoodOrderingDbContext> options)
            : base(options)
        {
        }

        // Khai báo các DbSet (bạn giữ nguyên các DbSet hiện tại của bạn)
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Food> Foods { get; set; }
        public virtual DbSet<Order> Orders { get; set; }
        public virtual DbSet<OrderDetail> OrderDetails { get; set; }
        public virtual DbSet<Store> Stores { get; set; }
        public virtual DbSet<User> Users { get; set; }

        // 🔥 ĐÂY LÀ PHẦN QUAN TRỌNG NHẤT ĐỂ SỬA LỖI
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Chỉ định rõ: Mối quan hệ giữa Khách hàng và Đơn hàng
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.CustomerOrders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. Chỉ định rõ: Mối quan hệ giữa Tài xế và Đơn hàng
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Driver)
                .WithMany(u => u.DriverOrders)
                .HasForeignKey(o => o.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
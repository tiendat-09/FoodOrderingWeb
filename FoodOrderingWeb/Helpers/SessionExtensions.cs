using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace FoodOrderingWeb.Helpers
{
    // Class này mở rộng chức năng cho Session để lưu được Object (List giỏ hàng)
    public static class SessionExtensions
    {
        // Hàm lưu Object vào Session
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        // Hàm lấy Object từ Session
        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}
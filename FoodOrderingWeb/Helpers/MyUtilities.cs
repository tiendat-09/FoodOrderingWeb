using System.Security.Cryptography;
using System.Text;

namespace FoodOrderingWeb.Helpers
{
    public class MyUtilities
    {
        // mã hóa mật khẩu đơn giản
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";

            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(password);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert byte array to hex string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
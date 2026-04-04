using System;
using System.Security.Cryptography;
using System.Text;

class Program {
    static void Main() {
        var secret = "TEST_SECRET_FOR_DEVELOPMENT";
        var payload = "{\"order_nsu\": \"123\", \"status\": \"paid\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        Console.WriteLine($"Payload: {payload}");
        Console.WriteLine($"Signature: {signature}");
    }
}

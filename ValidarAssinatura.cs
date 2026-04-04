using System;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main()
    {
        // Exemplo de como validar a assinatura no seu Controller
        string webhookSecret = "SEU_SECRET_AQUI"; // O mesmo _webhookSecret
        string rawBody = "{\"order_nsu\": \"123\", \"status\": \"paid\"}"; // O corpo exato recebido
        string receivedSignature = "ASSINATURA_RECEBIDA_DO_HEADER"; // Header X-InfinitePay-Signature

        bool isValid = VerifyHmac(rawBody, receivedSignature, webhookSecret);
        
        Console.WriteLine($"Assinatura válida: {isValid}");
    }

    private static bool VerifyHmac(string body, string signature, string secret)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var expected = Convert.ToHexString(hash).ToLowerInvariant();

            // Comparação de tempo constante para evitar ataques de temporização
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant())
            );
        }
        catch
        {
            return false;
        }
    }
}

using Microsoft.Extensions.Configuration;

public class AuthConfig
{
    public string Key { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    
    public AuthConfig(IConfiguration configuration)
    {
        Key = configuration["Jwt:Key"];
        Issuer = configuration["Jwt:Issuer"];
        Audience = configuration["Jwt:Audience"];
    }
}
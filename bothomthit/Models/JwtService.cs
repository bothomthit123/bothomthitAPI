using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

//Model này dùng để tạo và xác thực JWT
public interface IJwtService
{
    string CreateToken(int accountId, string email, string role);
    ClaimsPrincipal? ValidateToken(string token); // optional 
}


public class JwtOptions
{
    public string Issuer { get; set; } = "TourismApp";
    public string Audience { get; set; } = "TourismAppClient";
    public string Secret { get; set; } = "super_secret_key_bothomthit_awsd";
    public int ExpireMinutes { get; set; } = 720; // 12h
}

public class JwtService : IJwtService
{
    private readonly JwtOptions _opt;
    private readonly byte[] _key;

    public JwtService(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;
        _key = Encoding.UTF8.GetBytes(_opt.Secret); //chuyển secret thành mảng byte
    }

    public string CreateToken(int accountId, string email, string role) //tạo claim và token
    {
        var claims = new[]
        {
            new Claim("account_id", accountId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
        };

        var cred = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256); //tạo chữ ký
        var jwt = new JwtSecurityToken(
            issuer: _opt.Issuer,//nhà phát hành
            audience: _opt.Audience,//đối tượng sử dụng
            claims: claims,//danh sách claim
            notBefore: DateTime.UtcNow,//thời gian có hiệu lực
            expires: DateTime.UtcNow.AddMinutes(_opt.ExpireMinutes),//thời gian hết hạn
            signingCredentials: cred//chữ ký
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt); 
    }

    public ClaimsPrincipal? ValidateToken(string token) //xác thực token
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _opt.Issuer,
                ValidateAudience = true,
                ValidAudience = _opt.Audience,
                ValidateIssuerSigningKey = true, 
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch { return null; }
    }
}

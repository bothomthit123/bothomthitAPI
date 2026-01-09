using System.Security.Claims;
using bothomthit.Models;  
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory; // Thêm thư viện Cache

namespace TourismApp.Api.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IEmailService _emailService; 
    private readonly IMemoryCache _cache;         

    public AccountController(
        AppDbContext db,
        IJwtService jwt,
        IEmailService emailService,
        IMemoryCache cache)
    {
        _db = db;
        _jwt = jwt;
        _emailService = emailService;
        _cache = cache;
    }

    // DTO cho Login/Update 
    public record LoginRequest(string Email, string Password);
    public record UpdateProfileRequest(string? Name, string? Email);

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static int GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var s = user.FindFirstValue("account_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : 0;
    }

    
    //  API GỬI OTP (GỌI TRƯỚC KHI ĐĂNG KÝ)
    
    [HttpPost("send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req)
    {
        var email = NormalizeEmail(req.Email);

        // Kiểm tra email đã tồn tại chưa
        if (await _db.Accounts.AnyAsync(a => a.Email == email))
        {
            return Conflict(new { error = "email_exists", message = "Email này đã được sử dụng." });
        }

        // Tạo mã OTP 6 số ngẫu nhiên
        var otpCode = new Random().Next(100000, 999999).ToString();

        // Lưu vào Cache: Key là "OTP_email", Value là mã OTP, Hết hạn sau 5 phút
        _cache.Set($"OTP_{email}", otpCode, TimeSpan.FromMinutes(5));

        // Gửi Email (Bọc try-catch để tránh crash nếu mail server lỗi)
        try
        {
            string subject = "Mã xác thực đăng ký Tourism App";
            string body = $@"
                <h3>Xin chào,</h3>
                <p>Mã xác thực (OTP) của bạn là: <b style='font-size:24px;color:#007bff'>{otpCode}</b></p>
                <p>Mã này có hiệu lực trong vòng 5 phút. Vui lòng không chia sẻ cho người khác.</p>";

            await _emailService.SendEmailAsync(email, subject, body);

            return Ok(new { message = "OTP sent successfully" });
        }
        catch (Exception ex)
        {
            // Xóa cache nếu gửi mail thất bại
            _cache.Remove($"OTP_{email}");
            return StatusCode(500, new { error = "mail_error", message = ex.Message });
        }
    }

    
    // API ĐĂNG KÝ (CÓ KIỂM TRA OTP + TRANSACTION)
    
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var email = NormalizeEmail(req.Email);
        var role = string.IsNullOrWhiteSpace(req.Role) ? "User" : req.Role!;

        // --- BƯỚC 1: KIỂM TRA OTP ---
        if (!_cache.TryGetValue($"OTP_{email}", out string? cachedOtp))
        {
            return BadRequest(new { error = "otp_expired", message = "Mã OTP đã hết hạn hoặc không tồn tại. Vui lòng gửi lại." });
        }

        if (cachedOtp != req.Otp)
        {
            return BadRequest(new { error = "otp_invalid", message = "Mã OTP không chính xác." });
        }

        // --- BƯỚC 2: KIỂM TRA EMAIL TỒN TẠI (Double check) ---
        if (await _db.Accounts.AnyAsync(a => a.Email == email))
            return Conflict(new { error = "email_exists" });

        // --- BƯỚC 3: TIẾN HÀNH TẠO TÀI KHOẢN (DATABASE TRANSACTION) ---
        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            Account acc;
            if (role.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            {
                // Tạo Supplier
                var newSupplier = new Supplier
                {
                    Name = req.Name,
                    ContactInfo = req.Email
                };
                _db.Suppliers.Add(newSupplier);
                await _db.SaveChangesAsync();

                // Tạo Account Supplier
                acc = new Account
                {
                    Name = req.Name,
                    Email = email,
                    PasswordHash = req.Password, 
                    Role = "Supplier",
                    SupplierId = newSupplier.SupplierId
                };
            }
            else
            {
                // Tạo Account User thường
                acc = new Account
                {
                    Name = req.Name,
                    Email = email,
                    PasswordHash = req.Password,
                    Role = "User",
                    SupplierId = null
                };
            }

            _db.Accounts.Add(acc);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            // --- BƯỚC 4: DỌN DẸP CACHE ---
            // Xóa OTP sau khi đăng ký thành công để không thể dùng lại
            _cache.Remove($"OTP_{email}");

            return StatusCode(201, new { data = new { acc.AccountId, acc.Email, acc.Role } });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { error = "internal_server_error", message = ex.Message });
        }
    }

    // API ĐĂNG NHẬP
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var email = NormalizeEmail(req.Email);

        // Tìm tài khoản theo email
        var acc = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email == email);

        // 1. Kiểm tra tài khoản có tồn tại không
        if (acc == null)
            return Unauthorized(new { error = "invalid_credentials", message = "Email hoặc mật khẩu không chính xác." });

        // 2. [MỚI] Kiểm tra trạng thái khóa (IsLocked)
        if (acc.IsLocked) // Nếu IsLocked == true
        {
            // Trả về lỗi 403 Forbidden hoặc 401 tùy chính sách, kèm thông báo rõ ràng
            return StatusCode(403, new
            {
                error = "account_locked",
                message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên."
            });
        }

        // 3. Kiểm tra mật khẩu
        
        var passwordValid = acc.PasswordHash == req.Password;

        if (!passwordValid)
            return Unauthorized(new { error = "invalid_credentials", message = "Email hoặc mật khẩu không chính xác." });

        // 4. Tạo Token nếu mọi thứ hợp lệ
        var token = _jwt.CreateToken(acc.AccountId, acc.Email, acc.Role);

        return Ok(new
        {
            data = new
            {
                accountId = acc.AccountId,
                email = acc.Email,
                role = acc.Role,
                token
            }
        });
    }

    // API LẤY THÔNG TIN PROFILE CỦA TÔI
    [HttpGet("/me/profile")]
    [Authorize]
    public async Task<IActionResult> MeProfile()
    {
        var uid = GetUserIdFromClaims(User);
        if (uid <= 0) return Unauthorized(new { error = "invalid_token" });

        var acc = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == uid);
        if (acc == null) return NotFound(new { error = "account_not_found" });

        return Ok(new
        {
            data = new
            {
                accountId = acc.AccountId,
                name = acc.Name,
                email = acc.Email,
                role = acc.Role,
                supplierId = acc.SupplierId,
                createdAtUtc = acc.CreatedAtUtc
            }
        });
    }

    // API CẬP NHẬT THÔNG TIN PROFILE CỦA TÔI
    [HttpPut("{id:int}/profile")]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateProfileRequest req)
    {
        var acc = await _db.Accounts.FindAsync(id);
        if (acc == null) return NotFound(new { error = "account_not_found" });

        if (!string.IsNullOrWhiteSpace(req.Name)) acc.Name = req.Name!;
        if (!string.IsNullOrWhiteSpace(req.Email)) acc.Email = NormalizeEmail(req.Email);

        await _db.SaveChangesAsync();
        return Ok(new { data = new { acc.AccountId, acc.Name, acc.Email } });
    }

    // API LẤY THÔNG TIN TÀI KHOẢN THEO ID
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAccount(int id)
    {
        var acc = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == id);
        if (acc == null) return NotFound();

        return Ok(new
        {
            data = new { acc.AccountId, acc.Name, acc.Email, acc.Role, acc.CreatedAtUtc }
        });
    }
}
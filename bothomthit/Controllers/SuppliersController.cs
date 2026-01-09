using bothomthit.Models; // Đảm bảo namespace này đúng với project của bạn
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace TourismApp.Api.Controllers;


[ApiController]
[Route("api/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _db;
    public SuppliersController(AppDbContext db) => _db = db;

    // THÊM ACTION MỚI ĐỂ LẤY DANH SÁCH CHO TRANG ĐĂNG KÝ
    // Trả về danh sách các nhà cung cấp để hiển thị trên form đăng ký
    [HttpGet]
    public async Task<IActionResult> GetAllSuppliers()
    {
        var suppliers = await _db.Suppliers
            .AsNoTracking()
            .Select(s => new { s.SupplierId, s.Name }) // Chỉ lấy ID và Tên cho nhẹ
            .ToListAsync();

        return Ok(new { data = suppliers });
    }
    
    // Lấy thông tin chi tiết của một nhà cung cấp
    [HttpGet("{supplierId:int}")]
    public async Task<IActionResult> Get(int supplierId)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == supplierId);
        return s == null ? NotFound() : Ok(new { data = s });
    }

    
    // Tạo một nhà cung cấp mới (dành cho Admin)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Supplier req)
    {
        req.SupplierId = 0;
        await _db.Suppliers.AddAsync(req);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { data = new { supplierId = req.SupplierId } });
    }

    
    // Cập nhật thông tin nhà cung cấp (dành cho Admin)
    [HttpPut("{supplierId:int}")]
    public async Task<IActionResult> Update(int supplierId, [FromBody] Supplier req)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == supplierId);
        if (s == null) return NotFound();
        s.Name = req.Name ?? s.Name;
        s.ContactInfo = req.ContactInfo ?? s.ContactInfo;
        await _db.SaveChangesAsync();
        return Ok(new { data = s });
    }
    
    // Lấy danh sách các địa điểm thuộc về một Supplier cụ thể
    [HttpGet("{supplierId:int}/places")]
    [Authorize] // Bắt buộc phải có token hợp lệ để gọi
    public async Task<IActionResult> GetPlacesForSupplier(int supplierId)
    {
        // Lấy thông tin user từ token để kiểm tra quyền (cho supplier)
        var claimsUserIdString = User.FindFirstValue("account_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claimsUserIdString, out var claimsUserId))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        // Tìm tài khoản tương ứng với token
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == claimsUserId);
        if (account == null || account.Role != "Supplier" || account.SupplierId != supplierId)
        {
            // Nếu user không phải là Supplier, hoặc user không sở hữu supplierId này -> Cấm truy cập
            return Forbid();
        }

        // Lấy danh sách địa điểm
        var places = await _db.Places
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId && !p.IsDeleted) 
            .ToListAsync();

        return Ok(new { data = places });
    }
    
}


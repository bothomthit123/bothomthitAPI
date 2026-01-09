using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; 
using System.Text;
using TourismApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) URLs 
builder.WebHost.UseUrls(
    "http://localhost:5022",
    "http://localhost:7103",
    "http://0.0.0.0:5022",
    "http://0.0.0.0:7103"
);

// 2) Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- PHẦN SWAGGER ---
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "TourismApp API", Version = "v1" });

    // Cấu hình nút Authorize 
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Nhập Token vào đây (Chỉ cần dán chuỗi token, không cần gõ Bearer)",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });

    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});
// -----------------------------

builder.Services.AddMemoryCache(); 
builder.Services.AddScoped<IEmailService, EmailService>(); 

// 3) CORS 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// 4) DbContext 
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 5) JWT 
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtService, JwtService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSection["Issuer"] ?? "TourismApp";
var jwtAudience = jwtSection["Audience"] ?? "TourismAppClient";
var jwtSecret = jwtSection["Secret"] ?? "super_secret_key_please_replace_min32chars";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero,

            
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<RecommendationService>(); 

var app = builder.Build();

// 6) Kiểm tra DB 
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var can = await db.Database.CanConnectAsync();
        Console.WriteLine(can ? "Kết nối database thành công!" : "Không thể kết nối database!");
        Console.WriteLine($"DB Provider: {db.Database.ProviderName}");
        Console.WriteLine($"DB Name    : {db.Database.GetDbConnection().Database}");
        Console.WriteLine($"DB Server  : {db.Database.GetDbConnection().DataSource}");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Lỗi khi kết nối database:");
        Console.WriteLine(ex.ToString());
    }
}

// 7) Pipeline



if (app.Environment.IsDevelopment())



{



    app.UseSwagger();



    app.UseSwaggerUI();



}



else



{



    app.UseHttpsRedirection();



}







app.UseCors("AllowAll");







// Thứ tự quan trọng



app.UseAuthentication();



app.UseAuthorization();







app.MapControllers();

// 8) Log URLs 
var addrs = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
if (addrs != null)
{
    Console.WriteLine(" Now listening on:");
    foreach (var url in addrs.Addresses) Console.WriteLine($" - {url}");
}

app.Run();
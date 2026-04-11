using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
<<<<<<< HEAD
=======
using Microsoft.AspNetCore.Authorization;
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using VinhKhanh.Domain.Entities;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized("Tài khoản hoặc mật khẩu không chính xác.");
        }

        if (!user.IsApproved)
        {
            return StatusCode(403, "Tài khoản của bạn đang chờ Admin duyệt.");
        }

        var userRoles = await userManager.GetRolesAsync(user);
        var authClaims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("ActivationDate", user.ActivationDate.ToString("o"))
        };

        foreach (var userRole in userRoles)
        {
            authClaims.Add(new Claim(ClaimTypes.Role, userRole));
        }

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "VinhKhanh_CleanArchitecture_Super_Secret_Key_2026"));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            expires: DateTime.Now.AddHours(24),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expiration = token.ValidTo,
            roles = userRoles
        });
    }

    [HttpPost("register-shop")]
    public async Task<IActionResult> RegisterShop([FromBody] RegisterShopRequest request)
    {
        var userExists = await userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return StatusCode(500, "Tài khoản với Email này đã tồn tại!");

        ApplicationUser user = new()
        {
            Email = request.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserName = request.Email,
            FullName = request.FullName,
            IsApproved = false // Chủ quán cần Admin duyệt
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return StatusCode(500, "Khởi tạo tài khoản thất bại. Kiểm tra lại quy tắc mật khẩu.");

        await userManager.AddToRoleAsync(user, "ShopOwner");

        return Ok(new { success = true, message = "Đăng ký thành công! Vui lòng chờ Admin duyệt để có thể đăng nhập." });
    }

    [HttpPost("register-visitor")]
    public async Task<IActionResult> RegisterVisitor([FromBody] RegisterVisitorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest("Email, mật khẩu và họ tên không được để trống.");

        if (request.Password.Length < 6)
            return BadRequest("Mật khẩu phải có ít nhất 6 ký tự.");

        var userExists = await userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return Conflict(new { error = "Email đã được sử dụng." });

        var user = new ApplicationUser
        {
            Email = request.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserName = request.Email,
            FullName = request.FullName,
            IsApproved = true, // Visitor tự động kích hoạt
            ActivationDate = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return StatusCode(500, "Khởi tạo tài khoản thất bại. Kiểm tra lại quy tắc mật khẩu.");

        await userManager.AddToRoleAsync(user, "Visitor");

        return Ok(new { success = true, message = "Đăng ký thành công!" });
    }
<<<<<<< HEAD
=======

    /// <summary>
    /// Người dùng đang đăng nhập tự đổi mật khẩu của mình.
    /// Yêu cầu cung cấp mật khẩu cũ để xác thực.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Mật khẩu hiện tại và mật khẩu mới không được để trống." });

        if (request.NewPassword.Length < 6)
            return BadRequest(new { error = "Mật khẩu mới phải có ít nhất 6 ký tự." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await userManager.FindByIdAsync(userId ?? string.Empty);
        if (user is null)
            return NotFound(new { error = "Không tìm thấy tài khoản." });

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { error = "Đổi mật khẩu thất bại.", details = errors });
        }

        return Ok(new { success = true, message = "Đổi mật khẩu thành công." });
    }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterShopRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class RegisterVisitorRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

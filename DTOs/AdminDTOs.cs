using System.ComponentModel.DataAnnotations;

namespace SocketIR.API.DTOs;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Class { get; set; }
    public string? Department { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}

public class CreateUserDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    [RegularExpression("^(Student|Teacher|Admin)$", ErrorMessage = "Vai trò không hợp lệ")]
    public string Role { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Lớp không được vượt quá 50 ký tự")]
    public string? Class { get; set; }

    [StringLength(100, ErrorMessage = "Bộ môn không được vượt quá 100 ký tự")]
    public string? Department { get; set; }
}

public class UpdateUserDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    [RegularExpression("^(Student|Teacher|Admin)$", ErrorMessage = "Vai trò không hợp lệ")]
    public string Role { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Lớp không được vượt quá 50 ký tự")]
    public string? Class { get; set; }

    [StringLength(100, ErrorMessage = "Bộ môn không được vượt quá 100 ký tự")]
    public string? Department { get; set; }
} 
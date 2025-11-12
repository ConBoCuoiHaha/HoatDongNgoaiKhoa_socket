using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SocketIR.API.Models;

namespace SocketIR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user == null)
                {
                    return BadRequest("Email hoặc mật khẩu không đúng");
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);
                if (!result.Succeeded)
                {
                    return BadRequest("Email hoặc mật khẩu không đúng");
                }

                var token = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();

                var response = new LoginResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email!,
                        Role = user.Role.ToString(),
                        Class = user.Class,
                        Department = user.Department
                    }
                };

                _logger.LogInformation($"User {user.Email} logged in successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, "Có lỗi xảy ra trong quá trình đăng nhập");
            }
        }

        [HttpPost("register")]
        public async Task<ActionResult<LoginResponseDto>> Register(RegisterDto registerDto)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
                if (existingUser != null)
                {
                    return BadRequest("Email này đã được sử dụng");
                }

                // Validate role
                if (!Enum.TryParse<UserRole>(registerDto.Role, true, out var userRole))
                {
                    return BadRequest("Vai trò không hợp lệ");
                }

                // Create new user
                var user = new User
                {
                    UserName = registerDto.Email,
                    Email = registerDto.Email,
                    FullName = registerDto.FullName,
                    Role = userRole,
                    Class = userRole == UserRole.Student ? registerDto.Class : null,
                    Department = userRole == UserRole.Teacher ? registerDto.Department : null,
                    EmailConfirmed = true // For demo purposes
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return BadRequest($"Không thể tạo tài khoản: {errors}");
                }

                // Add user to role
                await _userManager.AddToRoleAsync(user, userRole.ToString());

                // Generate token and return login response
                var token = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();

                var response = new LoginResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Role = user.Role.ToString(),
                        Class = user.Class,
                        Department = user.Department
                    }
                };

                _logger.LogInformation($"User {user.Email} registered successfully");
                return CreatedAtAction(nameof(GetProfile), new { }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, "Có lỗi xảy ra trong quá trình đăng ký");
            }
        }

        [HttpGet("profile")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<UserDto>> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("Không tìm thấy người dùng");
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email!,
                    Role = user.Role.ToString(),
                    Class = user.Class,
                    Department = user.Department
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, "Có lỗi xảy ra khi lấy thông tin người dùng");
            }
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken(RefreshTokenDto refreshTokenDto)
        {
            try
            {
                // For demo purposes, we'll validate the refresh token and generate a new access token
                // In production, you should store and validate refresh tokens properly
                
                var principal = GetPrincipalFromExpiredToken(refreshTokenDto.Token);
                if (principal == null)
                {
                    return BadRequest("Token không hợp lệ");
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("Token không hợp lệ");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return BadRequest("Người dùng không tồn tại");
                }

                var newToken = GenerateJwtToken(user);
                var newRefreshToken = GenerateRefreshToken();

                var response = new LoginResponseDto
                {
                    Token = newToken,
                    RefreshToken = newRefreshToken,
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email!,
                        Role = user.Role.ToString(),
                        Class = user.Class,
                        Department = user.Department
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, "Có lỗi xảy ra khi làm mới token");
            }
        }

        [HttpPost("logout")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                return Ok(new { message = "Đăng xuất thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, "Có lỗi xảy ra trong quá trình đăng xuất");
            }
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "your-super-secret-key-that-is-at-least-32-characters-long");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email!),
                new("Role", user.Role.ToString()),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            // Add additional claims
            if (!string.IsNullOrEmpty(user.Class))
            {
                claims.Add(new Claim("Class", user.Class));
            }

            if (!string.IsNullOrEmpty(user.Department))
            {
                claims.Add(new Claim("Department", user.Department));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24), // 24 hours
                Issuer = _configuration["Jwt:Issuer"] ?? "SocketIR.API",
                Audience = _configuration["Jwt:Audience"] ?? "SocketIR.Client",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "your-super-secret-key-that-is-at-least-32-characters-long")),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            
            if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }

    // DTOs
    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Student"; // Student, Teacher, Admin
        public string? Class { get; set; } // For students
        public string? Department { get; set; } // For teachers
    }

    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Class { get; set; }
        public string? Department { get; set; }
    }

    public class RefreshTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
} 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocketIR.API.Data;
using SocketIR.API.Models;
using SocketIR.API.DTOs;

namespace SocketIR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<User> userManager, 
        ApplicationDbContext context,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(
        [FromQuery] string? role = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.Users.AsQueryable();

        // Filter by role
        if (!string.IsNullOrEmpty(role))
        {
            if (Enum.TryParse<UserRole>(role, out var userRole))
            {
                query = query.Where(u => u.Role == userRole);
            }
        }

        // Search by name or email
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userDtos = users.Select(u => {
            var dto = new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role.ToString(),
                Class = u.Class,
                Department = u.Department
            };
            
            _logger.LogDebug("User {Email}: DB Role={DbRole}, DTO Role={DtoRole}", 
                u.Email, u.Role, dto.Role);
                
            return dto;
        }).ToList();

        return Ok(new { Users = userDtos, TotalCount = totalCount });
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
    {
        try
        {
            _logger.LogInformation("Attempting to create user with email: {Email}, role: {Role}", 
                createUserDto.Email, createUserDto.Role);
                
            if (!Enum.TryParse<UserRole>(createUserDto.Role, out var userRole))
            {
                _logger.LogWarning("Invalid role specified: {Role}", createUserDto.Role);
                return BadRequest("Invalid role specified.");
            }

            var user = new User
            {
                UserName = createUserDto.Email,
                Email = createUserDto.Email,
                FullName = createUserDto.FullName,
                Role = userRole,
                Class = createUserDto.Class,
                Department = createUserDto.Department,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, createUserDto.Password);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Admin created new user: {Email} with role: {Role} (enum value: {EnumValue})", 
                    createUserDto.Email, createUserDto.Role, user.Role);

                // Double-check user was saved correctly
                var savedUser = await _userManager.FindByEmailAsync(user.Email);
                if (savedUser != null)
                {
                    _logger.LogInformation("Verified saved user: {Email} has role: {SavedRole} (enum value: {SavedEnumValue})", 
                        savedUser.Email, savedUser.Role.ToString(), savedUser.Role);
                }

                return Ok(new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role.ToString(),
                    Class = user.Class,
                    Department = user.Department
                });
            }
            
            _logger.LogWarning("Failed to create user {Email}. Errors: {Errors}", 
                createUserDto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "Lỗi tạo tài khoản người dùng");
        }
    }

    [HttpPut("users/{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(string id, [FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng");
            }

            user.FullName = updateUserDto.FullName;
            
            if (!string.IsNullOrEmpty(updateUserDto.Role))
            {
                if (!Enum.TryParse<UserRole>(updateUserDto.Role, out var userRole))
                {
                    return BadRequest("Invalid role specified.");
                }
                user.Role = userRole;
            }
            
            user.Class = updateUserDto.Class;
            user.Department = updateUserDto.Department;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(updateUserDto.Email) && updateUserDto.Email != user.Email)
            {
                user.Email = updateUserDto.Email;
                user.UserName = updateUserDto.Email;
            }

            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Admin updated user: {UserId}", id);

                return Ok(new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role.ToString(),
                    Class = user.Class,
                    Department = user.Department
                });
            }

            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            return StatusCode(500, "Lỗi cập nhật thông tin người dùng");
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<ActionResult> DeleteUser(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng");
            }

            // Prevent admin from deleting themselves
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                return BadRequest("Không thể xóa tài khoản của chính mình");
            }

            var result = await _userManager.DeleteAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Admin deleted user: {UserId}", id);
                return Ok("Xóa người dùng thành công");
            }

            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return StatusCode(500, "Lỗi xóa người dùng");
        }
    }

    [HttpPost("users/{id}/toggle-lock")]
    public async Task<ActionResult> ToggleUserLock(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng");
            }

            // Prevent admin from locking themselves
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                return BadRequest("Không thể khóa tài khoản của chính mình");
            }

            if (user.LockoutEnd == null || user.LockoutEnd <= DateTime.UtcNow)
            {
                // Lock user for 1 year
                await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddYears(1));
                _logger.LogInformation("Admin locked user: {UserId}", id);
                return Ok("Đã khóa tài khoản người dùng");
            }
            else
            {
                // Unlock user
                await _userManager.SetLockoutEndDateAsync(user, null);
                _logger.LogInformation("Admin unlocked user: {UserId}", id);
                return Ok("Đã mở khóa tài khoản người dùng");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling user lock");
            return StatusCode(500, "Lỗi cập nhật trạng thái khóa tài khoản");
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult> GetStatistics()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student);
            var totalTeachers = await _context.Users.CountAsync(u => u.Role == UserRole.Teacher);
            var totalAdmins = await _context.Users.CountAsync(u => u.Role == UserRole.Admin);
            
            var totalActivities = await _context.Activities.CountAsync();
            var activeActivities = await _context.Activities.CountAsync(a => a.Status == ActivityStatus.Open);
            
            var totalRegistrations = await _context.Registrations.CountAsync();
            var pendingRegistrations = await _context.Registrations.CountAsync(r => r.Status == RegistrationStatus.Pending);

            var recentActivities = await _context.Activities
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new { a.Id, a.Title, a.CreatedAt, a.CurrentParticipants, a.MaxParticipants })
                .ToListAsync();

            return Ok(new
            {
                UserStats = new
                {
                    TotalUsers = totalUsers,
                    Students = totalStudents,
                    Teachers = totalTeachers,
                    Admins = totalAdmins
                },
                ActivityStats = new
                {
                    TotalActivities = totalActivities,
                    ActiveActivities = activeActivities
                },
                RegistrationStats = new
                {
                    TotalRegistrations = totalRegistrations,
                    PendingRegistrations = pendingRegistrations
                },
                RecentActivities = recentActivities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics");
            return StatusCode(500, "Lỗi lấy thống kê");
        }
    }

    [HttpGet("activities")]
    public async Task<ActionResult> GetAllActivities(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Activities
                .Include(a => a.Registrations)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ActivityStatus>(status, out var statusEnum))
            {
                query = query.Where(a => a.Status == statusEnum);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.Title.Contains(search) || a.Description.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var activities = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.StartTime,
                    a.EndTime,
                    a.Location,
                    a.MaxParticipants,
                    a.CurrentParticipants,
                    a.Status,
                    a.Category,
                    a.CreatedAt,
                    CreatorName = _context.Users.Where(u => u.Id == a.CreatorId).Select(u => u.FullName).FirstOrDefault(),
                    RegistrationsCount = a.Registrations.Count
                })
                .ToListAsync();

            return Ok(new { Activities = activities, TotalCount = totalCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all activities");
            return StatusCode(500, "Lỗi lấy danh sách hoạt động");
        }
    }

    [HttpDelete("activities/{id}")]
    public async Task<ActionResult> DeleteActivity(int id)
    {
        try
        {
            var activity = await _context.Activities
                .Include(a => a.Registrations)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
            {
                return NotFound("Không tìm thấy hoạt động");
            }

            // Remove all registrations first
            _context.Registrations.RemoveRange(activity.Registrations);
            _context.Activities.Remove(activity);
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin deleted activity: {ActivityId}", id);
            return Ok("Xóa hoạt động thành công");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting activity");
            return StatusCode(500, "Lỗi xóa hoạt động");
        }
    }
} 
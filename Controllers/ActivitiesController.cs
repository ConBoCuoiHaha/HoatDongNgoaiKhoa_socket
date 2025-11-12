using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SocketIR.API.Data;
using SocketIR.API.Models;
using SocketIR.API.Services;

namespace SocketIR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ActivitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ActivitiesController> _logger;

        public ActivitiesController(
            ApplicationDbContext context,
            INotificationService notificationService,
            ILogger<ActivitiesController> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: api/activities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetActivities(
            [FromQuery] string? category = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] ActivityStatus? status = null,
            [FromQuery] bool includeCreator = false)
        {
            try
            {
                var query = _context.Activities.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(a => a.Category == category);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(a => a.StartTime >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(a => a.EndTime <= endDate.Value);
                }

                if (status.HasValue)
                {
                    query = query.Where(a => a.Status == status.Value);
                }

                if (includeCreator)
                {
                    query = query.Include(a => a.Creator);
                }

                var activities = await query
                    .OrderBy(a => a.StartTime)
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
                        a.RequireApproval,
                        a.Category,
                        a.CreatedAt,
                        Creator = includeCreator ? new { a.Creator.FullName, a.Creator.Email } : null,
                        IsRegistered = _context.Registrations.Any(r => r.ActivityId == a.Id && r.StudentId == User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
                        CanRegister = a.Status == ActivityStatus.Open && a.CurrentParticipants < a.MaxParticipants,
                        IsFull = a.CurrentParticipants >= a.MaxParticipants
                    })
                    .ToListAsync();

                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activities");
                return StatusCode(500, "Có lỗi xảy ra khi lấy danh sách hoạt động");
            }
        }

        // GET: api/activities/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetActivity(int id, [FromQuery] bool includeRegistrations = false)
        {
            try
            {
                var query = _context.Activities
                    .Include(a => a.Creator)
                    .Where(a => a.Id == id);

                if (includeRegistrations)
                {
                    query = query.Include(a => a.Registrations)
                                 .ThenInclude(r => r.Student);
                }

                var activity = await query.FirstOrDefaultAsync();

                if (activity == null)
                {
                    return NotFound("Không tìm thấy hoạt động");
                }

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                var result = new
                {
                    activity.Id,
                    activity.Title,
                    activity.Description,
                    activity.StartTime,
                    activity.EndTime,
                    activity.Location,
                    activity.MaxParticipants,
                    activity.CurrentParticipants,
                    activity.Status,
                    activity.RequireApproval,
                    activity.Category,
                    activity.CreatedAt,
                    Creator = new { activity.Creator.FullName, activity.Creator.Email, activity.Creator.Department },
                    IsCreator = activity.CreatorId == currentUserId,
                    CanEdit = userRole == "Admin" || activity.CreatorId == currentUserId,
                    IsRegistered = currentUserId != null && _context.Registrations.Any(r => r.ActivityId == id && r.StudentId == currentUserId),
                    CanRegister = activity.Status == ActivityStatus.Open && activity.CurrentParticipants < activity.MaxParticipants && userRole == "Student",
                    IsFull = activity.CurrentParticipants >= activity.MaxParticipants,
                    Registrations = includeRegistrations && (userRole == "Admin" || activity.CreatorId == currentUserId) ?
                        activity.Registrations.Select(r => new
                        {
                            r.Id,
                            r.Status,
                            r.AttendanceStatus,
                            r.RegistrationTime,
                            r.ApprovalTime,
                            r.Notes,
                            Student = new { r.Student.FullName, r.Student.Email, r.Student.Class }
                        }).ToList() : null
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting activity {id}");
                return StatusCode(500, "Có lỗi xảy ra khi lấy thông tin hoạt động");
            }
        }

        // POST: api/activities
        [HttpPost]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<ActionResult<Activity>> CreateActivity(CreateActivityDto dto)
        {
            try
            {
                // Validate dates
                if (dto.StartTime >= dto.EndTime)
                {
                    return BadRequest("Thời gian bắt đầu phải trước thời gian kết thúc");
                }

                if (dto.StartTime <= DateTime.UtcNow)
                {
                    return BadRequest("Thời gian bắt đầu phải sau thời điểm hiện tại");
                }

                // Check for conflicts
                var hasConflict = await _context.Activities
                    .AnyAsync(a => a.CreatorId == User.FindFirst(ClaimTypes.NameIdentifier)!.Value &&
                                   a.Status != ActivityStatus.Cancelled &&
                                   ((a.StartTime <= dto.StartTime && a.EndTime > dto.StartTime) ||
                                    (a.StartTime < dto.EndTime && a.EndTime >= dto.EndTime) ||
                                    (a.StartTime >= dto.StartTime && a.EndTime <= dto.EndTime)));

                if (hasConflict)
                {
                    return BadRequest("Bạn đã có hoạt động khác trong khoảng thời gian này");
                }

                var activity = new Activity
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    Location = dto.Location,
                    MaxParticipants = dto.MaxParticipants,
                    RequireApproval = dto.RequireApproval,
                    Category = dto.Category,
                    CreatorId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value,
                    Status = ActivityStatus.Open
                };

                _context.Activities.Add(activity);
                await _context.SaveChangesAsync();

                // Load creator info
                await _context.Entry(activity)
                    .Reference(a => a.Creator)
                    .LoadAsync();

                // Send realtime notification
                try
                {
                    await _notificationService.SendActivityCreatedNotificationAsync(activity);
                }
                catch (Exception notificationEx)
                {
                    _logger.LogWarning(notificationEx, "Failed to send notification for activity {ActivityId}", activity.Id);
                    // Continue without notification - don't fail the entire request
                }

                _logger.LogInformation($"Activity {activity.Id} created by user {activity.CreatorId}");

                // Return DTO to avoid circular reference
                var responseDto = new
                {
                    activity.Id,
                    activity.Title,
                    activity.Description,
                    activity.StartTime,
                    activity.EndTime,
                    activity.Location,
                    activity.MaxParticipants,
                    activity.CurrentParticipants,
                    activity.Status,
                    activity.RequireApproval,
                    activity.Category,
                    activity.CreatedAt,
                    activity.UpdatedAt,
                    Creator = new
                    {
                        activity.Creator.FullName,
                        activity.Creator.Email,
                        activity.Creator.Department
                    }
                };

                return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating activity");
                return StatusCode(500, "Có lỗi xảy ra khi tạo hoạt động");
            }
        }

        // PUT: api/activities/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UpdateActivity(int id, UpdateActivityDto dto)
        {
            try
            {
                var activity = await _context.Activities.FindAsync(id);

                if (activity == null)
                {
                    return NotFound("Không tìm thấy hoạt động");
                }

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                if (userRole != "Admin" && activity.CreatorId != currentUserId)
                {
                    return Forbid("Bạn không có quyền chỉnh sửa hoạt động này");
                }

                if (activity.Status == ActivityStatus.Completed || activity.Status == ActivityStatus.Cancelled)
                {
                    return BadRequest("Không thể chỉnh sửa hoạt động đã hoàn thành hoặc bị hủy");
                }

                // Validate dates if provided
                var startTime = dto.StartTime ?? activity.StartTime;
                var endTime = dto.EndTime ?? activity.EndTime;

                if (startTime >= endTime)
                {
                    return BadRequest("Thời gian bắt đầu phải trước thời gian kết thúc");
                }

                var updateMessage = "Thông tin hoạt động đã được cập nhật";
                var hasSignificantChange = false;

                // Update fields
                if (!string.IsNullOrEmpty(dto.Title) && dto.Title != activity.Title)
                {
                    activity.Title = dto.Title;
                    hasSignificantChange = true;
                }

                if (!string.IsNullOrEmpty(dto.Description))
                {
                    activity.Description = dto.Description;
                }

                if (dto.StartTime.HasValue && dto.StartTime != activity.StartTime)
                {
                    activity.StartTime = dto.StartTime.Value;
                    updateMessage = "Thời gian hoạt động đã được thay đổi";
                    hasSignificantChange = true;
                }

                if (dto.EndTime.HasValue && dto.EndTime != activity.EndTime)
                {
                    activity.EndTime = dto.EndTime.Value;
                    if (!hasSignificantChange)
                    {
                        updateMessage = "Thời gian hoạt động đã được thay đổi";
                        hasSignificantChange = true;
                    }
                }

                if (!string.IsNullOrEmpty(dto.Location) && dto.Location != activity.Location)
                {
                    activity.Location = dto.Location;
                    updateMessage = "Địa điểm hoạt động đã được thay đổi";
                    hasSignificantChange = true;
                }

                if (dto.MaxParticipants.HasValue && dto.MaxParticipants != activity.MaxParticipants)
                {
                    if (dto.MaxParticipants < activity.CurrentParticipants)
                    {
                        return BadRequest("Số lượng tối đa không thể nhỏ hơn số người đã đăng ký hiện tại");
                    }
                    activity.MaxParticipants = dto.MaxParticipants.Value;
                }

                if (!string.IsNullOrEmpty(dto.Category))
                {
                    activity.Category = dto.Category;
                }

                activity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notifications if there are significant changes
                if (hasSignificantChange)
                {
                    await _notificationService.SendActivityUpdatedNotificationAsync(activity, updateMessage);
                }

                _logger.LogInformation($"Activity {id} updated by user {currentUserId}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating activity {id}");
                return StatusCode(500, "Có lỗi xảy ra khi cập nhật hoạt động");
            }
        }

        // DELETE: api/activities/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> DeleteActivity(int id)
        {
            try
            {
                var activity = await _context.Activities.FindAsync(id);

                if (activity == null)
                {
                    return NotFound("Không tìm thấy hoạt động");
                }

                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                if (userRole != "Admin" && activity.CreatorId != currentUserId)
                {
                    return Forbid("Bạn không có quyền xóa hoạt động này");
                }

                // Mark as cancelled instead of deleting
                activity.Status = ActivityStatus.Cancelled;
                activity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send cancellation notifications
                await _notificationService.SendActivityCancelledNotificationAsync(activity);

                _logger.LogInformation($"Activity {id} cancelled by user {currentUserId}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling activity {id}");
                return StatusCode(500, "Có lỗi xảy ra khi hủy hoạt động");
            }
        }

        // GET: api/activities/my
        [HttpGet("my")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetMyActivities()
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                var activities = await _context.Activities
                    .Where(a => a.CreatorId == currentUserId)
                    .OrderByDescending(a => a.CreatedAt)
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
                        PendingApprovals = a.RequireApproval ?
                            _context.Registrations.Count(r => r.ActivityId == a.Id && r.Status == RegistrationStatus.Pending) : 0,
                        TotalRegistrations = _context.Registrations.Count(r => r.ActivityId == a.Id),
                        IsFull = a.CurrentParticipants >= a.MaxParticipants
                    })
                    .ToListAsync();

                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activities");
                return StatusCode(500, "Có lỗi xảy ra khi lấy danh sách hoạt động của bạn");
            }
        }

        // GET: api/activities/calendar
        [HttpGet("calendar")]
        public async Task<ActionResult<IEnumerable<object>>> GetActivitiesCalendar(
            [FromQuery] DateTime? month = null)
        {
            try
            {
                var targetMonth = month ?? DateTime.UtcNow;
                var startOfMonth = new DateTime(targetMonth.Year, targetMonth.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                var activities = await _context.Activities
                    .Where(a => a.StartTime >= startOfMonth && a.StartTime <= endOfMonth &&
                               a.Status != ActivityStatus.Cancelled)
                    .Select(a => new
                    {
                        a.Id,
                        a.Title,
                        Start = a.StartTime,
                        End = a.EndTime,
                        a.Location,
                        a.CurrentParticipants,
                        a.MaxParticipants,
                        a.Status,
                        a.Category,
                        IsFull = a.CurrentParticipants >= a.MaxParticipants,
                        Color = GetCategoryColor(a.Category)
                    })
                    .ToListAsync();

                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting calendar activities");
                return StatusCode(500, "Có lỗi xảy ra khi lấy lịch hoạt động");
            }
        }

        private static string GetCategoryColor(string? category)
        {
            return category?.ToLower() switch
            {
                "thể thao" => "#4CAF50",
                "văn nghệ" => "#FF9800",
                "học thuật" => "#2196F3",
                "tình nguyện" => "#9C27B0",
                "xã hội" => "#FF5722",
                _ => "#607D8B"
            };
        }
    }

    // DTOs
    public class CreateActivityDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public int MaxParticipants { get; set; }
        public bool RequireApproval { get; set; } = false;
        public string? Category { get; set; }
    }

    public class UpdateActivityDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Location { get; set; }
        public int? MaxParticipants { get; set; }
        public string? Category { get; set; }
    }
} 
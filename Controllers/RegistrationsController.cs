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
    public class RegistrationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<RegistrationsController> _logger;

        public RegistrationsController(
            ApplicationDbContext context,
            INotificationService notificationService,
            ILogger<RegistrationsController> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // POST: api/registrations
        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<Registration>> RegisterForActivity(RegisterActivityDto dto)
        {
            try
            {
                var studentId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                // Check if activity exists
                var activity = await _context.Activities
                    .Include(a => a.Creator)
                    .FirstOrDefaultAsync(a => a.Id == dto.ActivityId);

                if (activity == null)
                {
                    return NotFound("Không tìm thấy hoạt động");
                }

                // Check if activity is open for registration
                if (activity.Status != ActivityStatus.Open)
                {
                    return BadRequest("Hoạt động này không còn mở đăng ký");
                }

                // Check if already registered
                var existingRegistration = await _context.Registrations
                    .FirstOrDefaultAsync(r => r.ActivityId == dto.ActivityId && r.StudentId == studentId);

                if (existingRegistration != null)
                {
                    return BadRequest("Bạn đã đăng ký hoạt động này rồi");
                }

                // Check if activity is full
                if (activity.CurrentParticipants >= activity.MaxParticipants)
                {
                    return BadRequest("Hoạt động này đã đủ số lượng người tham gia");
                }

                // Check for time conflicts
                var hasTimeConflict = await _context.Registrations
                    .Where(r => r.StudentId == studentId && 
                               r.Status == RegistrationStatus.Approved)
                    .Join(_context.Activities,
                          r => r.ActivityId,
                          a => a.Id,
                          (r, a) => a)
                    .AnyAsync(a => (a.StartTime <= activity.StartTime && a.EndTime > activity.StartTime) ||
                                  (a.StartTime < activity.EndTime && a.EndTime >= activity.EndTime) ||
                                  (a.StartTime >= activity.StartTime && a.EndTime <= activity.EndTime));

                if (hasTimeConflict)
                {
                    return BadRequest("Bạn đã có hoạt động khác trong khoảng thời gian này");
                }

                // Create registration
                var registration = new Registration
                {
                    ActivityId = dto.ActivityId,
                    StudentId = studentId,
                    Status = activity.RequireApproval ? RegistrationStatus.Pending : RegistrationStatus.Approved,
                    RegistrationTime = DateTime.UtcNow,
                    Notes = dto.Notes
                };

                _context.Registrations.Add(registration);

                // Update participant count if auto-approved
                if (!activity.RequireApproval)
                {
                    activity.CurrentParticipants++;
                    
                    // Update status if full
                    if (activity.CurrentParticipants >= activity.MaxParticipants)
                    {
                        activity.Status = ActivityStatus.Full;
                    }
                }

                await _context.SaveChangesAsync();

                // Load student info
                await _context.Entry(registration)
                    .Reference(r => r.Student)
                    .LoadAsync();

                // Send notifications
                if (activity.RequireApproval)
                {
                    // Notify teacher about new registration
                    await _notificationService.SendNewRegistrationNotificationAsync(
                        activity.CreatorId, activity, registration.Student.FullName);
                }
                else
                {
                    // Auto-approved - update participants count realtime
                    await _notificationService.UpdateParticipantsCountAsync(
                        activity.Id, activity.CurrentParticipants, activity.MaxParticipants);
                }

                _logger.LogInformation($"Student {studentId} registered for activity {dto.ActivityId}");

                // Return DTO to avoid circular reference
                var responseDto = new
                {
                    Id = registration.Id,
                    ActivityId = registration.ActivityId,
                    StudentId = registration.StudentId,
                    Status = registration.Status.ToString(),
                    RegistrationTime = registration.RegistrationTime,
                    Notes = registration.Notes,
                    RequireApproval = activity.RequireApproval
                };

                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering for activity");
                return StatusCode(500, "Có lỗi xảy ra khi đăng ký hoạt động");
            }
        }

        // GET: api/registrations/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetRegistration(int id)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                var registration = await _context.Registrations
                    .Include(r => r.Activity)
                    .Include(r => r.Student)
                    .Include(r => r.ApprovedBy)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                {
                    return NotFound("Không tìm thấy đăng ký");
                }

                // Check permissions
                var isOwner = registration.StudentId == currentUserId;
                var isActivityCreator = registration.Activity.CreatorId == currentUserId;
                var isAdmin = userRole == "Admin";

                if (!isOwner && !isActivityCreator && !isAdmin)
                {
                    return Forbid("Bạn không có quyền xem thông tin đăng ký này");
                }

                var result = new
                {
                    registration.Id,
                    registration.Status,
                    registration.AttendanceStatus,
                    registration.RegistrationTime,
                    registration.ApprovalTime,
                    registration.Notes,
                    Activity = new
                    {
                        registration.Activity.Id,
                        registration.Activity.Title,
                        registration.Activity.StartTime,
                        registration.Activity.EndTime,
                        registration.Activity.Location
                    },
                    Student = isActivityCreator || isAdmin ? new
                    {
                        registration.Student.FullName,
                        registration.Student.Email,
                        registration.Student.Class
                    } : null,
                    ApprovedBy = registration.ApprovedBy != null ? new
                    {
                        registration.ApprovedBy.FullName,
                        registration.ApprovedBy.Email
                    } : null
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting registration {id}");
                return StatusCode(500, "Có lỗi xảy ra khi lấy thông tin đăng ký");
            }
        }

        // DELETE: api/registrations/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelRegistration(int id)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                var registration = await _context.Registrations
                    .Include(r => r.Activity)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                {
                    return NotFound("Không tìm thấy đăng ký");
                }

                // Check permissions
                var isOwner = registration.StudentId == currentUserId;
                var isAdmin = userRole == "Admin";

                if (!isOwner && !isAdmin)
                {
                    return Forbid("Bạn không có quyền hủy đăng ký này");
                }

                // Check if activity has already started
                if (registration.Activity.StartTime <= DateTime.UtcNow)
                {
                    return BadRequest("Không thể hủy đăng ký sau khi hoạt động đã bắt đầu");
                }

                // Update registration status
                registration.Status = RegistrationStatus.Cancelled;

                // Update participant count if it was approved
                if (registration.Status == RegistrationStatus.Approved)
                {
                    registration.Activity.CurrentParticipants--;
                    
                    // Update activity status if it's no longer full
                    if (registration.Activity.Status == ActivityStatus.Full && 
                        registration.Activity.CurrentParticipants < registration.Activity.MaxParticipants)
                    {
                        registration.Activity.Status = ActivityStatus.Open;
                    }
                }

                await _context.SaveChangesAsync();

                // Update participants count realtime
                await _notificationService.UpdateParticipantsCountAsync(
                    registration.Activity.Id, 
                    registration.Activity.CurrentParticipants, 
                    registration.Activity.MaxParticipants);

                _logger.LogInformation($"Registration {id} cancelled by user {currentUserId}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling registration {id}");
                return StatusCode(500, "Có lỗi xảy ra khi hủy đăng ký");
            }
        }

        // POST: api/registrations/{id}/approve
        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> ApproveRegistration(int id, [FromBody] ApprovalDto? dto = null)
        {
            return await ProcessApproval(id, RegistrationStatus.Approved, dto?.Reason);
        }

        // POST: api/registrations/{id}/reject
        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> RejectRegistration(int id, [FromBody] ApprovalDto dto)
        {
            return await ProcessApproval(id, RegistrationStatus.Rejected, dto.Reason);
        }

        private async Task<IActionResult> ProcessApproval(int id, RegistrationStatus newStatus, string? reason)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                var registration = await _context.Registrations
                    .Include(r => r.Activity)
                    .Include(r => r.Student)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                {
                    return NotFound("Không tìm thấy đăng ký");
                }

                // Check permissions
                var canApprove = userRole == "Admin" || registration.Activity.CreatorId == currentUserId;
                if (!canApprove)
                {
                    return Forbid("Bạn không có quyền duyệt đăng ký này");
                }

                if (registration.Status != RegistrationStatus.Pending)
                {
                    return BadRequest("Đăng ký này đã được xử lý rồi");
                }

                // Check if activity is full when approving
                if (newStatus == RegistrationStatus.Approved && 
                    registration.Activity.CurrentParticipants >= registration.Activity.MaxParticipants)
                {
                    return BadRequest("Hoạt động đã đầy, không thể duyệt thêm đăng ký");
                }

                // Update registration
                registration.Status = newStatus;
                registration.ApprovalTime = DateTime.UtcNow;
                registration.ApprovedById = currentUserId;
                registration.Notes = reason;

                // Update participant count if approved
                if (newStatus == RegistrationStatus.Approved)
                {
                    registration.Activity.CurrentParticipants++;
                    
                    // Update activity status if full
                    if (registration.Activity.CurrentParticipants >= registration.Activity.MaxParticipants)
                    {
                        registration.Activity.Status = ActivityStatus.Full;
                    }
                }

                await _context.SaveChangesAsync();

                // Send notifications
                if (newStatus == RegistrationStatus.Approved)
                {
                    await _notificationService.SendRegistrationApprovedNotificationAsync(
                        registration.StudentId, registration.Activity);
                    
                    // Update participants count realtime
                    await _notificationService.UpdateParticipantsCountAsync(
                        registration.Activity.Id, 
                        registration.Activity.CurrentParticipants, 
                        registration.Activity.MaxParticipants);
                }
                else
                {
                    await _notificationService.SendRegistrationRejectedNotificationAsync(
                        registration.StudentId, registration.Activity, reason);
                }

                var statusText = newStatus == RegistrationStatus.Approved ? "approved" : "rejected";
                _logger.LogInformation($"Registration {id} {statusText} by user {currentUserId}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing approval for registration {id}");
                return StatusCode(500, "Có lỗi xảy ra khi xử lý duyệt đăng ký");
            }
        }

        // GET: api/registrations/pending
        [HttpGet("pending")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetPendingRegistrations()
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                IQueryable<Registration> query = _context.Registrations
                    .Include(r => r.Student)
                    .Include(r => r.Activity)
                    .Where(r => r.Status == RegistrationStatus.Pending);

                // Teachers can only see pending registrations for their activities
                if (userRole == "Teacher")
                {
                    query = query.Where(r => r.Activity.CreatorId == currentUserId);
                }

                var pendingRegistrations = await query
                    .OrderByDescending(r => r.RegistrationTime)
                    .Select(r => new
                    {
                        Id = r.Id,
                        student = new
                        {
                            Id = r.Student.Id,
                            fullName = r.Student.FullName ?? "Không có tên",
                            email = r.Student.Email ?? "Không có email",
                            className = r.Student.Class ?? "-"
                        },
                        activity = new
                        {
                            Id = r.Activity.Id,
                            title = r.Activity.Title ?? "Không có tiêu đề",
                            startTime = r.Activity.StartTime,
                            location = r.Activity.Location ?? "Không có địa điểm"
                        },
                        registrationTime = r.RegistrationTime,
                        status = r.Status.ToString(),
                        notes = r.Notes ?? ""
                    })
                    .ToListAsync();

                return Ok(pendingRegistrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending registrations");
                return StatusCode(500, "Có lỗi xảy ra khi lấy danh sách đăng ký chờ duyệt");
            }
        }

        // GET: api/registrations/my
        [HttpGet("my")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<IEnumerable<object>>> GetMyRegistrations()
        {
            try
            {
                var studentId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                var registrations = await _context.Registrations
                    .Where(r => r.StudentId == studentId)
                    .Include(r => r.Activity)
                    .OrderByDescending(r => r.RegistrationTime)
                    .Select(r => new
                    {
                        r.Id,
                        status = r.Status.ToString(),
                        attendanceStatus = r.AttendanceStatus.ToString(),
                        registrationTime = r.RegistrationTime,
                        approvalTime = r.ApprovalTime,
                        activity = new
                        {
                            r.Activity.Id,
                            title = r.Activity.Title,
                            startTime = r.Activity.StartTime,
                            endTime = r.Activity.EndTime,
                            location = r.Activity.Location,
                            category = r.Activity.Category,
                            status = r.Activity.Status.ToString()
                        },
                        canCancel = r.Activity.StartTime > DateTime.UtcNow && 
                                   (r.Status == RegistrationStatus.Pending || r.Status == RegistrationStatus.Approved)
                    })
                    .ToListAsync();

                return Ok(registrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user registrations");
                return StatusCode(500, "Có lỗi xảy ra khi lấy danh sách đăng ký của bạn");
            }
        }

        // GET: api/registrations/activity/{activityId}
        [HttpGet("activity/{activityId}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetActivityRegistrations(int activityId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                // Check if user can view this activity's registrations
                var activity = await _context.Activities.FindAsync(activityId);
                if (activity == null)
                {
                    return NotFound("Không tìm thấy hoạt động");
                }

                if (userRole != "Admin" && activity.CreatorId != currentUserId)
                {
                    return Forbid("Bạn không có quyền xem danh sách đăng ký này");
                }

                var registrations = await _context.Registrations
                    .Where(r => r.ActivityId == activityId)
                    .Include(r => r.Student)
                    .Include(r => r.ApprovedBy)
                    .OrderBy(r => r.RegistrationTime)
                    .Select(r => new
                    {
                        r.Id,
                        status = r.Status.ToString(),
                        attendanceStatus = r.AttendanceStatus.ToString(),
                        registrationTime = r.RegistrationTime,
                        approvalTime = r.ApprovalTime,
                        r.Notes,
                        student = new
                        {
                            r.Student.Id,
                            fullName = r.Student.FullName,
                            email = r.Student.Email,
                            className = r.Student.Class
                        },
                        approvedBy = r.ApprovedBy != null ? new
                        {
                            fullName = r.ApprovedBy.FullName,
                            email = r.ApprovedBy.Email
                        } : null,
                        canApprove = r.Status == RegistrationStatus.Pending,
                        canMarkAttendance = activity.StartTime <= DateTime.UtcNow && r.Status == RegistrationStatus.Approved
                    })
                    .ToListAsync();

                return Ok(registrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting activity {activityId} registrations");
                return StatusCode(500, "Có lỗi xảy ra khi lấy danh sách đăng ký");
            }
        }

        // PUT: api/registrations/{id}/attendance
        [HttpPut("{id}/attendance")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UpdateAttendance(int id, UpdateAttendanceDto dto)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var userRole = User.FindFirst("Role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                var registration = await _context.Registrations
                    .Include(r => r.Activity)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                {
                    return NotFound("Không tìm thấy đăng ký");
                }

                // Check permissions
                if (userRole != "Admin" && registration.Activity.CreatorId != currentUserId)
                {
                    return Forbid("Bạn không có quyền cập nhật điểm danh này");
                }

                if (registration.Status != RegistrationStatus.Approved)
                {
                    return BadRequest("Chỉ có thể điểm danh cho đăng ký đã được duyệt");
                }

                registration.AttendanceStatus = dto.AttendanceStatus;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Attendance updated for registration {id} by user {currentUserId}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating attendance for registration {id}");
                return StatusCode(500, "Có lỗi xảy ra khi cập nhật điểm danh");
            }
        }
    }

    // DTOs
    public class RegisterActivityDto
    {
        public int ActivityId { get; set; }
        public string? Notes { get; set; }
    }

    public class ApprovalDto
    {
        public string? Reason { get; set; }
    }

    public class UpdateAttendanceDto
    {
        public AttendanceStatus AttendanceStatus { get; set; }
    }
} 
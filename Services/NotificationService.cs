using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocketIR.API.Data;
using SocketIR.API.Hubs;
using SocketIR.API.Models;

namespace SocketIR.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ActivityHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<ActivityHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task CreateAndSendNotificationAsync(string userId, string title, string content, NotificationType type, int? activityId = null, int? registrationId = null)
        {
            try
            {
                // Tạo notification trong database
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Content = content,
                    Type = type,
                    RelatedActivityId = activityId,
                    RelatedRegistrationId = registrationId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Gửi thông báo realtime qua SignalR
                await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", new
                {
                    Id = notification.Id,
                    Title = notification.Title,
                    Content = notification.Content,
                    Type = notification.Type.ToString(),
                    RelatedActivityId = notification.RelatedActivityId,
                    RelatedRegistrationId = notification.RelatedRegistrationId,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead
                });

                _logger.LogInformation($"Notification sent to user {userId}: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user {userId}");
                throw;
            }
        }

        public async Task SendNotificationToGroupAsync(string groupName, string title, string content, NotificationType type)
        {
            try
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNotification", new
                {
                    Title = title,
                    Content = content,
                    Type = type.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    IsGroupNotification = true
                });

                _logger.LogInformation($"Group notification sent to {groupName}: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending group notification to {groupName}");
                throw;
            }
        }

        public async Task SendActivityCreatedNotificationAsync(Activity activity)
        {
            try
            {
                var title = "Hoạt động mới";
                var content = $"Hoạt động '{activity.Title}' vừa được tạo. Thời gian: {activity.StartTime:dd/MM/yyyy HH:mm}";

                // Thông báo cho tất cả học sinh
                await SendNotificationToGroupAsync("Students", title, content, NotificationType.ActivityCreated);

                // Broadcast thông tin hoạt động mới
                await _hubContext.Clients.Group("Students").SendAsync("NewActivityCreated", new
                {
                    Id = activity.Id,
                    Title = activity.Title,
                    Description = activity.Description,
                    StartTime = activity.StartTime,
                    EndTime = activity.EndTime,
                    Location = activity.Location,
                    MaxParticipants = activity.MaxParticipants,
                    CurrentParticipants = activity.CurrentParticipants,
                    Category = activity.Category,
                    Status = activity.Status.ToString(),
                    RequireApproval = activity.RequireApproval
                });

                _logger.LogInformation($"Activity created notification sent for activity {activity.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending activity created notification for activity {activity.Id}");
                throw;
            }
        }

        public async Task SendRegistrationApprovedNotificationAsync(string studentId, Activity activity)
        {
            var title = "Đăng ký được duyệt";
            var content = $"Đăng ký tham gia hoạt động '{activity.Title}' đã được duyệt.";

            await CreateAndSendNotificationAsync(studentId, title, content, NotificationType.RegistrationApproved, activity.Id);

            // Gửi thông báo cập nhật trạng thái qua SignalR
            await _hubContext.Clients.User(studentId).SendAsync("RegistrationStatusUpdate", new
            {
                ActivityId = activity.Id,
                ActivityTitle = activity.Title,
                Status = "approved",
                Message = content,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendRegistrationRejectedNotificationAsync(string studentId, Activity activity, string? reason = null)
        {
            var title = "Đăng ký bị từ chối";
            var content = $"Đăng ký tham gia hoạt động '{activity.Title}' đã bị từ chối.";
            if (!string.IsNullOrEmpty(reason))
            {
                content += $" Lý do: {reason}";
            }

            await CreateAndSendNotificationAsync(studentId, title, content, NotificationType.RegistrationRejected, activity.Id);

            // Gửi thông báo cập nhật trạng thái qua SignalR
            await _hubContext.Clients.User(studentId).SendAsync("RegistrationStatusUpdate", new
            {
                ActivityId = activity.Id,
                ActivityTitle = activity.Title,
                Status = "rejected",
                Message = content,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendNewRegistrationNotificationAsync(string teacherId, Activity activity, string studentName)
        {
            var title = "Đăng ký mới";
            var content = $"Học sinh {studentName} vừa đăng ký tham gia hoạt động '{activity.Title}'.";

            await CreateAndSendNotificationAsync(teacherId, title, content, NotificationType.NewRegistration, activity.Id);

            // Gửi thông báo realtime cho giáo viên
            await _hubContext.Clients.User(teacherId).SendAsync("NewRegistration", new
            {
                Activity = new
                {
                    Id = activity.Id,
                    Title = activity.Title
                },
                Student = new
                {
                    FullName = studentName
                },
                Message = content,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendActivityUpdatedNotificationAsync(Activity activity, string updateMessage)
        {
            var title = "Hoạt động được cập nhật";
            var content = $"Hoạt động '{activity.Title}' đã được cập nhật: {updateMessage}";

            // Gửi thông báo cho tất cả người đã đăng ký
            var registeredStudents = await _context.Registrations
                .Where(r => r.ActivityId == activity.Id && r.Status == RegistrationStatus.Approved)
                .Select(r => r.StudentId)
                .ToListAsync();

            foreach (var studentId in registeredStudents)
            {
                await CreateAndSendNotificationAsync(studentId, title, content, NotificationType.ActivityUpdated, activity.Id);
            }

            // Broadcast cập nhật cho tất cả người dùng đang theo dõi hoạt động này
            await _hubContext.Clients.Group($"Activity_{activity.Id}").SendAsync("ActivityUpdated", new
            {
                ActivityId = activity.Id,
                Title = activity.Title,
                UpdateType = "updated",
                Message = updateMessage,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendActivityCancelledNotificationAsync(Activity activity)
        {
            var title = "Hoạt động bị hủy";
            var content = $"Hoạt động '{activity.Title}' đã bị hủy.";

            // Gửi thông báo cho tất cả người đã đăng ký
            var registeredStudents = await _context.Registrations
                .Where(r => r.ActivityId == activity.Id)
                .Select(r => r.StudentId)
                .ToListAsync();

            foreach (var studentId in registeredStudents)
            {
                await CreateAndSendNotificationAsync(studentId, title, content, NotificationType.ActivityCancelled, activity.Id);
            }

            // Broadcast hủy hoạt động
            await _hubContext.Clients.Group($"Activity_{activity.Id}").SendAsync("ActivityUpdated", new
            {
                ActivityId = activity.Id,
                Title = activity.Title,
                UpdateType = "cancelled",
                Message = content,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task UpdateParticipantsCountAsync(int activityId, int currentParticipants, int maxParticipants)
        {
            await _hubContext.Clients.Group($"Activity_{activityId}").SendAsync("ParticipantsUpdated", new
            {
                ActivityId = activityId,
                CurrentParticipants = currentParticipants,
                MaxParticipants = maxParticipants,
                IsFull = currentParticipants >= maxParticipants
            });

            // Cũng broadcast cho tất cả students để cập nhật danh sách hoạt động
            await _hubContext.Clients.Group("Students").SendAsync("ActivityParticipantsUpdated", new
            {
                ActivityId = activityId,
                CurrentParticipants = currentParticipants,
                MaxParticipants = maxParticipants,
                IsFull = currentParticipants >= maxParticipants
            });
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50) // Giới hạn 50 thông báo gần nhất
                .ToListAsync();
        }

        public async Task MarkNotificationAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Thông báo cập nhật trạng thái đọc
                await _hubContext.Clients.User(userId).SendAsync("NotificationRead", new
                {
                    NotificationId = notificationId,
                    ReadAt = notification.ReadAt
                });
            }
        }

        public async Task MarkAllNotificationsAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                var readTime = DateTime.UtcNow;
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = readTime;
                }

                await _context.SaveChangesAsync();

                // Thông báo tất cả đã đọc
                await _hubContext.Clients.User(userId).SendAsync("AllNotificationsRead", new
                {
                    ReadAt = readTime,
                    Count = unreadNotifications.Count
                });
            }
        }
    }
} 
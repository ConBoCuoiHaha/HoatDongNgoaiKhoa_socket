using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SocketIR.API.Models;

namespace SocketIR.API.Hubs
{
    [Authorize]
    public class ActivityHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("UserJoined", $"{Context.User?.Identity?.Name} đã tham gia nhóm {groupName}");
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("UserLeft", $"{Context.User?.Identity?.Name} đã rời khỏi nhóm {groupName}");
        }

        // Tham gia nhóm theo vai trò (Students, Teachers, Admins)
        public async Task JoinRoleGroup()
        {
            var userRole = Context.User?.FindFirst("Role")?.Value ?? 
                          Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            
            if (!string.IsNullOrEmpty(userRole))
            {
                var groupName = $"{userRole}s"; // Students, Teachers, Admins
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            }
        }

        // Tham gia nhóm theo hoạt động cụ thể
        public async Task JoinActivityGroup(int activityId)
        {
            var groupName = $"Activity_{activityId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        // Rời khỏi nhóm hoạt động
        public async Task LeaveActivityGroup(int activityId)
        {
            var groupName = $"Activity_{activityId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        // Gửi thông báo đến người dùng cụ thể
        public async Task SendNotificationToUser(string userId, string title, string content, string type)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", new
            {
                Title = title,
                Content = content,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }

        // Gửi thông báo đến nhóm người dùng
        public async Task SendNotificationToGroup(string groupName, string title, string content, string type)
        {
            await Clients.Group(groupName).SendAsync("ReceiveNotification", new
            {
                Title = title,
                Content = content,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }

        // Cập nhật thời gian thực khi có hoạt động mới
        public async Task BroadcastNewActivity(Activity activity)
        {
            await Clients.Group("Students").SendAsync("NewActivityCreated", new
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
                Status = activity.Status.ToString()
            });
        }

        // Cập nhật số lượng đăng ký realtime
        public async Task UpdateActivityParticipants(int activityId, int currentParticipants, int maxParticipants)
        {
            var groupName = $"Activity_{activityId}";
            await Clients.Group(groupName).SendAsync("ParticipantsUpdated", new
            {
                ActivityId = activityId,
                CurrentParticipants = currentParticipants,
                MaxParticipants = maxParticipants,
                IsFull = currentParticipants >= maxParticipants
            });
        }

        // Thông báo khi có đăng ký mới cho giáo viên
        public async Task NotifyTeacherNewRegistration(string teacherId, int activityId, string studentName)
        {
            await Clients.User(teacherId).SendAsync("NewRegistration", new
            {
                ActivityId = activityId,
                StudentName = studentName,
                Timestamp = DateTime.UtcNow,
                Message = $"Học sinh {studentName} vừa đăng ký tham gia hoạt động"
            });
        }

        // Thông báo khi đăng ký được duyệt/từ chối
        public async Task NotifyRegistrationStatus(string studentId, int activityId, string activityTitle, string status, string? reason = null)
        {
            var message = status.ToLower() switch
            {
                "approved" => $"Đăng ký tham gia '{activityTitle}' đã được duyệt",
                "rejected" => $"Đăng ký tham gia '{activityTitle}' đã bị từ chối" + (reason != null ? $": {reason}" : ""),
                _ => $"Trạng thái đăng ký '{activityTitle}' đã được cập nhật: {status}"
            };

            await Clients.User(studentId).SendAsync("RegistrationStatusUpdate", new
            {
                ActivityId = activityId,
                ActivityTitle = activityTitle,
                Status = status,
                Message = message,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });
        }

        // Thông báo khi hoạt động bị cập nhật hoặc hủy
        public async Task NotifyActivityUpdate(int activityId, string title, string updateType, string message)
        {
            var groupName = $"Activity_{activityId}";
            await Clients.Group(groupName).SendAsync("ActivityUpdated", new
            {
                ActivityId = activityId,
                Title = title,
                UpdateType = updateType, // "updated", "cancelled", "rescheduled"
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public override async Task OnConnectedAsync()
        {
            // Tự động tham gia nhóm theo vai trò khi kết nối
            await JoinRoleGroup();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Cleanup nếu cần
            await base.OnDisconnectedAsync(exception);
        }
    }
} 
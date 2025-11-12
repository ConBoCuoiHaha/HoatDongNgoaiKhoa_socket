using SocketIR.API.Models;

namespace SocketIR.API.Services
{
    public interface INotificationService
    {
        Task CreateAndSendNotificationAsync(string userId, string title, string content, NotificationType type, int? activityId = null, int? registrationId = null);
        Task SendNotificationToGroupAsync(string groupName, string title, string content, NotificationType type);
        Task SendActivityCreatedNotificationAsync(Activity activity);
        Task SendRegistrationApprovedNotificationAsync(string studentId, Activity activity);
        Task SendRegistrationRejectedNotificationAsync(string studentId, Activity activity, string? reason = null);
        Task SendNewRegistrationNotificationAsync(string teacherId, Activity activity, string studentName);
        Task SendActivityUpdatedNotificationAsync(Activity activity, string updateMessage);
        Task SendActivityCancelledNotificationAsync(Activity activity);
        Task UpdateParticipantsCountAsync(int activityId, int currentParticipants, int maxParticipants);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false);
        Task MarkNotificationAsReadAsync(int notificationId, string userId);
        Task MarkAllNotificationsAsReadAsync(string userId);
    }
} 
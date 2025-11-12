using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocketIR.API.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        [Required]
        public NotificationType Type { get; set; }

        public bool IsRead { get; set; } = false;

        public int? RelatedActivityId { get; set; }

        public int? RelatedRegistrationId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("RelatedActivityId")]
        public virtual Activity? RelatedActivity { get; set; }

        [ForeignKey("RelatedRegistrationId")]
        public virtual Registration? RelatedRegistration { get; set; }
    }

    public enum NotificationType
    {
        ActivityCreated = 1,        // Hoạt động mới được tạo
        RegistrationApproved = 2,   // Đăng ký được duyệt
        RegistrationRejected = 3,   // Đăng ký bị từ chối
        ActivityUpdated = 4,        // Hoạt động được cập nhật
        ActivityCancelled = 5,      // Hoạt động bị hủy
        ActivityReminder = 6,       // Nhắc nhở hoạt động sắp diễn ra
        NewRegistration = 7,        // Có đăng ký mới (cho giáo viên)
        System = 8                  // Thông báo hệ thống
    }
} 
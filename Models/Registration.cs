using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocketIR.API.Models
{
    public class Registration
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ActivityId { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        [Required]
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.NotSet;

        public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovalTime { get; set; }

        public string? ApprovedById { get; set; }

        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("ActivityId")]
        public virtual Activity Activity { get; set; } = null!;

        [ForeignKey("StudentId")]
        public virtual User Student { get; set; } = null!;

        [ForeignKey("ApprovedById")]
        public virtual User? ApprovedBy { get; set; }
    }

    public enum RegistrationStatus
    {
        Pending = 1,    // Chờ duyệt
        Approved = 2,   // Đã được duyệt
        Rejected = 3,   // Bị từ chối
        Cancelled = 4   // Đã hủy
    }

    public enum AttendanceStatus
    {
        NotSet = 0,     // Chưa xác định
        Present = 1,    // Có mặt
        Absent = 2      // Vắng mặt
    }
} 
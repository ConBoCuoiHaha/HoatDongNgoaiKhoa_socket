using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocketIR.API.Models
{
    public class Activity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        [MaxLength(200)]
        public string Location { get; set; } = string.Empty;

        [Required]
        public int MaxParticipants { get; set; }

        public int CurrentParticipants { get; set; } = 0;

        [Required]
        public ActivityStatus Status { get; set; } = ActivityStatus.Open;

        public bool RequireApproval { get; set; } = false;

        [MaxLength(100)]
        public string? Category { get; set; }

        [Required]
        public string CreatorId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("CreatorId")]
        public virtual User Creator { get; set; } = null!;
        
        public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    }

    public enum ActivityStatus
    {
        Open = 1,      // Đang mở đăng ký
        Full = 2,      // Đã đủ người
        Closed = 3,    // Đã đóng đăng ký
        Cancelled = 4, // Đã hủy
        Completed = 5  // Đã hoàn thành
    }
} 
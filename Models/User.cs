using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SocketIR.API.Models
{
    public class User : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        [MaxLength(50)]
        public string? Class { get; set; } // Cho học sinh

        [MaxLength(100)]
        public string? Department { get; set; } // Cho giáo viên

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Activity> CreatedActivities { get; set; } = new List<Activity>();
        public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public enum UserRole
    {
        Student = 1,
        Teacher = 2,
        Admin = 3
    }
} 
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SocketIR.API.Models;

namespace SocketIR.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Activity> Activities { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Cấu hình User
            builder.Entity<User>(entity =>
            {
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Class).HasMaxLength(50);
                entity.Property(e => e.Department).HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // Cấu hình Activity
            builder.Entity<Activity>(entity =>
            {
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.Location).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Category).HasMaxLength(100);

                entity.HasOne(a => a.Creator)
                      .WithMany(u => u.CreatedActivities)
                      .HasForeignKey(a => a.CreatorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.StartTime, e.EndTime });
                entity.HasIndex(e => e.Status);
            });

            // Cấu hình Registration
            builder.Entity<Registration>(entity =>
            {
                entity.HasOne(r => r.Activity)
                      .WithMany(a => a.Registrations)
                      .HasForeignKey(r => r.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Student)
                      .WithMany(u => u.Registrations)
                      .HasForeignKey(r => r.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.ApprovedBy)
                      .WithMany()
                      .HasForeignKey(r => r.ApprovedById)
                      .OnDelete(DeleteBehavior.NoAction);

                // Unique constraint: một student chỉ đăng ký một activity một lần
                entity.HasIndex(e => new { e.ActivityId, e.StudentId }).IsUnique();
                entity.HasIndex(e => e.Status);
            });

            // Cấu hình Notification
            builder.Entity<Notification>(entity =>
            {
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);

                entity.HasOne(n => n.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.RelatedActivity)
                      .WithMany()
                      .HasForeignKey(n => n.RelatedActivityId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(n => n.RelatedRegistration)
                      .WithMany()
                      .HasForeignKey(n => n.RelatedRegistrationId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => new { e.UserId, e.IsRead });
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
} 
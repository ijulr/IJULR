using IJULR.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IJULR.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Volume> Volumes => Set<Volume>();
        public DbSet<Issue> Issues => Set<Issue>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<Author> Authors => Set<Author>();
        public DbSet<SubmissionStatusHistory> SubmissionStatusHistory => Set<SubmissionStatusHistory>();
        public DbSet<Reviewer> Reviewers => Set<Reviewer>();
        public DbSet<ReviewAssignment> ReviewAssignments => Set<ReviewAssignment>();
        public DbSet<EditorialBoardMember> EditorialBoardMembers => Set<EditorialBoardMember>();
        public DbSet<Page> Pages => Set<Page>();
        public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
        public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
        public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
        public DbSet<WhatsAppLog> WhatsAppLogs => Set<WhatsAppLog>();
        public DbSet<PublishedVersion> PublishedVersions => Set<PublishedVersion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MySQL specific: Set default charset for all tables
            modelBuilder.HasCharSet("utf8mb4");

            // Indexes
            modelBuilder.Entity<User>().HasIndex(e => e.Email).IsUnique();
            modelBuilder.Entity<Volume>().HasIndex(e => e.VolumeNumber).IsUnique();
            modelBuilder.Entity<Issue>().HasIndex(e => new { e.VolumeId, e.IssueNumber }).IsUnique();
            modelBuilder.Entity<Submission>().HasIndex(e => e.TrackingId).IsUnique();
            modelBuilder.Entity<Submission>().Property(e => e.ReviewScore).HasPrecision(3, 1);
            modelBuilder.Entity<Reviewer>().HasIndex(e => e.UserId).IsUnique();
            modelBuilder.Entity<Page>().HasIndex(e => e.Slug).IsUnique();
            modelBuilder.Entity<SiteSetting>().HasIndex(e => e.SettingKey).IsUnique();
            modelBuilder.Entity<EmailTemplate>().HasIndex(e => e.TemplateName).IsUnique();

            // MySQL specific: Limit string length for indexed columns (MySQL has key length limits)
            modelBuilder.Entity<User>().Property(e => e.Email).HasMaxLength(255);
            modelBuilder.Entity<Submission>().Property(e => e.TrackingId).HasMaxLength(100);
            modelBuilder.Entity<Page>().Property(e => e.Slug).HasMaxLength(255);
            modelBuilder.Entity<SiteSetting>().Property(e => e.SettingKey).HasMaxLength(255);
            modelBuilder.Entity<EmailTemplate>().Property(e => e.TemplateName).HasMaxLength(255);

            // MySQL specific: Use LONGTEXT for large text fields
            modelBuilder.Entity<Submission>().Property(e => e.Abstract).HasColumnType("longtext");
            modelBuilder.Entity<Page>().Property(e => e.Content).HasColumnType("longtext");
            modelBuilder.Entity<EmailTemplate>().Property(e => e.Body).HasColumnType("longtext");
            modelBuilder.Entity<EmailLog>().Property(e => e.Body).HasColumnType("longtext");
            modelBuilder.Entity<EditorialBoardMember>().Property(e => e.Bio).HasColumnType("longtext");

            // Relationships
            modelBuilder.Entity<Issue>()
                .HasOne(i => i.Volume)
                .WithMany(v => v.Issues)
                .HasForeignKey(i => i.VolumeId);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Issue)
                .WithMany(i => i.Submissions)
                .HasForeignKey(s => s.IssueId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Author>()
                .HasOne(a => a.Submission)
                .WithMany(s => s.Authors)
                .HasForeignKey(a => a.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubmissionStatusHistory>()
                .HasOne(h => h.Submission)
                .WithMany(s => s.StatusHistory)
                .HasForeignKey(h => h.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Reviewer>()
                .HasOne(r => r.User)
                .WithOne(u => u.Reviewer)
                .HasForeignKey<Reviewer>(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Submission)
                .WithMany(s => s.ReviewAssignments)
                .HasForeignKey(ra => ra.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Reviewer)
                .WithMany(r => r.Assignments)
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // PublishedVersion relationships
            modelBuilder.Entity<PublishedVersion>()
                .HasOne(pv => pv.Submission)
                .WithMany(s => s.PublishedVersions)
                .HasForeignKey(pv => pv.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PublishedVersion>()
                .HasOne(pv => pv.Issue)
                .WithMany(i => i.PublishedVersions)
                .HasForeignKey(pv => pv.IssueId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

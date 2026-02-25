using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IJULR.Web.Models
{
    // ==================== USER ====================
    public class User
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Institution { get; set; }

        [MaxLength(100)]
        public string? Designation { get; set; }

        [MaxLength(50)]
        public string Role { get; set; } = "Reviewer";

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime(6)")]
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        public virtual Reviewer? Reviewer { get; set; }
    }

    // ==================== VOLUME ====================
    public class Volume
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int VolumeNumber { get; set; }

        // Soft Delete properties
        public bool IsDeleted { get; set; } = false;

        [Column(TypeName = "datetime(6)")]
        public DateTime? DeletedAt { get; set; }

        // Navigation
        public virtual ICollection<Issue> Issues { get; set; } = new List<Issue>();
    }

    // ==================== ISSUE ====================
    public class Issue
    {
        public int Id { get; set; }
        public int VolumeId { get; set; }
        public int IssueNumber { get; set; }

        [MaxLength(255)]
        public string? Title { get; set; }

        [MaxLength(100)]
        public string? QuarterName { get; set; }

        public int StartMonth { get; set; }
        public int EndMonth { get; set; }
        public bool IsPublished { get; set; }
        public bool IsCurrentIssue { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime? PublishedAt { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft Delete properties
        public bool IsDeleted { get; set; } = false;

        [Column(TypeName = "datetime(6)")]
        public DateTime? DeletedAt { get; set; }

        public virtual Volume Volume { get; set; } = null!;
        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
        public virtual ICollection<PublishedVersion> PublishedVersions { get; set; } = new List<PublishedVersion>();

        [NotMapped]
        public string DisplayTitle => $"Volume {Volume?.VolumeNumber}, Issue {IssueNumber} ({Title})";
    }

    // ==================== SUBMISSION ====================
    public class Submission
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string TrackingId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string PaperTitle { get; set; } = string.Empty;

        [Column(TypeName = "longtext")]
        public string? Abstract { get; set; }

        [MaxLength(500)]
        public string? Keywords { get; set; }

        [MaxLength(100)]
        public string Category { get; set; } = "Research Article";

        // File Info
        [MaxLength(255)]
        public string? ManuscriptFileName { get; set; }

        [MaxLength(500)]
        public string? ManuscriptFilePath { get; set; }

        public long? ManuscriptFileSize { get; set; }

        [MaxLength(500)]
        public string? PublishedPdfPath { get; set; }

        // Status
        [MaxLength(50)]
        public string Status { get; set; } = "Submitted";

        // Publishing Info
        public int? IssueId { get; set; }

        [MaxLength(100)]
        public string? DOI { get; set; }

        public int? PageStart { get; set; }
        public int? PageEnd { get; set; }

        // Review Info
        [Column(TypeName = "decimal(3,1)")]
        public decimal? ReviewScore { get; set; }

        [Column(TypeName = "text")]
        public string? EditorRemarks { get; set; }

        [Column(TypeName = "text")]
        public string? RevisionComments { get; set; }

        // Payment Info
        [MaxLength(255)]
        public string? PaymentReceiptFileName { get; set; }

        [MaxLength(500)]
        public string? PaymentReceiptFilePath { get; set; }

        [MaxLength(50)]
        public string? PaymentStatus { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime? PaymentDate { get; set; }

        // Certificate
        [MaxLength(500)]
        public string? CertificateFilePath { get; set; }

        // Copyright & Creative Comments
        [MaxLength(500)]
        public string? CopyrightInfo { get; set; }

        [Column(TypeName = "text")]
        public string? CreativeComments { get; set; }

        // Timestamps
        [Column(TypeName = "datetime(6)")]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime(6)")]
        public DateTime? PublishedAt { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime? UpdatedAt { get; set; }

        public virtual Issue? Issue { get; set; }
        public virtual ICollection<Author> Authors { get; set; } = new List<Author>();
        public virtual ICollection<SubmissionStatusHistory> StatusHistory { get; set; } = new List<SubmissionStatusHistory>();
        public virtual ICollection<ReviewAssignment> ReviewAssignments { get; set; } = new List<ReviewAssignment>();
        public virtual ICollection<PublishedVersion> PublishedVersions { get; set; } = new List<PublishedVersion>();

        [NotMapped]
        public string PageRange => PageStart.HasValue && PageEnd.HasValue ? $"{PageStart}-{PageEnd}" : "";

        [NotMapped]
        public Author? PrimaryAuthor => Authors.FirstOrDefault(a => a.IsCorresponding) ?? Authors.FirstOrDefault();
    }

    // ==================== AUTHOR ====================
    public class Author
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }
        public int AuthorOrder { get; set; } = 1;

        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Institution { get; set; }

        [MaxLength(100)]
        public string? Designation { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        public bool IsCorresponding { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Submission Submission { get; set; } = null!;
    }

    // ==================== SUBMISSION STATUS HISTORY ====================
    public class SubmissionStatusHistory
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }

        [MaxLength(50)]
        public string? FromStatus { get; set; }

        [MaxLength(50)]
        public string ToStatus { get; set; } = string.Empty;

        public int? ChangedByUserId { get; set; }

        [Column(TypeName = "text")]
        public string? Remarks { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public virtual Submission Submission { get; set; } = null!;
        public virtual User? ChangedByUser { get; set; }
    }

    // ==================== REVIEWER ====================
    public class Reviewer
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        [MaxLength(255)]
        public string? Specialization { get; set; }

        public bool IsAvailable { get; set; } = true;
        public int MaxAssignments { get; set; } = 5;
        public int CurrentAssignments { get; set; }
        public int TotalReviews { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual ICollection<ReviewAssignment> Assignments { get; set; } = new List<ReviewAssignment>();
    }

    // ==================== REVIEW ASSIGNMENT ====================
    public class ReviewAssignment
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }
        public int ReviewerId { get; set; }
        public int? AssignedByUserId { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Assigned";

        public int? Score { get; set; }

        [Column(TypeName = "text")]
        public string? Comments { get; set; }

        [MaxLength(50)]
        public string? Recommendation { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime(6)")]
        public DateTime? CompletedAt { get; set; }

        public virtual Submission Submission { get; set; } = null!;
        public virtual Reviewer Reviewer { get; set; } = null!;
    }

    // ==================== EDITORIAL BOARD MEMBER ====================
    public class EditorialBoardMember
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Institution { get; set; }

        [MaxLength(100)]
        public string? Designation { get; set; }

        [MaxLength(255)]
        public string? Specialization { get; set; }

        [Column(TypeName = "longtext")]
        public string? Bio { get; set; }

        [MaxLength(500)]
        public string? ProfileImagePath { get; set; }

        [MaxLength(50)]
        public string BoardCategory { get; set; } = "EditorialBoard";

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==================== PAGE ====================
    public class Page
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column(TypeName = "longtext")]
        public string? Content { get; set; }

        [MaxLength(500)]
        public string? MetaDescription { get; set; }

        [MaxLength(500)]
        public string? MetaKeywords { get; set; }

        [MaxLength(100)]
        public string? ParentMenu { get; set; }

        public int MenuOrder { get; set; }
        public bool IsPublished { get; set; } = true;

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime(6)")]
        public DateTime? UpdatedAt { get; set; }
    }

    // ==================== SITE SETTING ====================
    public class SiteSetting
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string SettingKey { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? SettingValue { get; set; }

        [MaxLength(50)]
        public string? SettingType { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime(6)")]
        public DateTime? UpdatedAt { get; set; }
    }

    // ==================== EMAIL TEMPLATE ====================
    public class EmailTemplate
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string TemplateName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Column(TypeName = "longtext")]
        public string Body { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==================== EMAIL LOG ====================
    public class EmailLog
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string ToEmail { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Column(TypeName = "longtext")]
        public string? Body { get; set; }

        [MaxLength(255)]
        public string? TemplateName { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [Column(TypeName = "text")]
        public string? ErrorMessage { get; set; }

        public int? SubmissionId { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime? SentAt { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==================== WHATSAPP LOG ====================
    public class WhatsAppLog
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string ToPhone { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [Column(TypeName = "text")]
        public string? ErrorMessage { get; set; }

        public int? SubmissionId { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime? SentAt { get; set; }

        [Column(TypeName = "datetime(6)")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==================== PUBLISHED VERSION ====================
    public class PublishedVersion
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }
        public int IssueId { get; set; }
        public int PageStart { get; set; }
        public int PageEnd { get; set; }

        [MaxLength(500)]
        public string PublishedPdfPath { get; set; } = string.Empty;

        [Column(TypeName = "datetime(6)")]
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByUserId { get; set; }

        public virtual Submission Submission { get; set; } = null!;
        public virtual Issue Issue { get; set; } = null!;
    }
}

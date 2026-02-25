using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IJULR.Web.Models
{
    // ==================== LOGIN ====================
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    // ==================== SUBMIT PAPER ====================
    public class SubmitPaperViewModel
    {
        [Required, Display(Name = "Paper Title")]
        public string PaperTitle { get; set; } = string.Empty;

        [Required, Display(Name = "Category")]
        public string Category { get; set; } = "Research Article";

        [Required, Display(Name = "Abstract"), MinLength(100, ErrorMessage = "Abstract must be at least 100 characters")]
        public string Abstract { get; set; } = string.Empty;

        [Required, Display(Name = "Keywords")]
        public string Keywords { get; set; } = string.Empty;

        [Required, Display(Name = "Manuscript (PDF/Word)")]
        public IFormFile ManuscriptFile { get; set; }

        [Required]
        public AuthorViewModel Author1 { get; set; } = new() { AuthorOrder = 1, IsCorresponding = true };

        public AuthorViewModel? Author2 { get; set; } = new() { AuthorOrder = 2 };
        public AuthorViewModel? Author3 { get; set; } = new() { AuthorOrder = 3 };

        public static List<SelectListItem> Categories => new()
        {
            new("Research Article", "Research Article"),
            new("Short Article/Essay", "Short Article"),
            new("Case Note", "Case Note"),
            new("Book Review", "Book Review")
        };
    }

    public class AuthorViewModel
    {
        public int AuthorOrder { get; set; } = 1;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Institution { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string? Country { get; set; }
        public bool IsCorresponding { get; set; }
    }

    // ==================== TRACK SUBMISSION ====================
    public class TrackSubmissionViewModel
    {
        [Required, Display(Name = "Tracking ID")]
        public string TrackingId { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        public string? PublishedPdfPath { get; set; }
    }

    // ==================== PAYMENT UPLOAD ====================
    public class PaymentUploadViewModel
    {
        public int SubmissionId { get; set; }
        public string TrackingId { get; set; } = string.Empty;
        public string PaperTitle { get; set; } = string.Empty;

        [Required, Display(Name = "Payment Receipt")]
        public IFormFile? ReceiptFile { get; set; }
    }

    // ==================== ADMIN DASHBOARD ====================
    public class AdminDashboardViewModel
    {
        public int TotalSubmissions { get; set; }
        public int Submitted { get; set; }
        public int UnderReview { get; set; }
        public int Accepted { get; set; }
        public int Rejected { get; set; }
        public int Published { get; set; }
        public int PaymentPending { get; set; }
        public int TotalReviewers { get; set; }
        public List<Submission> RecentSubmissions { get; set; } = new();
    }

    // ==================== SUBMISSION LIST ====================
    public class SubmissionListViewModel
    {
        public List<Submission> Submissions { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        public static List<string> Statuses => new() { "Submitted", "UnderReview", "Revision", "Accepted", "Rejected", "PaymentPending", "Published" };
    }

    // ==================== SUBMISSION DETAILS ====================
    public class SubmissionDetailsViewModel
    {
        public Submission Submission { get; set; } = null!;
        public List<ReviewAssignment> Reviews { get; set; } = new();
        public List<SubmissionStatusHistory> History { get; set; } = new();
        public List<Issue> AvailableIssues { get; set; } = new();
        public List<Reviewer> AvailableReviewers { get; set; } = new();
        public List<PublishedVersion> PublishedVersions { get; set; } = new();
    }

    // ==================== PUBLISH PAPER ====================
    public class PublishPaperViewModel
    {
        public int SubmissionId { get; set; }
        public string PaperTitle { get; set; } = string.Empty;
        public string Authors { get; set; } = string.Empty;

        [Required, Display(Name = "Issue")]
        public int IssueId { get; set; }

        [Required, Display(Name = "DOI")]
        public string DOI { get; set; } = string.Empty;

        [Required, Display(Name = "Page Start")]
        public int PageStart { get; set; }

        [Required, Display(Name = "Page End")]
        public int PageEnd { get; set; }

        [Display(Name = "Copyright Information")]
        public string? CopyrightInfo { get; set; }

        [Display(Name = "Creative Comments")]
        public string? CreativeComments { get; set; }

        public List<SelectListItem> Issues { get; set; } = new();
    }

    // ==================== CREATE REVIEWER ====================
    public class CreateReviewerViewModel
    {
        [Required, Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Institution { get; set; }
        public string? Specialization { get; set; }
    }

    // ==================== SITE SETTINGS ====================
    public class SiteSettingsViewModel
    {
        public string JournalName { get; set; } = string.Empty;
        public string JournalAbbreviation { get; set; } = string.Empty;
        public string? ISSN { get; set; }
        public string? ImpactFactor { get; set; }
        public string? PlagiarismSoftware { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactAddress { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? CopyrightText { get; set; }
        public IFormFile? LogoFile { get; set; }
        public IFormFile? PaymentQRFile { get; set; }
    }

    // ==================== PAGE EDIT ====================
    public class PageEditViewModel
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        [Required]
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? MetaDescription { get; set; }
        public bool IsPublished { get; set; }
    }

    // ==================== REVIEWER DASHBOARD ====================
    public class ReviewerDashboardViewModel
    {
        public int PendingReviews { get; set; }
        public int CompletedReviews { get; set; }
        public List<ReviewAssignment> Assignments { get; set; } = new();
    }

    // ==================== SUBMIT REVIEW ====================
    public class SubmitReviewViewModel
    {
        public int AssignmentId { get; set; }
        public int SubmissionId { get; set; }
        public string PaperTitle { get; set; } = string.Empty;
        public string? Abstract { get; set; }

        [Required, Range(1, 5)]
        public int Score { get; set; }

        [Required]
        public string Recommendation { get; set; } = string.Empty;

        [Required, MinLength(50)]
        public string Comments { get; set; } = string.Empty;

        public static List<SelectListItem> Recommendations => new()
        {
            new("Accept", "Accept"),
            new("Minor Revision", "MinorRevision"),
            new("Major Revision", "MajorRevision"),
            new("Reject", "Reject")
        };
    }

    // ==================== CURRENT ISSUE ====================
    public class CurrentIssueViewModel
    {
        public Issue? Issue { get; set; }
        public List<Submission> Papers { get; set; } = new();
    }

    // ==================== ARCHIVES ====================
    public class ArchivesViewModel
    {
        public List<VolumeArchiveItem> Volumes { get; set; } = new();
    }

    public class VolumeArchiveItem
    {
        public int VolumeNumber { get; set; }
        public int Year { get; set; }
        public List<IssueArchiveItem> Issues { get; set; } = new();
    }

    public class IssueArchiveItem
    {
        public int Id { get; set; }
        public int IssueNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string QuarterName { get; set; } = string.Empty;
        public int PaperCount { get; set; }
    }

    // ==================== PAPER DETAILS ====================
    public class PaperDetailsViewModel
    {
        public Submission Paper { get; set; } = null!;
        public string Citation { get; set; } = string.Empty;
    }

    // ==================== EDITORIAL BOARD MEMBER ====================
    public class EditorialBoardMemberViewModel
    {
        public int Id { get; set; }
        [Required]
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Institution { get; set; }
        public string? Designation { get; set; }
        public string? Specialization { get; set; }
        public string? Bio { get; set; }
        public string BoardCategory { get; set; } = "EditorialBoard";
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public IFormFile? ProfileImage { get; set; }

        public static List<SelectListItem> Categories => new()
        {
            new("Chief Editor", "ChiefEditor"),
            new("Associate Editor", "AssociateEditor"),
            new("Advisory Board", "AdvisoryBoard"),
            new("Editorial Board", "EditorialBoard")
        };
    }


}

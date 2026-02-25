using IJULR.Web.Data;
using IJULR.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IJULR.Web.Controllers
{
    [Route("reviewer")]
    public class ReviewerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ReviewerController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private int? ReviewerId => HttpContext.Session.GetInt32("ReviewerId");
        private bool IsLoggedIn => ReviewerId.HasValue;

        [HttpGet("login")]
        public IActionResult Login() => View();

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            string correctHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            Console.WriteLine(correctHash);
            if (!ModelState.IsValid) return View(model);

            var user = await _db.Users
                .Include(u => u.Reviewer)
                .FirstOrDefaultAsync(u => u.Email == model.Email && u.IsActive && u.Role == "Reviewer");

            if (user == null || user.Reviewer == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View(model);
            }

            HttpContext.Session.SetInt32("ReviewerId", user.Reviewer.Id);
            HttpContext.Session.SetInt32("ReviewerUserId", user.Id);
            HttpContext.Session.SetString("ReviewerName", user.FullName);

            return RedirectToAction("Dashboard");
        }

        [HttpGet("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var assignments = await _db.ReviewAssignments
                .Include(a => a.Submission).ThenInclude(s => s.Authors)
                .Where(a => a.ReviewerId == ReviewerId)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            ViewBag.ReviewerName = HttpContext.Session.GetString("ReviewerName");

            return View(new ReviewerDashboardViewModel
            {
                PendingReviews = assignments.Count(a => a.Status == "Assigned" || a.Status == "InProgress"),
                CompletedReviews = assignments.Count(a => a.Status == "Completed"),
                Assignments = assignments
            });
        }

        [HttpGet("review/{id}")]
        public async Task<IActionResult> Review(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var assignment = await _db.ReviewAssignments
                .Include(a => a.Submission)
                .FirstOrDefaultAsync(a => a.Id == id && a.ReviewerId == ReviewerId);

            if (assignment == null) return NotFound();

            return View(new SubmitReviewViewModel
            {
                AssignmentId = id,
                SubmissionId = assignment.SubmissionId,
                PaperTitle = assignment.Submission.PaperTitle,
                Abstract = assignment.Submission.Abstract
            });
        }

        [HttpPost("review/{id}")]
        public async Task<IActionResult> Review(int id, SubmitReviewViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            if (!ModelState.IsValid) return View(model);

            var assignment = await _db.ReviewAssignments
                .Include(a => a.Reviewer)
                .FirstOrDefaultAsync(a => a.Id == id && a.ReviewerId == ReviewerId);

            if (assignment == null) return NotFound();

            assignment.Score = model.Score;
            assignment.Comments = model.Comments;
            assignment.Recommendation = model.Recommendation;
            assignment.Status = "Completed";
            assignment.CompletedAt = DateTime.UtcNow;

            assignment.Reviewer.CurrentAssignments = Math.Max(0, assignment.Reviewer.CurrentAssignments - 1);
            assignment.Reviewer.TotalReviews++;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Review submitted successfully";
            return RedirectToAction("Dashboard");
        }

        [HttpGet("review-details/{id}")]
        public async Task<IActionResult> ReviewDetails(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var assignment = await _db.ReviewAssignments
                .Include(a => a.Submission).ThenInclude(s => s.Authors.OrderBy(x => x.AuthorOrder))
                .Include(a => a.Submission).ThenInclude(s => s.StatusHistory.OrderByDescending(h => h.ChangedAt))
                .FirstOrDefaultAsync(a => a.Id == id && a.ReviewerId == ReviewerId);

            if (assignment == null) return NotFound();

            ViewBag.ReviewerName = HttpContext.Session.GetString("ReviewerName");
            return View(assignment);
        }
        [HttpGet("manuscript/{id}")]
        public async Task<IActionResult> ViewManuscript(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var assignment = await _db.ReviewAssignments
                .Include(a => a.Submission)
                .FirstOrDefaultAsync(a => a.Id == id && a.ReviewerId == ReviewerId);

            if (assignment == null || string.IsNullOrEmpty(assignment.Submission.ManuscriptFilePath))
                return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, assignment.Submission.ManuscriptFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, "application/pdf");
        }
    }
}

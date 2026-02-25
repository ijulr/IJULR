using IJULR.Web.Data;
using IJULR.Web.Models;
using IJULR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IJULR.Web.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly INotificationService _notification;

        public SubmissionController(ApplicationDbContext db, IFileStorageService fileStorage, INotificationService notification)
        {
            _db = db;
            _fileStorage = fileStorage;
            _notification = notification;
        }

        private async Task LoadViewBag()
        {
            ViewBag.Settings = await _db.SiteSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "");
        }

        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            await LoadViewBag();
            return View(new SubmitPaperViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Submit(SubmitPaperViewModel model)
        {
            // Validate
            if (model.ManuscriptFile == null || model.ManuscriptFile.Length == 0)
                ModelState.AddModelError("ManuscriptFile", "Please upload your manuscript");

            if (string.IsNullOrEmpty(model.Author1.FullName) || string.IsNullOrEmpty(model.Author1.Email))
                ModelState.AddModelError("", "Primary author name and email are required");

            if (string.IsNullOrWhiteSpace(model.Author2?.FullName))
            {
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Author2.")).ToList())
                {
                    ModelState.Remove(key);
                }
                model.Author2 = null;
            }

            if (string.IsNullOrWhiteSpace(model.Author3?.FullName))
            {
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Author3.")).ToList())
                {
                    ModelState.Remove(key);
                }
                model.Author3 = null;
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Generate unique tracking ID using MAX to avoid duplicates
            var year = DateTime.UtcNow.Year;
            var prefix = $"IJULR-{year}-";

            // Get the highest tracking number for this year
            var lastTrackingId = await _db.Submissions
                .Where(s => s.TrackingId.StartsWith(prefix))
                .OrderByDescending(s => s.TrackingId)
                .Select(s => s.TrackingId)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastTrackingId))
            {
                // Extract the number part (e.g., "IJULR-2026-0015" -> 15)
                var numberPart = lastTrackingId.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            var trackingId = $"{prefix}{nextNumber:D4}";

            // Save file
            var filePath = await _fileStorage.SaveFileAsync(model.ManuscriptFile!, "manuscripts");

            // Create submission
            var submission = new Submission
            {
                TrackingId = trackingId,
                PaperTitle = model.PaperTitle,
                Abstract = model.Abstract,
                Keywords = model.Keywords,
                Category = model.Category,
                ManuscriptFileName = model.ManuscriptFile!.FileName,
                ManuscriptFilePath = filePath,
                ManuscriptFileSize = model.ManuscriptFile.Length,
                Status = "Submitted"
            };

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();

            // Add authors
            AddAuthor(submission.Id, model.Author1, true);
            if (!string.IsNullOrEmpty(model.Author2?.FullName)) AddAuthor(submission.Id, model.Author2, false);
            if (!string.IsNullOrEmpty(model.Author3?.FullName)) AddAuthor(submission.Id, model.Author3, false);

            // Add status history
            _db.SubmissionStatusHistory.Add(new SubmissionStatusHistory
            {
                SubmissionId = submission.Id,
                ToStatus = "Submitted",
                Remarks = "Paper submitted successfully"
            });

            await _db.SaveChangesAsync();

            // Load authors for notification
            submission = await _db.Submissions.Include(s => s.Authors).FirstOrDefaultAsync(s => s.Id == submission.Id);

            // Send notifications
            await _notification.NotifySubmissionReceived(submission!);

            return RedirectToAction("Success", new { id = submission!.Id });
        }

        private void AddAuthor(int submissionId, AuthorViewModel model, bool isCorresponding)
        {
            _db.Authors.Add(new Author
            {
                SubmissionId = submissionId,
                AuthorOrder = model.AuthorOrder,
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Institution = model.Institution,
                Designation = model.Designation,
                Country = model.Country,
                IsCorresponding = isCorresponding
            });
        }

        public async Task<IActionResult> Success(int id)
        {
            var submission = await _db.Submissions.Include(s => s.Authors).FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null) return NotFound();

            await LoadViewBag();
            return View(submission);
        }

        [HttpGet]
        public async Task<IActionResult> Track()
        {
            await LoadViewBag();
            return View(new TrackSubmissionViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Track(TrackSubmissionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadViewBag();
                return View(model);
            }

            // Debug: Check if submission exists at all
            var submissionOnly = await _db.Submissions
                .FirstOrDefaultAsync(s => s.TrackingId == model.TrackingId.ToUpper());

            // Debug: Check if authors are loaded
            var submissionWithAuthors = await _db.Submissions
                .Include(s => s.Authors)
                .FirstOrDefaultAsync(s => s.TrackingId == model.TrackingId.ToUpper());

            var authorCount = submissionWithAuthors?.Authors?.Count ?? 0;
            var authorEmails = submissionWithAuthors?.Authors?.Select(a => a.Email).ToList();

            System.Diagnostics.Debug.WriteLine($"Submission exists: {submissionOnly != null}");
            System.Diagnostics.Debug.WriteLine($"Author count: {authorCount}");
            System.Diagnostics.Debug.WriteLine($"Author emails: {string.Join(", ", authorEmails ?? new List<string>())}");
            System.Diagnostics.Debug.WriteLine($"Input email: {model.Email.ToLower()}");

            // Original query
            var submission = await _db.Submissions
                .Include(s => s.Authors)
                .Include(s => s.StatusHistory.OrderByDescending(h => h.ChangedAt))
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .FirstOrDefaultAsync(s => s.TrackingId == model.TrackingId.ToUpper() &&
                    s.Authors.Any(a => a.Email.ToLower() == model.Email.ToLower()));

            if (submission == null)
            {
                var debugMsg = $"Submission exists: {submissionOnly != null}, Author count: {authorCount}, Emails in DB: {string.Join(", ", authorEmails ?? new List<string>())}, Input: {model.Email.ToLower()}";
                ModelState.AddModelError("", debugMsg);
                await LoadViewBag();
                return View(model);
            }

            return RedirectToAction("Status", new { id = submission.Id, email = model.Email });
        }

        public async Task<IActionResult> Status(int id, string email)
        {
            var submission = await _db.Submissions
                .Include(s => s.Authors)
                .Include(s => s.StatusHistory.OrderByDescending(h => h.ChangedAt))
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound();

            // Verify email
            if (!submission.Authors.Any(a => a.Email.ToLower() == email.ToLower()))
                return Forbid();

            await LoadViewBag();
            return View(submission);
        }

        [HttpGet]
        public async Task<IActionResult> UploadPayment(int id, string email)
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null) return NotFound();

            var model = new PaymentUploadViewModel
            {
                SubmissionId = id,
                TrackingId = submission.TrackingId,
                PaperTitle = submission.PaperTitle
            };

            await LoadViewBag();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UploadPayment(PaymentUploadViewModel model)
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == model.SubmissionId);
            if (submission == null) return NotFound();

            if (model.ReceiptFile == null)
            {
                ModelState.AddModelError("ReceiptFile", "Please upload payment receipt");
                await LoadViewBag();
                return View(model);
            }

            var filePath = await _fileStorage.SaveFileAsync(model.ReceiptFile, "receipts");

            submission.PaymentReceiptFileName = model.ReceiptFile.FileName;
            submission.PaymentReceiptFilePath = filePath;
            submission.PaymentStatus = "Uploaded";
            submission.PaymentDate = DateTime.UtcNow;

            _db.SubmissionStatusHistory.Add(new SubmissionStatusHistory
            {
                SubmissionId = submission.Id,
                FromStatus = submission.Status,
                ToStatus = "PaymentPending",
                Remarks = "Payment receipt uploaded by author"
            });

            submission.Status = "PaymentPending";
            await _db.SaveChangesAsync();

            TempData["Success"] = "Payment receipt uploaded successfully!";
            return RedirectToAction("Track");
        }

        [HttpGet]
        public async Task<IActionResult> Resubmit(int id, string email)
        {
            var submission = await _db.Submissions.Include(s => s.Authors).FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null || submission.Status != "Revision") return NotFound();

            await LoadViewBag();
            return View(submission);
        }

        [HttpPost]
        public async Task<IActionResult> Resubmit(int id, IFormFile manuscript)
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null) return NotFound();

            if (manuscript == null)
            {
                ModelState.AddModelError("", "Please upload revised manuscript");
                await LoadViewBag();
                return View(submission);
            }

            var filePath = await _fileStorage.SaveFileAsync(manuscript, "manuscripts");

            submission.ManuscriptFileName = manuscript.FileName;
            submission.ManuscriptFilePath = filePath;
            submission.ManuscriptFileSize = manuscript.Length;
            submission.Status = "Submitted";
            submission.UpdatedAt = DateTime.UtcNow;

            _db.SubmissionStatusHistory.Add(new SubmissionStatusHistory
            {
                SubmissionId = submission.Id,
                FromStatus = "Revision",
                ToStatus = "Submitted",
                Remarks = "Revised manuscript submitted"
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "Revised manuscript submitted successfully!";
            return RedirectToAction("Track");
        }
    }
}
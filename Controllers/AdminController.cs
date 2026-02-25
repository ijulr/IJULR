using IJULR.Web.Data;
using IJULR.Web.Models;
using IJULR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace IJULR.Web.Controllers
{
    [Route("editorial")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly INotificationService _notification;
        private readonly IWebHostEnvironment _env;

        public AdminController(ApplicationDbContext db, IFileStorageService fileStorage, INotificationService notification, IWebHostEnvironment env)
        {
            _db = db;
            _fileStorage = fileStorage;
            _notification = notification;
            _env = env;
        }

        private int? UserId => HttpContext.Session.GetInt32("UserId");
        private bool IsLoggedIn => UserId.HasValue;

        private IActionResult CheckLogin() => IsLoggedIn ? null! : RedirectToAction("Login");

        [HttpGet("login")]
        public IActionResult Login() => View();

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Email == model.Email && u.IsActive && (u.Role == "Admin" || u.Role == "Editor"));

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View(model);
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserRole", user.Role);

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

            var model = new AdminDashboardViewModel
            {
                TotalSubmissions = await _db.Submissions.CountAsync(),
                Submitted = await _db.Submissions.CountAsync(s => s.Status == "Submitted"),
                UnderReview = await _db.Submissions.CountAsync(s => s.Status == "UnderReview"),
                Accepted = await _db.Submissions.CountAsync(s => s.Status == "Accepted"),
                Rejected = await _db.Submissions.CountAsync(s => s.Status == "Rejected"),
                Published = await _db.Submissions.CountAsync(s => s.Status == "Published"),
                PaymentPending = await _db.Submissions.CountAsync(s => s.Status == "PaymentPending"),
                TotalReviewers = await _db.Reviewers.CountAsync(),
                RecentSubmissions = await _db.Submissions
                    .Include(s => s.Authors)
                    .OrderByDescending(s => s.SubmittedAt)
                    .Take(10)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpGet("submissions")]
        public async Task<IActionResult> Submissions(string? search, string? status, int page = 1)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var query = _db.Submissions.Include(s => s.Authors).AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.TrackingId.Contains(search) || s.PaperTitle.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);

            var total = await query.CountAsync();
            var submissions = await query.OrderByDescending(s => s.SubmittedAt).Skip((page - 1) * 20).Take(20).ToListAsync();

            return View(new SubmissionListViewModel
            {
                Submissions = submissions,
                SearchTerm = search,
                StatusFilter = status,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(total / 20.0),
                TotalCount = total
            });
        }

        [HttpGet("submission/{id}")]
        public async Task<IActionResult> SubmissionDetails(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions
                .Include(s => s.Authors.OrderBy(a => a.AuthorOrder))
                .Include(s => s.StatusHistory.OrderByDescending(h => h.ChangedAt))
                .Include(s => s.ReviewAssignments).ThenInclude(r => r.Reviewer).ThenInclude(r => r.User)
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .Include(s => s.PublishedVersions).ThenInclude(pv => pv.Issue).ThenInclude(i => i.Volume)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound();

            return View(new SubmissionDetailsViewModel
            {
                Submission = submission,
                Reviews = submission.ReviewAssignments.ToList(),
                History = submission.StatusHistory.ToList(),
                PublishedVersions = submission.PublishedVersions.OrderByDescending(pv => pv.PublishedAt).ToList(),
                AvailableIssues = await _db.Issues.Include(i => i.Volume).Where(i => !i.IsDeleted).OrderByDescending(i => i.Volume.Year).ThenBy(i => i.IssueNumber).ToListAsync(),
                AvailableReviewers = await _db.Reviewers.Include(r => r.User).Where(r => r.IsAvailable).ToListAsync()
            });
        }

        [HttpPost("submission/{id}/status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus, string? remarks, int? issueId, int? pageStart, int? pageEnd, int? publishedVersionId, IFormFile? PaymentQRFile, IFormFile? CertificateFile)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions
                .Include(s => s.Authors)
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .Include(s => s.PublishedVersions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound();

            // Convert files to bytes
            byte[]? qrBytes = null;
            byte[]? certificateBytes = null;

            if (PaymentQRFile != null && PaymentQRFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await PaymentQRFile.CopyToAsync(ms);
                qrBytes = ms.ToArray();
            }

            if (CertificateFile != null && CertificateFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await CertificateFile.CopyToAsync(ms);
                certificateBytes = ms.ToArray();

                // Save certificate to storage (you want to keep this for download later)
                submission.CertificateFilePath = await _fileStorage.SaveFileAsync(CertificateFile, "certificates");
            }

            var oldStatus = submission.Status;
            submission.Status = newStatus;
            submission.EditorRemarks = remarks;
            submission.UpdatedAt = DateTime.UtcNow;

            if (newStatus == "Revision")
            {
                submission.RevisionComments = remarks;
            }

            if (newStatus == "Published" && issueId.HasValue && pageStart.HasValue && pageEnd.HasValue)
            {
                submission.IssueId = issueId;
                submission.PageStart = pageStart;
                submission.PageEnd = pageEnd;
                submission.PublishedAt = DateTime.UtcNow;

                string? publishedPdfPath = null;

                if (!string.IsNullOrEmpty(submission.ManuscriptFilePath))
                {
                    try
                    {
                        var originalPath = Path.Combine(_env.WebRootPath, submission.ManuscriptFilePath.TrimStart('/'));

                        if (System.IO.File.Exists(originalPath))
                        {
                            var publishedFileName = $"published_{submission.TrackingId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                            var publishedFolder = Path.Combine(_env.WebRootPath, "uploads", "published");
                            Directory.CreateDirectory(publishedFolder);
                            var publishedPath = Path.Combine(publishedFolder, publishedFileName);

                            ExtractPdfPages(originalPath, publishedPath, pageStart.Value, pageEnd.Value);

                            publishedPdfPath = $"/uploads/published/{publishedFileName}";
                            submission.PublishedPdfPath = publishedPdfPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        TempData["Warning"] = $"Status updated but PDF extraction failed: {ex.Message}";
                    }
                }

                if (publishedVersionId.HasValue)
                {
                    var existingVersion = await _db.PublishedVersions.FindAsync(publishedVersionId.Value);
                    if (existingVersion != null)
                    {
                        if (!string.IsNullOrEmpty(existingVersion.PublishedPdfPath))
                        {
                            var oldPdfPath = Path.Combine(_env.WebRootPath, existingVersion.PublishedPdfPath.TrimStart('/'));
                            if (System.IO.File.Exists(oldPdfPath))
                            {
                                System.IO.File.Delete(oldPdfPath);
                            }
                        }

                        existingVersion.IssueId = issueId.Value;
                        existingVersion.PageStart = pageStart.Value;
                        existingVersion.PageEnd = pageEnd.Value;
                        existingVersion.PublishedPdfPath = publishedPdfPath ?? existingVersion.PublishedPdfPath;
                        existingVersion.PublishedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    _db.PublishedVersions.Add(new PublishedVersion
                    {
                        SubmissionId = id,
                        IssueId = issueId.Value,
                        PageStart = pageStart.Value,
                        PageEnd = pageEnd.Value,
                        PublishedPdfPath = publishedPdfPath ?? "",
                        CreatedByUserId = UserId
                    });
                }
            }


            _db.SubmissionStatusHistory.Add(new SubmissionStatusHistory
            {
                SubmissionId = id,
                FromStatus = oldStatus,
                ToStatus = newStatus,
                ChangedByUserId = UserId,
                Remarks = remarks
            });

            await _db.SaveChangesAsync();

            // Pass bytes directly to notification
            await _notification.NotifyStatusChanged(submission, oldStatus, newStatus, qrBytes, certificateBytes);

            TempData["Success"] = $"Status updated to {newStatus}";
            return RedirectToAction("SubmissionDetails", new { id });
        }

        private void ExtractPdfPages(string sourcePath, string destPath, int startPage, int endPage)
        {
            using var inputDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
            using var outputDocument = new PdfDocument();

            int start = Math.Max(0, startPage - 1);
            int end = Math.Min(inputDocument.PageCount, endPage);

            for (int i = start; i < end; i++)
            {
                outputDocument.AddPage(inputDocument.Pages[i]);
            }

            outputDocument.Save(destPath);
        }

        [HttpPost("submission/{id}/add-publication")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPublication(int id, int issueId, int pageStart, int pageEnd)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions.FindAsync(id);
            if (submission == null) return NotFound();

            if (submission.Status != "Published")
            {
                TempData["Error"] = "Submission must be published first";
                return RedirectToAction("SubmissionDetails", new { id });
            }

            string? publishedPdfPath = null;

            if (!string.IsNullOrEmpty(submission.ManuscriptFilePath))
            {
                try
                {
                    var originalPath = Path.Combine(_env.WebRootPath, submission.ManuscriptFilePath.TrimStart('/'));

                    if (System.IO.File.Exists(originalPath))
                    {
                        var publishedFileName = $"published_{submission.TrackingId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                        var publishedFolder = Path.Combine(_env.WebRootPath, "uploads", "published");
                        Directory.CreateDirectory(publishedFolder);
                        var publishedPath = Path.Combine(publishedFolder, publishedFileName);

                        ExtractPdfPages(originalPath, publishedPath, pageStart, pageEnd);

                        publishedPdfPath = $"/uploads/published/{publishedFileName}";
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"PDF extraction failed: {ex.Message}";
                    return RedirectToAction("SubmissionDetails", new { id });
                }
            }

            _db.PublishedVersions.Add(new PublishedVersion
            {
                SubmissionId = id,
                IssueId = issueId,
                PageStart = pageStart,
                PageEnd = pageEnd,
                PublishedPdfPath = publishedPdfPath ?? "",
                CreatedByUserId = UserId
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "Additional publication added successfully";
            return RedirectToAction("SubmissionDetails", new { id });
        }

        [HttpPost("submission/{id}/delete-publication/{versionId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePublication(int id, int versionId)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var version = await _db.PublishedVersions.FindAsync(versionId);
            if (version == null || version.SubmissionId != id) return NotFound();

            if (!string.IsNullOrEmpty(version.PublishedPdfPath))
            {
                var pdfPath = Path.Combine(_env.WebRootPath, version.PublishedPdfPath.TrimStart('/'));
                if (System.IO.File.Exists(pdfPath))
                {
                    System.IO.File.Delete(pdfPath);
                }
            }

            _db.PublishedVersions.Remove(version);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Publication removed successfully";
            return RedirectToAction("SubmissionDetails", new { id });
        }

        [HttpPost("submission/{id}/assign-reviewer")]
        public async Task<IActionResult> AssignReviewer(int id, int reviewerId)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions.FindAsync(id);
            var reviewer = await _db.Reviewers.FindAsync(reviewerId);

            if (submission == null || reviewer == null) return NotFound();

            if (await _db.ReviewAssignments.AnyAsync(r => r.SubmissionId == id && r.ReviewerId == reviewerId))
            {
                TempData["Error"] = "Reviewer already assigned";
                return RedirectToAction("SubmissionDetails", new { id });
            }

            _db.ReviewAssignments.Add(new ReviewAssignment
            {
                SubmissionId = id,
                ReviewerId = reviewerId,
                AssignedByUserId = UserId,
                DueDate = DateTime.UtcNow.AddDays(7)
            });

            reviewer.CurrentAssignments++;

            if (submission.Status == "Submitted")
            {
                submission.Status = "UnderReview";
                _db.SubmissionStatusHistory.Add(new SubmissionStatusHistory
                {
                    SubmissionId = id,
                    FromStatus = "Submitted",
                    ToStatus = "UnderReview",
                    ChangedByUserId = UserId,
                    Remarks = "Assigned to reviewer"
                });
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Reviewer assigned successfully";
            return RedirectToAction("SubmissionDetails", new { id });
        }

        [HttpGet("submission/{id}/publish")]
        public async Task<IActionResult> PublishPaper(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions
                .Include(s => s.Authors)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound();

            var issues = await _db.Issues
                .Include(i => i.Volume)
                .OrderByDescending(i => i.Volume.Year)
                .ThenBy(i => i.IssueNumber)
                .ToListAsync();

            return View(new PublishPaperViewModel
            {
                SubmissionId = id,
                PaperTitle = submission.PaperTitle,
                Authors = string.Join(", ", submission.Authors.OrderBy(a => a.AuthorOrder).Select(a => a.FullName)),
                Issues = issues.Select(i => new SelectListItem(
                    $"Vol. {i.Volume.VolumeNumber}, Issue {i.IssueNumber} - {i.Title}",
                    i.Id.ToString(),
                    i.Id == submission.IssueId // Set selected if matches
                )).ToList(),

                // Populate existing data
                IssueId = submission.IssueId ?? 0,
                DOI = submission.DOI ?? "",
                PageStart = submission.PageStart ?? 0,
                PageEnd = submission.PageEnd ?? 0,
                CreativeComments = submission.CreativeComments ?? "",
                CopyrightInfo = submission.CopyrightInfo ?? $"© {DateTime.Now.Year} Author(s). This is an open access article..."
            });
        }

        [HttpPost("submission/{id}/publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishPaper(int id, PublishPaperViewModel model, IFormFile CertificatePdf)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            var submission = await _db.Submissions.Include(s => s.Authors).FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null) return NotFound();

            // Validate certificate upload
            if (CertificatePdf == null || CertificatePdf.Length == 0)
            {
                TempData["Error"] = "Please upload a certificate PDF.";
                return RedirectToAction("PublishPaper", new { id });
            }

            // Read certificate bytes BEFORE saving
            byte[] certificateBytes;
            using (var ms = new MemoryStream())
            {
                await CertificatePdf.CopyToAsync(ms);
                certificateBytes = ms.ToArray();
            }

            var oldStatus = submission.Status;
            submission.IssueId = model.IssueId;
            submission.DOI = model.DOI;
            submission.PageStart = model.PageStart;
            submission.PageEnd = model.PageEnd;
            submission.CopyrightInfo = model.CopyrightInfo;
            submission.CreativeComments = model.CreativeComments;
            submission.Status = "Published";
            submission.PublishedAt = DateTime.UtcNow;
            submission.UpdatedAt = DateTime.UtcNow;

            // Save certificate PDF
            submission.CertificateFilePath = await _fileStorage.SaveFileAsync(CertificatePdf, "certificates");

            _db.SubmissionStatusHistory.Add(new SubmissionStatusHistory
            {
                SubmissionId = id,
                FromStatus = oldStatus,
                ToStatus = "Published",
                ChangedByUserId = UserId,
                ChangedAt = DateTime.UtcNow,
                Remarks = $"Published in Issue {model.IssueId}, Pages {model.PageStart}-{model.PageEnd}"
            });

            await _db.SaveChangesAsync();

            // Load issue for notification
            submission = await _db.Submissions
                .Include(s => s.Authors)
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .FirstOrDefaultAsync(s => s.Id == id);

            // Send notification WITH certificate attached
            await _notification.NotifyCertificateReady(submission!, certificateBytes);

            TempData["Success"] = "Paper published successfully! Certificate sent to author.";
            return RedirectToAction("SubmissionDetails", new { id });
        }
        [HttpPost("submission/{id}/update-certificate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCertificate(int id, IFormFile CertificatePdf)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions.FindAsync(id);
            if (submission == null) return NotFound();

            if (CertificatePdf != null && CertificatePdf.Length > 0)
            {
                // Delete old certificate if exists
                if (!string.IsNullOrEmpty(submission.CertificateFilePath))
                {
                    await _fileStorage.DeleteFileAsync(submission.CertificateFilePath);
                }

                submission.CertificateFilePath = await _fileStorage.SaveFileAsync(CertificatePdf, "certificates");
                submission.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                TempData["Success"] = "Certificate updated successfully!";
            }
            else
            {
                TempData["Error"] = "Please select a PDF file.";
            }

            return RedirectToAction("SubmissionDetails", new { id });
        }

        [HttpGet("reviewers")]
        public async Task<IActionResult> Reviewers()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            return View(await _db.Reviewers.Include(r => r.User).ToListAsync());
        }

        [HttpGet("reviewers/create")]
        public IActionResult CreateReviewer()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            return View(new CreateReviewerViewModel());
        }

        [HttpPost("reviewers/create")]
        public async Task<IActionResult> CreateReviewer(CreateReviewerViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            if (!ModelState.IsValid) return View(model);

            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email already exists");
                return View(model);
            }

            var user = new User
            {
                Email = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                Institution = model.Institution,
                Role = "Reviewer"
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _db.Reviewers.Add(new Reviewer { UserId = user.Id, Specialization = model.Specialization });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Reviewer created successfully";
            return RedirectToAction("Reviewers");
        }

        [HttpGet("volumes")]
        public async Task<IActionResult> Volumes()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            var volumes = await _db.Volumes
                .Where(v => !v.IsDeleted)
                .Include(v => v.Issues.Where(i => !i.IsDeleted))
                .OrderByDescending(v => v.Year)
                .ThenBy(v => v.VolumeNumber)
                .ToListAsync();
            return View(volumes);
        }

        [HttpPost("volumes/create")]
        public async Task<IActionResult> CreateVolume(int year)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var maxVolNum = await _db.Volumes
                .Where(v => !v.IsDeleted)
                .MaxAsync(v => (int?)v.VolumeNumber) ?? 0;

            var volNum = maxVolNum + 1;

            var existingVolume = await _db.Volumes
                .Include(v => v.Issues)
                .FirstOrDefaultAsync(v => v.VolumeNumber == volNum && v.IsDeleted);

            if (existingVolume != null)
            {
                existingVolume.IsDeleted = false;
                existingVolume.DeletedAt = null;
                existingVolume.Year = year;

                foreach (var issue in existingVolume.Issues)
                {
                    issue.IsDeleted = false;
                    issue.DeletedAt = null;
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = "Volume restored with 4 quarterly issues";
                return RedirectToAction("Volumes");
            }

            var volume = new Volume { VolumeNumber = volNum, Year = year };
            _db.Volumes.Add(volume);
            await _db.SaveChangesAsync();

            var quarters = new[] { ("January - March", "Q1", 1, 3), ("April - June", "Q2", 4, 6), ("July - September", "Q3", 7, 9), ("October - December", "Q4", 10, 12) };
            for (int i = 0; i < 4; i++)
            {
                _db.Issues.Add(new Issue
                {
                    VolumeId = volume.Id,
                    IssueNumber = i + 1,
                    Title = $"{quarters[i].Item1} {year}",
                    QuarterName = quarters[i].Item2,
                    StartMonth = quarters[i].Item3,
                    EndMonth = quarters[i].Item4,
                    IsCurrentIssue = i == 0
                });
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = "Volume created with 4 quarterly issues";
            return RedirectToAction("Volumes");
        }

        [HttpPost("DeleteVolume")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVolume([FromForm] int id)
        {
            var volume = await _db.Volumes
                .Include(v => v.Issues)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (volume == null)
            {
                TempData["Error"] = "Volume not found.";
                return RedirectToAction("Volumes");
            }

            var hasPublishedContent = volume.Issues.Any(i => i.IsCurrentIssue || i.IsPublished);
            if (hasPublishedContent)
            {
                TempData["Error"] = "Cannot delete volume with current or published issues.";
                return RedirectToAction("Volumes");
            }

            volume.IsDeleted = true;
            volume.DeletedAt = DateTime.UtcNow;

            foreach (var issue in volume.Issues)
            {
                issue.IsDeleted = true;
                issue.DeletedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Volume {volume.VolumeNumber} ({volume.Year}) deleted successfully.";
            return RedirectToAction("Volumes");
        }

        [HttpPost("issues/{id}/set-current")]
        public async Task<IActionResult> SetCurrentIssue(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            await _db.Issues.ForEachAsync(i => i.IsCurrentIssue = false);
            var issue = await _db.Issues.FindAsync(id);
            if (issue != null) issue.IsCurrentIssue = true;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Current issue updated";
            return RedirectToAction("Volumes");
        }

        [HttpGet("pages")]
        public async Task<IActionResult> Pages()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            return View(await _db.Pages.OrderBy(p => p.ParentMenu).ThenBy(p => p.MenuOrder).ToListAsync());
        }

        [HttpGet("pages/{id}/edit")]
        public async Task<IActionResult> EditPage(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var page = await _db.Pages.FindAsync(id);
            if (page == null) return NotFound();

            return View(new PageEditViewModel
            {
                Id = page.Id,
                Slug = page.Slug,
                Title = page.Title,
                Content = page.Content,
                MetaDescription = page.MetaDescription,
                IsPublished = page.IsPublished
            });
        }

        [HttpPost("pages/{id}/edit")]
        public async Task<IActionResult> EditPage(int id, PageEditViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var page = await _db.Pages.FindAsync(id);
            if (page == null) return NotFound();

            page.Title = model.Title;
            page.Content = model.Content;
            page.MetaDescription = model.MetaDescription;
            page.IsPublished = model.IsPublished;
            page.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Page updated";
            return RedirectToAction("Pages");
        }

        [HttpGet("settings")]
        public async Task<IActionResult> Settings()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var settings = await _db.SiteSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "");

            return View(new SiteSettingsViewModel
            {
                JournalName = settings.GetValueOrDefault("JournalName", ""),
                JournalAbbreviation = settings.GetValueOrDefault("JournalAbbreviation", ""),
                ISSN = settings.GetValueOrDefault("ISSN", ""),
                ImpactFactor = settings.GetValueOrDefault("ImpactFactor", ""),
                PlagiarismSoftware = settings.GetValueOrDefault("PlagiarismSoftware", ""),
                ContactEmail = settings.GetValueOrDefault("ContactEmail", ""),
                ContactPhone = settings.GetValueOrDefault("ContactPhone", ""),
                ContactAddress = settings.GetValueOrDefault("ContactAddress", ""),
                WebsiteUrl = settings.GetValueOrDefault("WebsiteUrl", ""),
                CopyrightText = settings.GetValueOrDefault("CopyrightText", "")
            });
        }

        [HttpPost("settings")]
        public async Task<IActionResult> Settings(SiteSettingsViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            await UpdateSetting("JournalName", model.JournalName);
            await UpdateSetting("JournalAbbreviation", model.JournalAbbreviation);
            await UpdateSetting("ISSN", model.ISSN);
            await UpdateSetting("ImpactFactor", model.ImpactFactor);
            await UpdateSetting("PlagiarismSoftware", model.PlagiarismSoftware);
            await UpdateSetting("ContactEmail", model.ContactEmail);
            await UpdateSetting("ContactPhone", model.ContactPhone);
            await UpdateSetting("ContactAddress", model.ContactAddress);
            await UpdateSetting("WebsiteUrl", model.WebsiteUrl);
            await UpdateSetting("CopyrightText", model.CopyrightText);

            if (model.LogoFile != null)
            {
                var path = await _fileStorage.SaveFileAsync(model.LogoFile, "images");
                await UpdateSetting("LogoPath", path);
            }

            if (model.PaymentQRFile != null)
            {
                var path = await _fileStorage.SaveFileAsync(model.PaymentQRFile, "images");
                await UpdateSetting("PaymentQRCodePath", path);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Settings saved";
            return RedirectToAction("Settings");
        }

        private async Task UpdateSetting(string key, string? value)
        {
            var setting = await _db.SiteSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting != null)
            {
                setting.SettingValue = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }

        [HttpGet("editorial-board")]
        public async Task<IActionResult> EditorialBoard()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var members = await _db.EditorialBoardMembers
                .OrderBy(m => m.BoardCategory)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            return View(members);
        }

        [HttpGet("editorial-board/create")]
        public IActionResult CreateBoardMember()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            return View(new EditorialBoardMemberViewModel());
        }

        [HttpPost("editorial-board/create")]
        public async Task<IActionResult> CreateBoardMember(EditorialBoardMemberViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var member = new EditorialBoardMember
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Institution = model.Institution,
                Designation = model.Designation,
                Specialization = model.Specialization,
                Bio = model.Bio,
                BoardCategory = model.BoardCategory,
                DisplayOrder = model.DisplayOrder,
                IsActive = model.IsActive
            };

            if (model.ProfileImage != null)
                member.ProfileImagePath = await _fileStorage.SaveFileAsync(model.ProfileImage, "profiles");

            _db.EditorialBoardMembers.Add(member);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Board member added";
            return RedirectToAction("EditorialBoard");
        }

        [HttpGet("editorial-board/edit/{id}")]
        public async Task<IActionResult> EditBoardMember(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var member = await _db.EditorialBoardMembers.FindAsync(id);
            if (member == null) return NotFound();

            var viewModel = new EditorialBoardMemberViewModel
            {
                Id = member.Id,
                FullName = member.FullName,
                Email = member.Email,
                Phone = member.Phone,
                Institution = member.Institution,
                Designation = member.Designation,
                Specialization = member.Specialization,
                Bio = member.Bio,
                BoardCategory = member.BoardCategory,
                DisplayOrder = member.DisplayOrder,
                IsActive = member.IsActive
            };

            return View(viewModel);
        }

        [HttpPost("editorial-board/edit/{id}")]
        public async Task<IActionResult> EditBoardMember(int id, EditorialBoardMemberViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var member = await _db.EditorialBoardMembers.FindAsync(id);
            if (member == null) return NotFound();

            member.FullName = model.FullName;
            member.Email = model.Email;
            member.Phone = model.Phone;
            member.Institution = model.Institution;
            member.Designation = model.Designation;
            member.Specialization = model.Specialization;
            member.Bio = model.Bio;
            member.BoardCategory = model.BoardCategory;
            member.DisplayOrder = model.DisplayOrder;
            member.IsActive = model.IsActive;

            if (model.ProfileImage != null)
                member.ProfileImagePath = await _fileStorage.SaveFileAsync(model.ProfileImage, "profiles");

            await _db.SaveChangesAsync();

            TempData["Success"] = "Board member updated successfully";
            return RedirectToAction("EditorialBoard");
        }

        [HttpPost("editorial-board/delete/{id}")]
        public async Task<IActionResult> DeleteBoardMember(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var member = await _db.EditorialBoardMembers.FindAsync(id);
            if (member == null) return NotFound();

            _db.EditorialBoardMembers.Remove(member);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Board member deleted successfully";
            return RedirectToAction("EditorialBoard");
        }

        [HttpGet("email-logs")]
        public async Task<IActionResult> EmailLogs()
        {
            if (!IsLoggedIn) return RedirectToAction("Login");
            return View(await _db.EmailLogs.OrderByDescending(e => e.CreatedAt).Take(100).ToListAsync());
        }

        [HttpGet("submission/{id}/certificate")]
        public async Task<IActionResult> DownloadCertificate(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var submission = await _db.Submissions
                .Include(s => s.Authors)
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == "Published");

            if (submission == null) return NotFound();

            if (string.IsNullOrEmpty(submission.CertificateFilePath))
            {
                TempData["Error"] = "Certificate not uploaded yet.";
                return RedirectToAction("SubmissionDetails", new { id });
            }

            var fileBytes = _fileStorage.GetFileUrl(submission.CertificateFilePath);
            if (fileBytes == null)
            {
                TempData["Error"] = "Certificate file not found.";
                return RedirectToAction("SubmissionDetails", new { id });
            }

            return File(fileBytes, "application/pdf", $"Certificate_{submission.TrackingId}.pdf");
        }

        [HttpPost("submission/{id}/regenerate-certificate")]
        public IActionResult RegenerateCertificate(int id)
        {
            TempData["Info"] = "Please upload a new certificate manually in the submission edit screen.";
            return RedirectToAction("SubmissionDetails", new { id });
        }

        [HttpGet("reviewers/{id}/edit")]
        public async Task<IActionResult> EditReviewer(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var reviewer = await _db.Reviewers
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reviewer == null) return NotFound();

            return View(new CreateReviewerViewModel
            {
                FirstName = reviewer.User.FirstName,
                LastName = reviewer.User.LastName,
                Email = reviewer.User.Email,
                Phone = reviewer.User.Phone,
                Institution = reviewer.User.Institution,
                Specialization = reviewer.Specialization
            });
        }

        [HttpPost("reviewers/{id}/edit")]
        public async Task<IActionResult> EditReviewer(int id, CreateReviewerViewModel model)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var reviewer = await _db.Reviewers
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reviewer == null) return NotFound();

            if (reviewer.User.Email != model.Email)
            {
                var existingUser = await _db.Users.AnyAsync(u => u.Email == model.Email);
                if (existingUser)
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    return View(model);
                }
            }

            reviewer.User.FirstName = model.FirstName;
            reviewer.User.LastName = model.LastName;
            reviewer.User.Email = model.Email;
            reviewer.User.Phone = model.Phone;
            reviewer.User.Institution = model.Institution;
            reviewer.User.UpdatedAt = DateTime.UtcNow;
            reviewer.Specialization = model.Specialization;

            if (!string.IsNullOrEmpty(model.Password))
            {
                reviewer.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Reviewer updated successfully";
            return RedirectToAction("Reviewers");
        }

        [HttpPost("reviewers/{id}/delete")]
        public async Task<IActionResult> DeleteReviewer(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var reviewer = await _db.Reviewers
                .Include(r => r.User)
                .Include(r => r.Assignments)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reviewer == null) return NotFound();

            var hasPendingAssignments = reviewer.Assignments.Any(a => a.Status != "Completed");
            if (hasPendingAssignments)
            {
                TempData["Error"] = "Cannot delete reviewer with pending assignments. Complete or reassign them first.";
                return RedirectToAction("Reviewers");
            }

            try
            {
                if (reviewer.Assignments.Any())
                {
                    _db.ReviewAssignments.RemoveRange(reviewer.Assignments);
                }

                _db.Reviewers.Remove(reviewer);
                _db.Users.Remove(reviewer.User);

                await _db.SaveChangesAsync();

                TempData["Success"] = "Reviewer deleted successfully";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting reviewer: {ex.Message}";
            }

            return RedirectToAction("Reviewers");
        }

        [HttpPost("reviewers/{id}/toggle-status")]
        public async Task<IActionResult> ToggleReviewerStatus(int id)
        {
            if (!IsLoggedIn) return RedirectToAction("Login");

            var reviewer = await _db.Reviewers.FindAsync(id);
            if (reviewer == null) return NotFound();

            reviewer.IsAvailable = !reviewer.IsAvailable;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Reviewer status changed to {(reviewer.IsAvailable ? "Available" : "Unavailable")}";
            return RedirectToAction("Reviewers");
        }

        [HttpPost("submission/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubmission(int id)
        {
            var submission = await _db.Submissions
                .Include(s => s.Authors)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null)
            {
                TempData["Error"] = "Submission not found.";
                return RedirectToAction("Submissions");
            }

            if (submission.Status == "Published")
            {
                TempData["Error"] = "Cannot delete published submissions.";
                return RedirectToAction("Submissions");
            }

            try
            {
                if (!string.IsNullOrEmpty(submission.ManuscriptFilePath))
                {
                    await _fileStorage.DeleteFileAsync(submission.ManuscriptFilePath);
                }

                if (!string.IsNullOrEmpty(submission.PaymentReceiptFilePath))
                {
                    await _fileStorage.DeleteFileAsync(submission.PaymentReceiptFilePath);
                }

                if (!string.IsNullOrEmpty(submission.CertificateFilePath))
                {
                    await _fileStorage.DeleteFileAsync(submission.CertificateFilePath);
                }

                if (!string.IsNullOrEmpty(submission.PublishedPdfPath))
                {
                    await _fileStorage.DeleteFileAsync(submission.PublishedPdfPath);
                }

                var statusHistory = await _db.SubmissionStatusHistory
                    .Where(h => h.SubmissionId == id)
                    .ToListAsync();
                _db.SubmissionStatusHistory.RemoveRange(statusHistory);

                var reviewAssignments = await _db.ReviewAssignments
                    .Where(r => r.SubmissionId == id)
                    .ToListAsync();
                _db.ReviewAssignments.RemoveRange(reviewAssignments);

                var publishedVersions = await _db.PublishedVersions
                    .Where(p => p.SubmissionId == id)
                    .ToListAsync();
                _db.PublishedVersions.RemoveRange(publishedVersions);

                var emailLogs = await _db.EmailLogs
                    .Where(e => e.SubmissionId == id)
                    .ToListAsync();
                _db.EmailLogs.RemoveRange(emailLogs);

                var whatsAppLogs = await _db.WhatsAppLogs
                    .Where(w => w.SubmissionId == id)
                    .ToListAsync();
                _db.WhatsAppLogs.RemoveRange(whatsAppLogs);

                _db.Authors.RemoveRange(submission.Authors);
                _db.Submissions.Remove(submission);

                await _db.SaveChangesAsync();

                TempData["Success"] = $"Submission {submission.TrackingId} deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting submission: {ex.Message}";
            }

            return RedirectToAction("Submissions");
        }
    }
}

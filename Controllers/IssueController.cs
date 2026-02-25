using IJULR.Web.Data;
using IJULR.Web.Models;
using IJULR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IJULR.Web.Controllers
{
    public class IssueController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public IssueController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private async Task LoadViewBag()
        {
            ViewBag.Settings = await _db.SiteSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "");
        }

        public async Task<IActionResult> CurrentIssue()
        {
            var issue = await _db.Issues
                .Include(i => i.Volume)
                .FirstOrDefaultAsync(i => i.IsCurrentIssue);

            var papers = new List<Submission>();
            if (issue != null)
            {
                papers = await _db.Submissions
                    .Include(s => s.Authors.OrderBy(a => a.AuthorOrder))
                    .Where(s => s.IssueId == issue.Id && s.Status == "Published")
                    .OrderBy(s => s.PageStart)
                    .ToListAsync();
            }

            await LoadViewBag();
            return View(new CurrentIssueViewModel { Issue = issue, Papers = papers });
        }

        public async Task<IActionResult> Archives()
        {
            var volumes = await _db.Volumes
                .Where(v => !v.IsDeleted)  // Only non-deleted volumes
                .Include(v => v.Issues.Where(i => !i.IsDeleted))  // Only non-deleted issues
                .OrderByDescending(v => v.Year)
                .ToListAsync();

            var model = new ArchivesViewModel
            {
                Volumes = volumes.Select(v => new VolumeArchiveItem
                {
                    VolumeNumber = v.VolumeNumber,
                    Year = v.Year,
                    Issues = v.Issues.OrderBy(i => i.IssueNumber).Select(i => new IssueArchiveItem
                    {
                        Id = i.Id,
                        IssueNumber = i.IssueNumber,
                        Title = i.Title ?? "",
                        QuarterName = i.QuarterName ?? "",
                        PaperCount = _db.Submissions.Count(s => s.IssueId == i.Id && s.Status == "Published")
                    }).ToList()
                }).ToList()
            };

            await LoadViewBag();
            return View(model);
        }

        public async Task<IActionResult> ViewIssue(int id)
        {
            var issue = await _db.Issues.Include(i => i.Volume).FirstOrDefaultAsync(i => i.Id == id);
            if (issue == null) return NotFound();

            var papers = await _db.Submissions
                .Include(s => s.Authors.OrderBy(a => a.AuthorOrder))
                .Where(s => s.IssueId == id && s.Status == "Published")
                .OrderBy(s => s.PageStart)
                .ToListAsync();

            ViewBag.Issue = issue;
            await LoadViewBag();
            return View(papers);
        }

        public async Task<IActionResult> Paper(int id)
        {
            var paper = await _db.Submissions
                .Include(s => s.Authors.OrderBy(a => a.AuthorOrder))
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == "Published");

            if (paper == null) return NotFound();

            // Build citation
            var authors = string.Join(", ", paper.Authors.Select(a => a.FullName));
            var citation = $"{authors}, \"{paper.PaperTitle}\", International Journal of Unified Law Research (IJULR), Vol. {paper.Issue?.Volume?.VolumeNumber}, Issue {paper.Issue?.IssueNumber}, {paper.PublishedAt?.Year}, pp. {paper.PageRange}";

            if (!string.IsNullOrEmpty(paper.DOI))
                citation += $", DOI: {paper.DOI}";

            await LoadViewBag();
            return View(new PaperDetailsViewModel { Paper = paper, Citation = citation });
        }

        public async Task<IActionResult> Download(int id)
        {
            var paper = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id && s.Status == "Published");
            if (paper == null || string.IsNullOrEmpty(paper.ManuscriptFilePath)) return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, paper.ManuscriptFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = $"IJULR_{paper.TrackingId}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        public async Task<IActionResult> ExportCitation(int id, string format = "plain")
        {
            var paper = await _db.Submissions
                .Include(s => s.Authors.OrderBy(a => a.AuthorOrder))
                .Include(s => s.Issue).ThenInclude(i => i!.Volume)
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == "Published");

            if (paper == null) return NotFound();

            var authors = string.Join(", ", paper.Authors.Select(a => a.FullName));
            string citation;

            if (format == "bibtex")
            {
                citation = $@"@article{{{paper.TrackingId},
  author = {{{authors}}},
  title = {{{paper.PaperTitle}}},
  journal = {{International Journal of Unified Law Research}},
  year = {{{paper.PublishedAt?.Year}}},
  volume = {{{paper.Issue?.Volume?.VolumeNumber}}},
  number = {{{paper.Issue?.IssueNumber}}},
  pages = {{{paper.PageRange}}},
  doi = {{{paper.DOI}}}
}}";
            }
            else
            {
                citation = $"{authors}, \"{paper.PaperTitle}\", International Journal of Unified Law Research (IJULR), Vol. {paper.Issue?.Volume?.VolumeNumber}, Issue {paper.Issue?.IssueNumber}, {paper.PublishedAt?.Year}, pp. {paper.PageRange}";
                if (!string.IsNullOrEmpty(paper.DOI)) citation += $", DOI: {paper.DOI}";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(citation);
            var fileName = format == "bibtex" ? $"{paper.TrackingId}.bib" : $"{paper.TrackingId}_citation.txt";
            return File(bytes, "text/plain", fileName);
        }

        public async Task<IActionResult> Certificate(int id)
        {
            var paper = await _db.Submissions
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == "Published");

            if (paper == null || string.IsNullOrEmpty(paper.CertificateFilePath))
                return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, paper.CertificateFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, "application/pdf", $"Certificate_{paper.TrackingId}.pdf");
        }
    }
}

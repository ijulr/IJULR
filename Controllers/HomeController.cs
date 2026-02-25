using IJULR.Web.Data;
using IJULR.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IJULR.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db) => _db = db;

        private async Task LoadViewBag()
        {
            ViewBag.Settings = await _db.SiteSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "");
        }

        public async Task<IActionResult> Index()
        {
            await LoadViewBag();
            return View();
        }

        public async Task<IActionResult> About()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "about-journal" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> AimsScope()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "aims-scope" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> Objectives()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "objectives" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> ReviewProcess()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "review-process" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> PublicationProcess()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "publication-process" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> AuthorGuidelines()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "author-guidelines" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> ReviewerGuidelines()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "reviewer-guidelines" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> EditorGuidelines()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "editor-guidelines" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> FormattingStyle()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "formatting-style" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> PublicationEthics()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "publication-ethics" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> PublicationPolicy()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "publication-policy" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> PeerReviewPolicy()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "peer-review-policy" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> PlagiarismPolicy()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "plagiarism-policy" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> Licensing()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "licensing" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> OpenAccess()
        {
            var page = await _db.Pages.FirstOrDefaultAsync(p => p.Slug == "open-access" && p.IsPublished);
            await LoadViewBag();
            return View("Page", page);
        }

        public async Task<IActionResult> EditorialBoard()
        {
            var members = await _db.EditorialBoardMembers
                .Where(m => m.IsActive)
                .OrderBy(m => m.BoardCategory == "ChiefEditor" ? 0 : m.BoardCategory == "AssociateEditor" ? 1 : m.BoardCategory == "AdvisoryBoard" ? 2 : 3)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            await LoadViewBag();
            return View(members);
        }

        public async Task<IActionResult> Contact()
        {
            await LoadViewBag();
            return View();
        }

        public IActionResult Error() => View();
    }
}

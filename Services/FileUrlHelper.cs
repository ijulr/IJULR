using Microsoft.AspNetCore.Http;
using IJULR.Web.Services;

namespace IJULR.Web.Helpers
{
    /// <summary>
    /// Helper class to generate consistent file URLs for cloud-stored files
    /// </summary>
    public class FileUrlHelper
    {
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IFileStorageService _fileStorageService;

        public FileUrlHelper(IConfiguration config, IHttpContextAccessor httpContextAccessor, IFileStorageService fileStorageService)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            _fileStorageService = fileStorageService;
        }

        /// <summary>
        /// Get download URL for a file
        /// </summary>
        public string GetDownloadUrl(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return string.Empty;

            var baseUrl = GetBaseUrl();
            return $"{baseUrl}/api/file/download?key={Uri.EscapeDataString(fileKey)}";
        }

        /// <summary>
        /// Get view/embed URL for a file (opens in browser)
        /// </summary>
        public string GetViewUrl(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return string.Empty;

            var baseUrl = GetBaseUrl();
            return $"{baseUrl}/api/file/view?key={Uri.EscapeDataString(fileKey)}";
        }

        /// <summary>
        /// Get direct cloud URL (for embedding or redirecting to R2)
        /// </summary>
        public string GetDirectCloudUrl(string fileKey)
        {
            return _fileStorageService.GetFileUrl(fileKey);
        }

        /// <summary>
        /// Get preview URL for AJAX requests
        /// </summary>
        public string GetPreviewUrl(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return string.Empty;

            var baseUrl = GetBaseUrl();
            return $"{baseUrl}/api/file/preview?key={Uri.EscapeDataString(fileKey)}";
        }

        /// <summary>
        /// Get iframe embed code for PDFs
        /// </summary>
        public string GetPdfEmbedCode(string fileKey, string width = "100%", string height = "600px")
        {
            if (string.IsNullOrEmpty(fileKey))
                return string.Empty;

            var viewUrl = GetViewUrl(fileKey);
            return $@"<iframe src=""{viewUrl}"" width=""{width}"" height=""{height}"" frameborder=""0""></iframe>";
        }

        /// <summary>
        /// Get image embed code
        /// </summary>
        public string GetImageEmbedCode(string fileKey, string alt = "Image", string width = "100%", string height = "auto")
        {
            if (string.IsNullOrEmpty(fileKey))
                return string.Empty;

            var directUrl = GetDirectCloudUrl(fileKey);
            return $@"<img src=""{directUrl}"" alt=""{alt}"" style=""width: {width}; height: {height};"" />";
        }

        /// <summary>
        /// Get HTML link for file download
        /// </summary>
        public string GetDownloadLink(string fileKey, string displayText)
        {
            if (string.IsNullOrEmpty(fileKey))
                return string.Empty;

            var downloadUrl = GetDownloadUrl(fileKey);
            var fileName = Path.GetFileName(fileKey);
            return $@"<a href=""{downloadUrl}"" download=""{fileName}"">{displayText}</a>";
        }

        /// <summary>
        /// Get base URL of the application
        /// </summary>
        private string GetBaseUrl()
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            if (request != null)
            {
                return $"{request.Scheme}://{request.Host}";
            }

            // Fallback to config
            return _config["AppSettings:BaseUrl"] ?? "https://www.ijulr.com";
        }
    }
}
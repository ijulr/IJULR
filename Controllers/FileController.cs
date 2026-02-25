using Microsoft.AspNetCore.Mvc;
using IJULR.Web.Services;
using System;
using System.Threading.Tasks;

namespace IJULR.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileStorageService fileStorageService, ILogger<FileController> logger)
        {
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        /// <summary>
        /// Download file from cloud storage
        /// Usage: GET /api/file/download?key=submissions/guid_filename.pdf
        /// </summary>
        [HttpGet("download")]
        public async Task<IActionResult> Download([FromQuery] string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return BadRequest("File key is required");
            }

            try
            {
                var fileBytes = await _fileStorageService.GetFileAsync(key);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    return NotFound("File not found");
                }

                var fileName = Path.GetFileName(key);
                var contentType = GetContentType(fileName);

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {Key}", key);
                return StatusCode(500, "Error downloading file");
            }
        }

        /// <summary>
        /// View file in browser (inline)
        /// Usage: GET /api/file/view?key=submissions/guid_filename.pdf
        /// </summary>
        [HttpGet("view")]
        public async Task<IActionResult> View([FromQuery] string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return BadRequest("File key is required");
            }

            try
            {
                var fileStream = await _fileStorageService.GetFileStreamAsync(key);
                if (fileStream == null)
                {
                    return NotFound("File not found");
                }

                var fileName = Path.GetFileName(key);
                var contentType = GetContentType(fileName);

                // Use FileStreamResult to allow viewing in browser
                return new FileStreamResult(fileStream, contentType)
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing file: {Key}", key);
                return StatusCode(500, "Error viewing file");
            }
        }

        /// <summary>
        /// Get file as JSON response (for AJAX requests)
        /// Usage: GET /api/file/preview?key=submissions/guid_filename.pdf
        /// </summary>
        [HttpGet("preview")]
        public async Task<IActionResult> Preview([FromQuery] string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return BadRequest(new { error = "File key is required" });
            }

            try
            {
                var fileBytes = await _fileStorageService.GetFileAsync(key);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    return NotFound(new { error = "File not found" });
                }

                var fileName = Path.GetFileName(key);
                var base64 = Convert.ToBase64String(fileBytes);

                return Ok(new
                {
                    fileName = fileName,
                    size = fileBytes.Length,
                    base64Data = base64,
                    mimeType = GetContentType(fileName)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing file: {Key}", key);
                return StatusCode(500, new { error = "Error previewing file" });
            }
        }

        /// <summary>
        /// Get direct cloud URL for embedding
        /// Usage: GET /api/file/url?key=submissions/guid_filename.pdf
        /// </summary>
        [HttpGet("url")]
        public IActionResult GetUrl([FromQuery] string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return BadRequest(new { error = "File key is required" });
            }

            try
            {
                var url = _fileStorageService.GetFileUrl(key);
                return Ok(new { url = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file URL: {Key}", key);
                return StatusCode(500, new { error = "Error getting file URL" });
            }
        }

        /// <summary>
        /// Helper method to determine MIME type based on file extension
        /// </summary>
        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };
        }
    }
}
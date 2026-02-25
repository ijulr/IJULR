using IJULR.Web.Data;
using IJULR.Web.Models;
using Microsoft.EntityFrameworkCore;
using Amazon.S3;
using Amazon.S3.Transfer;
using RegionEndpoint = Amazon.RegionEndpoint;
using System.Net;
using System.Net.Mail;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IJULR.Web.Services
{
    // ==================== FILE STORAGE SERVICE ====================
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string folder);
        Task<string> SaveFileAsync(byte[] bytes, string fileName, string folder);
        Task DeleteFileAsync(string filePath);
        string GetFileUrl(string filePath);
        Task<byte[]> GetFileAsync(string filePath);
        Task<Stream> GetFileStreamAsync(string filePath);
    }

    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly string _webRoot;

        public LocalFileStorageService(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;

            _webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

            if (!Directory.Exists(_webRoot))
            {
                Directory.CreateDirectory(_webRoot);
            }
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsPath = Path.Combine(_webRoot, "uploads", folder);

            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{folder}/{fileName}";
        }

        public async Task<string> SaveFileAsync(byte[] bytes, string fileName, string folder)
        {
            if (bytes == null || bytes.Length == 0) return null;

            var uploadsPath = Path.Combine(_webRoot, "uploads", folder);

            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            await File.WriteAllBytesAsync(filePath, bytes);
            return $"/uploads/{folder}/{uniqueFileName}";
        }

        public Task DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return Task.CompletedTask;

            var relativePath = filePath.Replace("/", "\\").TrimStart('\\');
            var fullPath = Path.Combine(_webRoot, relativePath);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }

        public string GetFileUrl(string filePath) => filePath;

        public async Task<byte[]> GetFileAsync(string filePath)
        {
            var relativePath = filePath.Replace("/", "\\").TrimStart('\\');
            var fullPath = Path.Combine(_webRoot, relativePath);
            if (File.Exists(fullPath))
                return await File.ReadAllBytesAsync(fullPath);
            return null;
        }

        public async Task<Stream> GetFileStreamAsync(string filePath)
        {
            var relativePath = filePath.Replace("/", "\\").TrimStart('\\');
            var fullPath = Path.Combine(_webRoot, relativePath);
            if (File.Exists(fullPath))
                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return null;
        }
    }

    // Cloudflare R2 Storage (S3-Compatible)
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IConfiguration _config;
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _publicUrl;

        public S3FileStorageService(IConfiguration config)
        {
            _config = config;
            var r2Config = _config.GetSection("CloudStorageSettings:R2");

            _bucketName = r2Config["BucketName"] ?? "ijulr-files";
            var accountId = r2Config["AccountId"];

            var s3Config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                SignatureVersion = "4"
            };

            _s3Client = new AmazonS3Client(
                r2Config["AccessKey"],
                r2Config["SecretKey"],
                s3Config
            );

            _publicUrl = r2Config["PublicUrl"] ?? $"https://pub-{accountId}.r2.dev";
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            var key = $"{folder}/{Guid.NewGuid()}_{file.FileName}";

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var request = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = memoryStream,
                ContentType = file.ContentType,
                DisablePayloadSigning = true
            };

            await _s3Client.PutObjectAsync(request);
            return key;
        }

        public async Task<string> SaveFileAsync(byte[] bytes, string fileName, string folder)
        {
            if (bytes == null || bytes.Length == 0) return null;

            var key = $"{folder}/{Guid.NewGuid()}_{fileName}";

            using var stream = new MemoryStream(bytes);

            var request = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                DisablePayloadSigning = true
            };

            await _s3Client.PutObjectAsync(request);
            return key;
        }

        public async Task DeleteFileAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            var request = new Amazon.S3.Model.DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
        }

        public string GetFileUrl(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return $"{_publicUrl}/{key}";
        }

        public async Task<byte[]> GetFileAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                var request = new Amazon.S3.Model.GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                using var response = await _s3Client.GetObjectAsync(request);
                using var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Stream> GetFileStreamAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                var request = new Amazon.S3.Model.GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                var response = await _s3Client.GetObjectAsync(request);
                var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch
            {
                return null;
            }
        }
    }

    // ==================== EMAIL SERVICE ====================
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, int? submissionId = null);
        Task SendEmailWithAttachmentsAsync(string toEmail, string subject, string body, List<EmailAttachment>? attachments, int? submissionId = null);
        Task SendTemplateEmailAsync(string templateName, string toEmail, Dictionary<string, string> placeholders, int? submissionId = null);
        Task SendTemplateEmailWithAttachmentsAsync(string templateName, string toEmail, Dictionary<string, string> placeholders, List<EmailAttachment>? attachments, int? submissionId = null);
    }

    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ApplicationDbContext context, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _context = context;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, int? submissionId = null)
        {
            await SendEmailWithAttachmentsAsync(toEmail, subject, body, null, submissionId);
        }

        public async Task SendEmailWithAttachmentsAsync(string toEmail, string subject, string body, List<EmailAttachment>? attachments, int? submissionId = null)
        {
            var emailLog = new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                SubmissionId = submissionId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                // Check if email sending is enabled
                var enabled = _config.GetValue<bool>("EmailSettings:EnableEmailSending");
                if (!enabled)
                {
                    emailLog.Status = "Disabled";
                    emailLog.ErrorMessage = "Email sending is disabled in configuration";
                    _context.EmailLogs.Add(emailLog);
                    await _context.SaveChangesAsync();
                    _logger.LogWarning("Email sending disabled. Skipping email to {Email}", toEmail);
                    return;
                }

                // Validate email address
                if (!IsValidEmail(toEmail))
                {
                    emailLog.Status = "Failed";
                    emailLog.ErrorMessage = $"Invalid email address: {toEmail}";
                    _context.EmailLogs.Add(emailLog);
                    await _context.SaveChangesAsync();
                    _logger.LogWarning("Invalid email address: {Email}", toEmail);
                    return;
                }

                // Get configuration
                var smtpServer = _config["EmailSettings:SmtpServer"];
                var smtpPort = _config.GetValue<int>("EmailSettings:SmtpPort");
                var smtpUsername = _config["EmailSettings:SmtpUsername"];
                var smtpPassword = _config["EmailSettings:SmtpPassword"];
                var fromEmail = _config["EmailSettings:FromEmail"];
                var fromName = _config["EmailSettings:FromName"];

                // Validate configuration
                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    emailLog.Status = "Failed";
                    emailLog.ErrorMessage = "SMTP configuration is incomplete";
                    _context.EmailLogs.Add(emailLog);
                    await _context.SaveChangesAsync();
                    _logger.LogError("SMTP configuration is incomplete");
                    return;
                }

                // Create message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = body };

                // Add attachments
                if (attachments != null && attachments.Any())
                {
                    foreach (var attachment in attachments)
                    {
                        if (attachment?.Content != null && attachment.Content.Length > 0)
                        {
                            builder.Attachments.Add(
                                attachment.FileName ?? "attachment",
                                attachment.Content,
                                ContentType.Parse(attachment.MimeType ?? "application/octet-stream")
                            );
                        }
                    }
                }

                message.Body = builder.ToMessageBody();

                // Send email
                using var client = new MailKit.Net.Smtp.SmtpClient();

                // Determine secure socket options based on port
                SecureSocketOptions socketOptions = smtpPort switch
                {
                    465 => SecureSocketOptions.SslOnConnect,
                    587 => SecureSocketOptions.StartTls,
                    _ => SecureSocketOptions.Auto
                };

                _logger.LogInformation("Connecting to SMTP: {Server}:{Port} with {SocketOption}",
                    smtpServer, smtpPort, socketOptions);

                await client.ConnectAsync(smtpServer, smtpPort, socketOptions);

                _logger.LogInformation("Authenticating with username: {Username}", smtpUsername);

                await client.AuthenticateAsync(smtpUsername, smtpPassword);

                _logger.LogInformation("Sending email to: {Email}", toEmail);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                emailLog.Status = "Sent";
                emailLog.SentAt = DateTime.UtcNow;
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (OperationCanceledException ex)
            {
                emailLog.Status = "Failed";
                emailLog.ErrorMessage = $"Email sending timeout: {ex.Message}";
                _logger.LogError(ex, "Email sending timeout for {Email}", toEmail);
            }
            catch (AuthenticationException ex)
            {
                emailLog.Status = "Failed";
                emailLog.ErrorMessage = $"SMTP authentication failed: {ex.Message}";
                _logger.LogError(ex, "SMTP authentication failed for {Email}", toEmail);
            }
            catch (SmtpProtocolException ex)
            {
                emailLog.Status = "Failed";
                emailLog.ErrorMessage = $"SMTP protocol error: {ex.Message}";
                _logger.LogError(ex, "SMTP protocol error for {Email}", toEmail);
            }
            catch (Exception ex)
            {
                emailLog.Status = "Failed";
                emailLog.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to send email to {Email}. Error: {Error}", toEmail, ex.Message);
            }
            finally
            {
                _context.EmailLogs.Add(emailLog);
                await _context.SaveChangesAsync();
            }
        }

        public async Task SendTemplateEmailAsync(string templateName, string toEmail, Dictionary<string, string> placeholders, int? submissionId = null)
        {
            await SendTemplateEmailWithAttachmentsAsync(templateName, toEmail, placeholders, null, submissionId);
        }

        public async Task SendTemplateEmailWithAttachmentsAsync(string templateName, string toEmail, Dictionary<string, string> placeholders, List<EmailAttachment>? attachments, int? submissionId = null)
        {
            try
            {
                var template = await _context.EmailTemplates
                    .FirstOrDefaultAsync(t => t.TemplateName == templateName && t.IsActive);

                if (template == null)
                {
                    _logger.LogWarning("Email template {TemplateName} not found or not active", templateName);
                    return;
                }

                var subject = template.Subject;
                var body = template.Body;

                // Replace placeholders
                if (placeholders != null && placeholders.Any())
                {
                    foreach (var placeholder in placeholders)
                    {
                        var placeholder_key = $"{{{{{placeholder.Key}}}}}";
                        subject = subject.Replace(placeholder_key, placeholder.Value ?? "");
                        body = body.Replace(placeholder_key, placeholder.Value ?? "");
                    }
                }

                await SendEmailWithAttachmentsAsync(toEmail, subject, body, attachments, submissionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending template email {TemplateName} to {Email}", templateName, toEmail);
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    // Email Attachment Model
    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = "application/octet-stream";
    }

    // ==================== WHATSAPP SERVICE ====================
    public interface IWhatsAppService
    {
        Task SendMessageAsync(string toPhone, string message, int? submissionId = null);
        Task SendMessageWithMediaAsync(string toPhone, string message, byte[]? mediaBytes, string fileName, int? submissionId = null);
    }

    public class TwilioWhatsAppService : IWhatsAppService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TwilioWhatsAppService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public TwilioWhatsAppService(IConfiguration config, ApplicationDbContext context, ILogger<TwilioWhatsAppService> logger, IServiceProvider serviceProvider)
        {
            _config = config;
            _context = context;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task SendMessageAsync(string toPhone, string message, int? submissionId = null)
        {
            var log = new WhatsAppLog
            {
                ToPhone = toPhone,
                Message = message,
                SubmissionId = submissionId,
                Status = "Pending"
            };

            try
            {
                var enabled = _config.GetValue<bool>("WhatsAppSettings:EnableWhatsApp");
                if (!enabled)
                {
                    log.Status = "Disabled";
                    log.ErrorMessage = "WhatsApp is disabled in configuration";
                    _context.WhatsAppLogs.Add(log);
                    await _context.SaveChangesAsync();
                    return;
                }

                var accountSid = _config["WhatsAppSettings:AccountSid"];
                var authToken = _config["WhatsAppSettings:AuthToken"];
                var fromNumber = _config["WhatsAppSettings:FromNumber"];

                TwilioClient.Init(accountSid, authToken);

                var formattedTo = toPhone.StartsWith("whatsapp:") ? toPhone : $"whatsapp:{toPhone}";

                var messageResult = await MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(fromNumber),
                    to: new Twilio.Types.PhoneNumber(formattedTo)
                );

                if (!string.IsNullOrEmpty(messageResult.Sid))
                {
                    log.Status = "Sent";
                    log.SentAt = DateTime.UtcNow;
                    log.ErrorMessage = null;
                }
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to send WhatsApp to {Phone}", toPhone);
            }

            _context.WhatsAppLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task SendMessageWithMediaAsync(string toPhone, string message, byte[]? mediaBytes, string fileName, int? submissionId = null)
        {
            toPhone = toPhone.StartsWith("+91") ? toPhone : "+91" + toPhone.TrimStart('0').Replace("91", "").Trim();
            var log = new WhatsAppLog
            {
                ToPhone = toPhone,
                Message = message,
                SubmissionId = submissionId,
                Status = "Pending"
            };

            try
            {
                var enabled = _config.GetValue<bool>("WhatsAppSettings:EnableWhatsApp");
                if (!enabled)
                {
                    log.Status = "Disabled";
                    log.ErrorMessage = "WhatsApp is disabled in configuration";
                    _context.WhatsAppLogs.Add(log);
                    await _context.SaveChangesAsync();
                    return;
                }

                var accountSid = _config["WhatsAppSettings:AccountSid"];
                var authToken = _config["WhatsAppSettings:AuthToken"];
                var fromNumber = _config["WhatsAppSettings:FromNumber"];

                TwilioClient.Init(accountSid, authToken);

                MessageResource messageResult;

                if (mediaBytes != null && mediaBytes.Length > 0)
                {
                    var fileService = _serviceProvider.GetService<IFileStorageService>();
                    if (fileService != null)
                    {
                        var folder = "whatsapp-media";
                        var filePath = await fileService.SaveFileAsync(mediaBytes, fileName, folder);
                        var mediaUrl = fileService.GetFileUrl(filePath);

                        messageResult = await MessageResource.CreateAsync(
                            body: message,
                            from: new Twilio.Types.PhoneNumber(fromNumber),
                            to: new Twilio.Types.PhoneNumber($"whatsapp:{toPhone}"),
                            mediaUrl: new List<Uri> { new Uri(mediaUrl) }
                        );
                    }
                    else
                    {
                        messageResult = await MessageResource.CreateAsync(
                            body: message,
                            from: new Twilio.Types.PhoneNumber(fromNumber),
                            to: new Twilio.Types.PhoneNumber($"whatsapp:{toPhone}")
                        );
                    }
                }
                else
                {
                    messageResult = await MessageResource.CreateAsync(
                        body: message,
                        from: new Twilio.Types.PhoneNumber(fromNumber),
                        to: new Twilio.Types.PhoneNumber($"whatsapp:{toPhone}")
                    );
                }

                log.Status = "Sent";
                log.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to send WhatsApp to {Phone}", toPhone);
            }

            _context.WhatsAppLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }

    // ==================== NOTIFICATION SERVICE ====================
    public interface INotificationService
    {
        Task NotifySubmissionReceived(Submission submission);
        Task NotifyStatusChanged(Submission submission, string oldStatus, string newStatus, byte[]? qrBytes = null, byte[]? certificateBytes = null);
        Task NotifyCertificateReady(Submission submission, byte[]? certificatePdf = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly IEmailService _emailService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly IServiceProvider _serviceProvider;

        public NotificationService(IEmailService emailService, IWhatsAppService whatsAppService, IServiceProvider serviceProvider)
        {
            _emailService = emailService;
            _whatsAppService = whatsAppService;
            _serviceProvider = serviceProvider;
        }

        public async Task NotifySubmissionReceived(Submission submission)
        {
            var author = submission.PrimaryAuthor;
            if (author == null) return;

            var placeholders = new Dictionary<string, string>
            {
                { "AuthorName", author.FullName },
                { "PaperTitle", submission.PaperTitle },
                { "TrackingId", submission.TrackingId }
            };

            await _emailService.SendTemplateEmailAsync("SubmissionReceived", author.Email, placeholders, submission.Id);

            if (!string.IsNullOrEmpty(author.Phone))
            {
                var message = $"IJULR: Your paper \"{submission.PaperTitle}\" has been submitted. Tracking ID: {submission.TrackingId}";
                await _whatsAppService.SendMessageAsync(author.Phone, message, submission.Id);
            }
        }

        public async Task NotifyStatusChanged(Submission submission, string oldStatus, string newStatus, byte[]? qrBytes = null, byte[]? certificateBytes = null)
        {
            var author = submission.PrimaryAuthor;
            if (author == null) return;

            var templateName = newStatus switch
            {
                "UnderReview" => "PaperUnderReview",
                "Accepted" => "PaperAccepted",
                "Rejected" => "PaperRejected",
                "Revision" => "RevisionRequired",
                "PaymentPending" => "PaymentPending",
                "Published" => "PaperPublished",
                _ => null
            };

            var placeholders = new Dictionary<string, string>
            {
                { "AuthorName", author.FullName },
                { "PaperTitle", submission.PaperTitle },
                { "TrackingId", submission.TrackingId },
                { "RevisionComments", submission.RevisionComments ?? "" },
                { "ReviewerComments", submission.EditorRemarks ?? "" },
                { "Volume", submission.Issue?.Volume?.VolumeNumber.ToString() ?? "" },
                { "Issue", submission.Issue?.IssueNumber.ToString() ?? "" },
                { "DOI", submission.DOI ?? "" },
                { "PageRange", submission.PageRange ?? "" }
            };

            var attachments = new List<EmailAttachment>();

            if ((newStatus == "Accepted" || newStatus == "PaymentPending") && qrBytes != null && qrBytes.Length > 0)
            {
                attachments.Add(new EmailAttachment
                {
                    FileName = $"PaymentQR_{submission.TrackingId}.png",
                    Content = qrBytes,
                    MimeType = "image/png"
                });
            }

            if (newStatus == "Published" && certificateBytes != null && certificateBytes.Length > 0)
            {
                attachments.Add(new EmailAttachment
                {
                    FileName = $"Certificate_{submission.TrackingId}.pdf",
                    Content = certificateBytes,
                    MimeType = "application/pdf"
                });
            }

            if (templateName != null)
            {
                if (attachments.Any())
                {
                    await _emailService.SendTemplateEmailWithAttachmentsAsync(templateName, author.Email, placeholders, attachments, submission.Id);
                }
                else
                {
                    await _emailService.SendTemplateEmailAsync(templateName, author.Email, placeholders, submission.Id);
                }
            }

            if (!string.IsNullOrEmpty(author.Phone))
            {
                var message = newStatus switch
                {
                    "Accepted" => $"IJULR: Congratulations! Your paper \"{submission.PaperTitle}\" has been accepted. Please scan the QR code to complete payment. Tracking ID: {submission.TrackingId}",
                    "PaymentPending" => $"IJULR: Payment reminder for your paper \"{submission.PaperTitle}\". Please scan the QR code to complete payment. Tracking ID: {submission.TrackingId}",
                    "Published" => $"IJULR: Congratulations! Your paper \"{submission.PaperTitle}\" has been published! Your certificate is attached. Tracking ID: {submission.TrackingId}",
                    _ => $"IJULR: Your paper \"{submission.PaperTitle}\" status changed to {newStatus}. Tracking ID: {submission.TrackingId}"
                };

                if ((newStatus == "Accepted" || newStatus == "PaymentPending") && qrBytes != null)
                {
                    await _whatsAppService.SendMessageWithMediaAsync(author.Phone, message, qrBytes, "PaymentQR.png", submission.Id);
                }
                else if (newStatus == "Published" && certificateBytes != null)
                {
                    await _whatsAppService.SendMessageWithMediaAsync(author.Phone, message, certificateBytes, $"Certificate_{submission.TrackingId}.pdf", submission.Id);
                }
                else
                {
                    await _whatsAppService.SendMessageAsync(author.Phone, message, submission.Id);
                }
            }
        }

        public async Task NotifyCertificateReady(Submission submission, byte[]? certificatePdf = null)
        {
            var author = submission.PrimaryAuthor;
            if (author == null) return;

            var placeholders = new Dictionary<string, string>
            {
                { "AuthorName", author.FullName },
                { "PaperTitle", submission.PaperTitle },
                { "TrackingId", submission.TrackingId },
                { "Volume", submission.Issue?.Volume?.VolumeNumber.ToString() ?? "" },
                { "Issue", submission.Issue?.IssueNumber.ToString() ?? "" },
                { "DOI", submission.DOI ?? "" },
                { "PageRange", submission.PageRange }
            };

            if (certificatePdf != null && certificatePdf.Length > 0)
            {
                var attachments = new List<EmailAttachment>
                {
                    new EmailAttachment
                    {
                        FileName = $"Certificate_{submission.TrackingId}.pdf",
                        Content = certificatePdf,
                        MimeType = "application/pdf"
                    }
                };
                await _emailService.SendTemplateEmailWithAttachmentsAsync("CertificateReady", author.Email, placeholders, attachments, submission.Id);
            }
            else
            {
                await _emailService.SendTemplateEmailAsync("CertificateReady", author.Email, placeholders, submission.Id);
            }

            if (!string.IsNullOrEmpty(author.Phone))
            {
                var message = $"IJULR: Your paper \"{submission.PaperTitle}\" has been published! Certificate is ready. Tracking ID: {submission.TrackingId}";
                await _whatsAppService.SendMessageAsync(author.Phone, message, submission.Id);
            }
        }
    }
}
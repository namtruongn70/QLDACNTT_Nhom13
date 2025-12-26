using System;
using System.IO;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using QRCoder;
using Microsoft.Extensions.Configuration;

namespace CinemaManagement.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Gửi email xác nhận vé
        /// </summary>
        public async Task SendTicketEmailAsync(string toEmail, string subject, string htmlContent, string qrContent)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Cinema", _config["Gmail:Username"]));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            // Tạo QR code Base64
            string qrBase64 = GenerateQRCodeBase64(qrContent);

            // Chèn QR code vào nội dung HTML
            htmlContent += $"<div style='margin-top:20px;text-align:center;'><img src='data:image/png;base64,{qrBase64}' alt='QR Code' /></div>";

            message.Body = new TextPart("html") { Text = htmlContent };

            using var client = new SmtpClient();
            await client.ConnectAsync(_config["Gmail:SmtpServer"], int.Parse(_config["Gmail:Port"]), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_config["Gmail:Username"], _config["Gmail:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private string GenerateQRCodeBase64(string content)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);
            using var ms = new MemoryStream();
            using var bitmap = qrCode.GetGraphic(20);
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}

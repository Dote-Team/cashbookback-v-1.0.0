namespace cashbook.Models
{
    public class MailSettings
    {
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; }
    }
}

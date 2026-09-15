namespace cashbook.Dto.setting
{
    public class SettingDto
    {
        public bool CategoryStatus { get; set; }
        public bool PaymentMethodStatus { get; set; }
        public bool ContactStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

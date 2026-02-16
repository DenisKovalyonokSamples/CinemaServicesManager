using System.ComponentModel.DataAnnotations;

namespace CNM.Domain.Clients
{
    public class ImdbOptions
    {
        [Required]
        public string BaseUrl { get; set; }
        [Range(1, 120)]
        public int TimeoutSeconds { get; set; } = 10;
        [Range(0, 10)]
        public int RetryCount { get; set; } = 3;
    }
}
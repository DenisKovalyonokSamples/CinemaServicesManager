using System.ComponentModel.DataAnnotations;

namespace CNM.Gateway.API.Options
{
    public class GatewayOptions
    {
        [Required]
        [Url]
        public string ShowtimesEndpoint { get; set; }

        [Required]
        [Url]
        public string MoviesEndpoint { get; set; }
    }
}
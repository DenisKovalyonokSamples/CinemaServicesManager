using System.ComponentModel.DataAnnotations;

namespace CNM.Showtimes.API.Options
{
    public class AuthOptions
    {
        [Required]
        public string SchemeName { get; set; } = "CustomAuthentication";
    }
}
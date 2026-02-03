namespace CNM.Application.Services
{
    public class ImdbTitleResponse
    {
        public string title { get; set; } // Added: title from IMDB API
        public string id { get; set; } // Added: IMDB id
        public string stars { get; set; } // Added: stars field
        public string releaseDate { get; set; } // Added: release date field
    }
}

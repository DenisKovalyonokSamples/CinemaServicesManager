using System;

namespace CNM.Application.Services
{
    public class ImdbStatusSingleton
    {
        public bool Up { get; set; } // Added: indicates IMDB API availability
        public DateTime LastCall { get; set; } // Added: last time status was updated
    }
}

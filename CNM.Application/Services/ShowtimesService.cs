using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CNM.Domain.Database.Entities;
using CNM.Domain.Interfaces;

namespace CNM.Application.Services
{
    public class ShowtimesService
    {
        private readonly IShowtimesRepository _repository;
        private readonly IImdbClient _imdbClient;
        public ShowtimesService(IShowtimesRepository repository, IImdbClient imdbClient)
        {
            _repository = repository;
            _imdbClient = imdbClient;
        }

        public IEnumerable<ShowtimeEntity> GetShowtimes(DateTime? date, string title)
        {
            var titleTerm = title?.Trim();
            return _repository.GetCollection(showtime =>
                (!date.HasValue || (showtime.StartDate <= date.Value && showtime.EndDate >= date.Value)) &&
                (string.IsNullOrWhiteSpace(titleTerm) || (showtime.Movie != null && showtime.Movie.Title != null && showtime.Movie.Title.Contains(titleTerm, StringComparison.OrdinalIgnoreCase)))
            );
        }

        public async Task<ShowtimeEntity> CreateAsync(ShowtimeEntity payload, string imdbApiKey)
        {
            var imdbTitle = await _imdbClient.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
            payload.Movie.Title = imdbTitle?.title ?? payload.Movie.Title;
            payload.Movie.Stars = imdbTitle?.stars ?? payload.Movie.Stars;
            if (DateTime.TryParse(imdbTitle?.releaseDate, out var parsedReleaseDate)) payload.Movie.ReleaseDate = parsedReleaseDate;
            return _repository.Add(payload);
        }

        public async Task<ShowtimeEntity> UpdateAsync(ShowtimeEntity payload, string imdbApiKey)
        {
            if (payload?.Movie != null && !string.IsNullOrWhiteSpace(payload.Movie.ImdbId) && !string.IsNullOrWhiteSpace(imdbApiKey))
            {
                var imdbTitle = await _imdbClient.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
                payload.Movie.Title = imdbTitle?.title ?? payload.Movie.Title;
                payload.Movie.Stars = imdbTitle?.stars ?? payload.Movie.Stars;
                if (DateTime.TryParse(imdbTitle?.releaseDate, out var parsedReleaseDate)) payload.Movie.ReleaseDate = parsedReleaseDate;
            }
            return _repository.Update(payload);
        }

        public bool Delete(int id) => _repository.Delete(id) != null;
    }
}
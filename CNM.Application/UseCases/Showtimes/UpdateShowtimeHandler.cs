using System;
using System.Threading;
using System.Threading.Tasks;
using CNM.Domain.Database.Entities;
using CNM.Domain.Interfaces;
using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class UpdateShowtimeHandler : IRequestHandler<UpdateShowtimeCommand, ShowtimeEntity>
    {
        private readonly IShowtimesRepository _repository;
        private readonly IImdbClient _imdbClient;
        public UpdateShowtimeHandler(IShowtimesRepository repository, IImdbClient imdbClient)
        {
            _repository = repository;
            _imdbClient = imdbClient;
        }
        public async Task<ShowtimeEntity> Handle(UpdateShowtimeCommand request, CancellationToken cancellationToken)
        {
            var payload = request.Payload;
            if (payload?.Movie != null && !string.IsNullOrWhiteSpace(payload.Movie.ImdbId) && !string.IsNullOrWhiteSpace(request.ImdbApiKey))
            {
                var imdbTitle = await _imdbClient.GetByIdAsync(payload.Movie.ImdbId, request.ImdbApiKey);
                payload.Movie.Title = imdbTitle?.title ?? payload.Movie.Title;
                payload.Movie.Stars = imdbTitle?.stars ?? payload.Movie.Stars;
                if (DateTime.TryParse(imdbTitle?.releaseDate, out var parsedReleaseDate)) payload.Movie.ReleaseDate = parsedReleaseDate;
            }
            var result = _repository.Update(payload);
            return result;
        }
    }
}
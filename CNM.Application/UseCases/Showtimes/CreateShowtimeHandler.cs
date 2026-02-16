using System;
using System.Threading;
using System.Threading.Tasks;
using CNM.Domain.Database.Entities;
using CNM.Domain.Interfaces;
using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class CreateShowtimeHandler : IRequestHandler<CreateShowtimeCommand, ShowtimeEntity>
    {
        private readonly IShowtimesRepository _repository;
        private readonly IImdbClient _imdbClient;
        public CreateShowtimeHandler(IShowtimesRepository repository, IImdbClient imdbClient)
        {
            _repository = repository;
            _imdbClient = imdbClient;
        }
        public async Task<ShowtimeEntity> Handle(CreateShowtimeCommand request, CancellationToken cancellationToken)
        {
            var payload = request.Payload;
            var imdbTitle = await _imdbClient.GetByIdAsync(payload.Movie.ImdbId, request.ImdbApiKey);
            payload.Movie.Title = imdbTitle?.title ?? payload.Movie.Title;
            payload.Movie.Stars = imdbTitle?.stars ?? payload.Movie.Stars;
            if (DateTime.TryParse(imdbTitle?.releaseDate, out var parsedReleaseDate)) payload.Movie.ReleaseDate = parsedReleaseDate;
            return _repository.Add(payload);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CNM.Domain.Database.Entities;
using CNM.Domain.Interfaces;
using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class GetShowtimesHandler : IRequestHandler<GetShowtimesQuery, IEnumerable<ShowtimeEntity>>
    {
        private readonly IShowtimesRepository _repository;
        public GetShowtimesHandler(IShowtimesRepository repository)
        {
            _repository = repository;
        }
        public Task<IEnumerable<ShowtimeEntity>> Handle(GetShowtimesQuery request, CancellationToken cancellationToken)
        {
            var titleTerm = request.Title?.Trim();
            var result = _repository.GetCollection(showtime =>
                (!request.Date.HasValue || (showtime.StartDate <= request.Date.Value && showtime.EndDate >= request.Date.Value)) &&
                (string.IsNullOrWhiteSpace(titleTerm) || (showtime.Movie != null && showtime.Movie.Title != null && showtime.Movie.Title.Contains(titleTerm, StringComparison.OrdinalIgnoreCase)))
            );
            return Task.FromResult(result);
        }
    }
}
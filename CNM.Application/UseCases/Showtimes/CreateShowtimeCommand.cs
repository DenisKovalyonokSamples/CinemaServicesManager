using CNM.Domain.Database.Entities;
using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class CreateShowtimeCommand : IRequest<ShowtimeEntity>
    {
        public ShowtimeEntity Payload { get; set; }
        public string ImdbApiKey { get; set; }
    }
}
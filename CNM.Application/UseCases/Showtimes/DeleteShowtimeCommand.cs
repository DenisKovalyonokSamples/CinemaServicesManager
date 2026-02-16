using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class DeleteShowtimeCommand : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
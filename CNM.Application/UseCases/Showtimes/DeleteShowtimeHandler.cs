using System.Threading;
using System.Threading.Tasks;
using CNM.Domain.Interfaces;
using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class DeleteShowtimeHandler : IRequestHandler<DeleteShowtimeCommand, bool>
    {
        private readonly IShowtimesRepository _repository;
        public DeleteShowtimeHandler(IShowtimesRepository repository)
        {
            _repository = repository;
        }
        public Task<bool> Handle(DeleteShowtimeCommand request, CancellationToken cancellationToken)
        {
            var deleted = _repository.Delete(request.Id) != null;
            return Task.FromResult(deleted);
        }
    }
}
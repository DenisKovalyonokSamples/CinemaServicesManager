using CNM.Domain.Database; // keep this for DI registration
using CNM.Domain.Repositories;
using DomainDb = CNM.Domain.Interfaces;
using DomainEntities = CNM.Domain.Database.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using CNM.Domain.Interfaces;

namespace CNM.Showtimes.API.Controllers
{
    [ApiController]
    [Route("showtime")]
    public class ShowtimeController : ControllerBase
    {
        private readonly MediatR.IMediator _mediator;
        public ShowtimeController(MediatR.IMediator mediator)
        {
            _mediator = mediator;
        }

        // Compatibility constructor for existing tests
        public ShowtimeController(IShowtimesRepository repository, IImdbClient imdbClient)
        {
            MediatR.ServiceFactory factory = type =>
            {
                if (type == typeof(CNM.Application.UseCases.Showtimes.GetShowtimesHandler))
                    return new CNM.Application.UseCases.Showtimes.GetShowtimesHandler(repository);
                if (type == typeof(CNM.Application.UseCases.Showtimes.CreateShowtimeHandler))
                    return new CNM.Application.UseCases.Showtimes.CreateShowtimeHandler(repository, imdbClient);
                if (type == typeof(CNM.Application.UseCases.Showtimes.UpdateShowtimeHandler))
                    return new CNM.Application.UseCases.Showtimes.UpdateShowtimeHandler(repository, imdbClient);
                if (type == typeof(CNM.Application.UseCases.Showtimes.DeleteShowtimeHandler))
                    return new CNM.Application.UseCases.Showtimes.DeleteShowtimeHandler(repository);
                return null;
            };
            _mediator = new MediatR.Mediator(factory);
        }

        [HttpGet]
        [Authorize(Policy = "Read")]
        public IActionResult Get([FromQuery] DateTime? date, [FromQuery] string title)
        {
            var showtimes = _mediator.Send(new CNM.Application.UseCases.Showtimes.GetShowtimesQuery { Date = date, Title = title }).GetAwaiter().GetResult();
            return Ok(showtimes.ToList());
        }

        [HttpPost]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Post([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload?.Movie == null || string.IsNullOrWhiteSpace(payload.Movie.ImdbId))
                return BadRequest("movie.imdb_id required");
            if (string.IsNullOrWhiteSpace(imdbApiKey))
                return BadRequest("imdb_api_key required");

            var createdShowtime = await _mediator.Send(new CNM.Application.UseCases.Showtimes.CreateShowtimeCommand { Payload = payload, ImdbApiKey = imdbApiKey });
            return Created($"/showtime/{createdShowtime.Id}", createdShowtime);
        }

        [HttpPut]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Put([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            var updatedShowtime = await _mediator.Send(new CNM.Application.UseCases.Showtimes.UpdateShowtimeCommand { Payload = payload, ImdbApiKey = imdbApiKey });
            if (updatedShowtime == null) return NotFound();
            return Ok(updatedShowtime);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "Write")]
        public IActionResult Delete(int id)
        {
            var deleted = _mediator.Send(new CNM.Application.UseCases.Showtimes.DeleteShowtimeCommand { Id = id }).GetAwaiter().GetResult();
            if (!deleted) return NotFound();
            return NoContent();
        }

        [HttpPatch]
        public IActionResult Patch()
        {
            throw new Exception("Test error");
        }
    }
}

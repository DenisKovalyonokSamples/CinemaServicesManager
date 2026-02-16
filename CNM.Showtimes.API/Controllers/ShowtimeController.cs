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
        private readonly CNM.Application.Services.ShowtimesService _showtimesService;
        public ShowtimeController(DomainDb.IShowtimesRepository showtimesRepository, IImdbClient imdbClient)
        {
            _showtimesService = new CNM.Application.Services.ShowtimesService(showtimesRepository, imdbClient);
        }

        [HttpGet]
        [Authorize(Policy = "Read")]
        public IActionResult Get([FromQuery] DateTime? date, [FromQuery] string title)
        {
            var showtimes = _showtimesService.GetShowtimes(date, title);
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

            var createdShowtime = await _showtimesService.CreateAsync(payload, imdbApiKey);
            return Created($"/showtime/{createdShowtime.Id}", createdShowtime);
        }

        [HttpPut]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Put([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            var updatedShowtime = await _showtimesService.UpdateAsync(payload, imdbApiKey);
            if (updatedShowtime == null) return NotFound();
            return Ok(updatedShowtime);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "Write")]
        public IActionResult Delete(int id)
        {
            var deleted = _showtimesService.Delete(id);
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

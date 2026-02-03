using CNM.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CNM.Application.Controllers
{
    [ApiController]
    [Route("status")]
    public class StatusController : ControllerBase
    {
        private readonly ImdbStatusSingleton _status;
        public StatusController(ImdbStatusSingleton status) // Added: inject singleton status
        {
            _status = status;
        }

        [HttpGet]
        public IActionResult Get() => Ok(new { up = _status.Up, last_call = _status.LastCall }); // Added: returns IMDB status
    }
}

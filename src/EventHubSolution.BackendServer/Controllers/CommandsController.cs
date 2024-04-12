using EventHubSolution.BackendServer.Data;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/commands")]
    [ApiController]
    public class CommandsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CommandsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCommands()
        {
            var commands = _context.Commands;

            var commandVms = await commands.Select(f => new CommandVm()
            {
                Id = f.Id,
                Name = f.Name,
            }).ToListAsync();

            return Ok(commandVms);
        }

    }
}

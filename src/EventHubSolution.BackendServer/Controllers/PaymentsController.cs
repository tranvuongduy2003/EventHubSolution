using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/tickets")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public TicketsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostTicket([FromBody] TicketCreateRequest request)
        {
            var dbTicket = await _db.Tickets.FindAsync(request.Id);
            if (dbTicket != null)
                return BadRequest(new ApiBadRequestResponse($"Ticket with id {request.Id} is existed."));

            var ticket = new Ticket()
            {
                Id = Guid.NewGuid().ToString(),
            };
            _db.Tickets.Add(ticket);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetTickets([FromQuery] PaginationFilter filter)
        {
            var tickets = _db.Tickets.ToList();
            if (filter.search != null)
            {
                tickets = tickets.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            tickets = filter.order switch
            {
                PageOrder.ASC => tickets.OrderBy(c => c.SortOrder).ToList(),
                PageOrder.DESC => tickets.OrderByDescending(c => c.SortOrder).ToList(),
                _ => tickets
            };

            var metadata = new Metadata(tickets.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                tickets = tickets.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var ticketVms = tickets.Select(f => new TicketVm()
            {
            }).ToList();

            var pagination = new Pagination<TicketVm>
            {
                Items = ticketVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new ApiNotFoundResponse(""));

            var ticketVm = new TicketVm()
            {
            };
            return Ok(ticketVm);
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutTicket(string id, [FromBody] TicketCreateRequest request)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new ApiNotFoundResponse(""));

            _db.Tickets.Update(ticket);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteTicket(string id)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new ApiNotFoundResponse(""));

            _db.Tickets.Remove(ticket);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                var ticketvm = new TicketVm()
                {
                };
                return Ok(ticketvm);
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}

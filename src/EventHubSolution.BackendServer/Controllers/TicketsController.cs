using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
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
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostTicket([FromBody] TicketCreateRequest request)
        {
            var ticket = new Ticket()
            {
                Id = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                CustomerEmail = request.CustomerEmail,
                Status = request.Status,
                PaymentId = request.PaymentId,
                TicketTypeId = request.TicketTypeId
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
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.VIEW)]
        public async Task<IActionResult> GetTickets([FromQuery] PaginationFilter filter)
        {
            var tickets = _db.Tickets.ToList();


            if (!filter.search.IsNullOrEmpty())
            {
                tickets = tickets.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
                                             c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
                                             c.CustomerPhone.ToLower().Contains(filter.search.ToLower())
                ).ToList();
            }

            tickets = filter.order switch
            {
                PageOrder.ASC => tickets.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => tickets.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => tickets
            };

            var metadata = new Metadata(tickets.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                tickets = tickets.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var ticketVms = tickets.Select(t => new TicketVm()
            {
                Id = t.Id,
                EventId = t.EventId,
                CustomerName = t.CustomerName,
                CustomerPhone = t.CustomerPhone,
                CustomerEmail = t.CustomerEmail,
                Status = t.Status,
                PaymentId = t.PaymentId,
                TicketTypeId = t.TicketTypeId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            var pagination = new Pagination<TicketVm>
            {
                Items = ticketVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new ApiNotFoundResponse(""));

            var ticketVm = new TicketVm()
            {
                Id = ticket.Id,
                EventId = ticket.EventId,
                CustomerName = ticket.CustomerName,
                CustomerPhone = ticket.CustomerPhone,
                CustomerEmail = ticket.CustomerEmail,
                Status = ticket.Status,
                PaymentId = ticket.PaymentId,
                TicketTypeId = ticket.TicketTypeId,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt
            };
            return Ok(new ApiOkResponse(ticketVm));
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutTicket(string id, [FromBody] TicketCreateRequest request)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new ApiNotFoundResponse(""));

            ticket.EventId = request.EventId;
            ticket.CustomerName = request.CustomerName;
            ticket.CustomerPhone = request.CustomerPhone;
            ticket.CustomerEmail = request.CustomerEmail;
            ticket.Status = request.Status;
            ticket.PaymentId = request.PaymentId;
            ticket.TicketTypeId = request.TicketTypeId;

            _db.Tickets.Update(ticket);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }

            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.DELETE)]
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
                    Id = ticket.Id,
                    EventId = ticket.EventId,
                    CustomerName = ticket.CustomerName,
                    CustomerPhone = ticket.CustomerPhone,
                    CustomerEmail = ticket.CustomerEmail,
                    Status = ticket.Status,
                    PaymentId = ticket.PaymentId,
                    TicketTypeId = ticket.TicketTypeId,
                    CreatedAt = ticket.CreatedAt,
                    UpdatedAt = ticket.UpdatedAt
                };
                return Ok(new ApiOkResponse(ticketvm));
            }

            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}
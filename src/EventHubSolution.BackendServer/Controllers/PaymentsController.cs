using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PaymentsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostPayment([FromBody] PaymentCreateRequest request)
        {
            var payment = new Payment()
            {
                Id = Guid.NewGuid().ToString(),
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Discount = request.Discount,
                Status = request.Status,
                EventId = request.EventId,
                PaymentMethod = request.PaymentMethod,
                PaymentSession = request.PaymentSession,
                TicketQuantity = request.TicketQuantity,
                TotalPrice = request.TotalPrice,
                UserId = request.UserId,
                PaymentIntentId = request.PaymentIntentId
            };
            _db.Payments.Add(payment);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { id = payment.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.VIEW)]
        public async Task<IActionResult> GetPayments([FromQuery] PaginationFilter filter)
        {
            var payments = _db.Payments.ToList();

            var metadata = new Metadata(payments.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.search != null)
            {
                payments = payments.Where(c => c.CustomerName.ToLower().Contains(filter.search.ToLower()) ||
                                               c.CustomerEmail.ToLower().Contains(filter.search.ToLower()) ||
                                               c.CustomerPhone.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            payments = filter.order switch
            {
                PageOrder.ASC => payments.OrderBy(c => c.CreatedAt).ToList(),
                PageOrder.DESC => payments.OrderByDescending(c => c.CreatedAt).ToList(),
                _ => payments
            };

            if (filter.takeAll == false)
            {
                payments = payments.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var paymentVms = payments.Select(p => new PaymentVm()
            {
                Id = p.Id,
                CustomerName = p.CustomerName,
                CustomerEmail = p.CustomerEmail,
                CustomerPhone = p.CustomerPhone,
                Discount = p.Discount,
                Status = p.Status,
                EventId = p.EventId,
                PaymentMethod = p.PaymentMethod,
                PaymentSession = p.PaymentSession,
                TicketQuantity = p.TicketQuantity,
                TotalPrice = p.TotalPrice,
                UserId = p.UserId,
                PaymentIntentId = p.PaymentIntentId,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            var pagination = new Pagination<PaymentVm>
            {
                Items = paymentVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var payment = await _db.Payments.FindAsync(id);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse(""));

            var paymentVm = new PaymentVm()
            {
                Id = payment.Id,
                CustomerName = payment.CustomerName,
                CustomerEmail = payment.CustomerEmail,
                CustomerPhone = payment.CustomerPhone,
                Discount = payment.Discount,
                Status = payment.Status,
                EventId = payment.EventId,
                PaymentMethod = payment.PaymentMethod,
                PaymentSession = payment.PaymentSession,
                TicketQuantity = payment.TicketQuantity,
                TotalPrice = payment.TotalPrice,
                UserId = payment.UserId,
                PaymentIntentId = payment.PaymentIntentId,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt
            };
            return Ok(new ApiOkResponse(paymentVm));
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutPayment(string id, [FromBody] PaymentCreateRequest request)
        {
            var payment = await _db.Payments.FindAsync(id);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse(""));

            payment.CustomerName = request.CustomerName;
            payment.CustomerEmail = request.CustomerEmail;
            payment.CustomerPhone = request.CustomerPhone;
            payment.Discount = request.Discount;
            payment.Status = request.Status;
            payment.EventId = request.EventId;
            payment.PaymentMethod = request.PaymentMethod;
            payment.PaymentSession = request.PaymentSession;
            payment.TicketQuantity = request.TicketQuantity;
            payment.TotalPrice = request.TotalPrice;
            payment.UserId = request.UserId;
            payment.PaymentIntentId = request.PaymentIntentId;

            _db.Payments.Update(payment);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.DELETE)]
        public async Task<IActionResult> DeletePayment(string id)
        {
            var payment = await _db.Payments.FindAsync(id);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse(""));

            _db.Payments.Remove(payment);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                var paymentvm = new PaymentVm()
                {
                    Id = payment.Id,
                    CustomerName = payment.CustomerName,
                    CustomerEmail = payment.CustomerEmail,
                    CustomerPhone = payment.CustomerPhone,
                    Discount = payment.Discount,
                    Status = payment.Status,
                    EventId = payment.EventId,
                    PaymentMethod = payment.PaymentMethod,
                    PaymentSession = payment.PaymentSession,
                    TicketQuantity = payment.TicketQuantity,
                    TotalPrice = payment.TotalPrice,
                    UserId = payment.UserId,
                    PaymentIntentId = payment.PaymentIntentId
                };
                return Ok(new ApiOkResponse(paymentvm));
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}

using Bogus;
using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using EventHubSolution.ViewModels.General;
using EventHubSolution.ViewModels.Stripe;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly StripeService _stripeService;

        public PaymentsController(ApplicationDbContext db, UserManager<User> userManager, StripeService stripeService)
        {
            _db = db;
            _userManager = userManager;
            _stripeService = stripeService;
        }

        [HttpPost("checkout")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostCheckout([FromBody] CheckoutRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new ApiNotFoundResponse($"User with id {request.UserId} does not exist!"));

            var eventData = await _db.Events.FindAsync(request.EventId);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse($"Event with id {request.EventId} does not exist!"));

            var payment = new Payment()
            {
                Id = Guid.NewGuid().ToString(),
                CustomerName = request.FullName,
                CustomerEmail = request.Email,
                CustomerPhone = request.PhoneNumber,
                Discount = eventData.Promotion ?? 0.0,
                Status = PaymentStatus.PENDING,
                EventId = request.EventId,
                PaymentMethod = ViewModels.Constants.PaymentMethod.BANKING,
                UserId = request.UserId,
            };

            int ticketQuantity = 0;
            long totalPrice = 0;

            foreach (var item in request.Items)
            {
                var ticketType = await _db.TicketTypes.FirstOrDefaultAsync(t => t.EventId.Equals(eventData.Id) && t.Id.Equals(item.TicketTypeId));
                if (ticketType == null)
                    return NotFound(new ApiNotFoundResponse($"Event with id {request.EventId} does not exist in event with id {eventData.Id}!"));
                if (ticketType.Quantity < item.Quantity)
                    return BadRequest(new ApiBadRequestResponse($"Ticket type {ticketType.Name} does not have enough quantity!"));

                ticketQuantity += item.Quantity;
                totalPrice += (long)(item.Quantity * ticketType.Price * eventData.Promotion);

                ticketType.NumberOfSoldTickets += item.Quantity;
                _db.TicketTypes.Update(ticketType);

                var paymentItem = new PaymentItem
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = request.EventId,
                    Quantity = item.Quantity,
                    TicketTypeId = ticketType.Id,
                    UserId = request.UserId,
                    PaymentId = payment.Id,
                    TotalPrice = ticketType.Price * item.Quantity,
                    Name = ticketType.Name,
                    Discount = eventData.Promotion ?? 0.0,
                };
                await _db.PaymentItems.AddAsync(paymentItem);
            }

            payment.TicketQuantity = ticketQuantity;
            payment.TotalPrice = totalPrice;

            await _db.Payments.AddAsync(payment);
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

        [HttpPatch("{paymentId}/accept")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PatchAcceptPayment(string paymentId)
        {
            var payment = await _db.Payments.FindAsync(paymentId);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse($"Payment with id {paymentId} does not exist!"));

            payment.Status = PaymentStatus.PAID;
            _db.Payments.Update(payment);

            var paymentItems = await _db.PaymentItems.Where(pi => pi.PaymentId.Equals(paymentId)).ToListAsync();
            foreach (var paymentItem in paymentItems)
            {
                var ticketType = await _db.TicketTypes.FindAsync(paymentItem.TicketTypeId);
                var maxTicketNumberLength = ticketType.Quantity.ToString().Length;
                var stringifiedQuantity = ticketType.NumberOfSoldTickets.ToString();
                var ticketNumber = stringifiedQuantity.PadLeft(maxTicketNumberLength - stringifiedQuantity.Length, '0');

                for (int i = 0; i < paymentItem.Quantity; i++)
                {
                    var ticket = new Ticket
                    {
                        Id = Guid.NewGuid().ToString(),
                        CustomerName = payment.CustomerName,
                        CustomerEmail = payment.CustomerEmail,
                        CustomerPhone = payment.CustomerPhone,
                        EventId = payment.EventId,
                        PaymentId = paymentId,
                        TicketTypeId = paymentItem.TicketTypeId,
                        TicketNo = $"#{ticketNumber}",
                    };
                    await _db.Tickets.AddAsync(ticket);
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new ApiOkResponse("Payment accepted!"));
        }

        [HttpPatch("{paymentId}/reject")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PatchRejectPayment(string paymentId)
        {
            var payment = await _db.Payments.FindAsync(paymentId);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse($"Payment with id {paymentId} does not exist!"));

            payment.Status = PaymentStatus.REJECTED;
            _db.Payments.Update(payment);

            var paymentItems = await _db.PaymentItems.Where(pi => pi.PaymentId.Equals(paymentId)).ToListAsync();
            foreach (var paymentItem in paymentItems)
            {
                var ticketType = await _db.TicketTypes.FindAsync(paymentItem.TicketTypeId);
                ticketType.NumberOfSoldTickets -= paymentItem.Quantity;
                _db.TicketTypes.Update(ticketType);
            }

            await _db.SaveChangesAsync();

            return Ok(new ApiOkResponse("Payment rejected!"));
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
                PaymentSessionId = request.PaymentSessionId,
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
        public async Task<IActionResult> GetPayments([FromQuery] PaymentPaginationFilter filter)
        {
            var payments = _db.Payments.ToList();


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

            payments = payments.Where(p => p.Status.Equals(filter.status)).ToList();

            var metadata = new Metadata(payments.Count(), filter.page, filter.size, filter.takeAll);

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
                PaymentMethod = (PaymentMethod)p.PaymentMethod,
                PaymentSessionId = p.PaymentSessionId,
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
                PaymentMethod = (PaymentMethod)payment.PaymentMethod,
                PaymentSessionId = payment.PaymentSessionId,
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
            payment.PaymentSessionId = request.PaymentSessionId;
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
                    PaymentMethod = (PaymentMethod)payment.PaymentMethod,
                    PaymentSessionId = payment.PaymentSessionId,
                    TicketQuantity = payment.TicketQuantity,
                    TotalPrice = payment.TotalPrice,
                    UserId = payment.UserId,
                    PaymentIntentId = payment.PaymentIntentId
                };
                return Ok(new ApiOkResponse(paymentvm));
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpPost("create-stripe-session")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostCreateStripeSession([FromBody] CreatePaymentSessionRequest request)
        {
            var payment = await _db.Payments.FindAsync(request.PaymentId);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse($"Payment with id {request.PaymentId} does not exist!"));

            var paymentItems = (from _paymentItem in _db.PaymentItems.ToList()
                                join _event in _db.Events.ToList()
                                on _paymentItem.EventId equals _event.Id
                                where _paymentItem.PaymentId == payment.Id
                                select new PaymentItemVm
                                {
                                    Id = _paymentItem.Id,
                                    Discount = _paymentItem.Discount,
                                    EventId = _event.Id,
                                    EventName = _event.Name,
                                    Quantity = _paymentItem.Quantity,
                                    TotalPrice = _paymentItem.TotalPrice,
                                    PaymentId = _paymentItem.PaymentId,
                                    TicketTypeId = _paymentItem.TicketTypeId,
                                    TicketTypeName = _paymentItem.Name,
                                    UserId = _paymentItem.UserId
                                }).ToList();

            var sessionRequest = new CreateStripeSessionRequest
            {
                ApprovedUrl = request.ApprovedUrl,
                CancelUrl = request.CancelUrl,
                Items = paymentItems,
            };

            var session = await _stripeService.CreateStripeSession(sessionRequest);

            request.StripeSessionUrl = session.Url;

            payment.PaymentSessionId = session.Id;
            _db.Payments.Update(payment);

            await _db.SaveChangesAsync();

            return Ok(new ApiOkResponse(request));
        }

        [HttpPost("{paymentId}/validate-stripe-session")]
        [ClaimRequirement(FunctionCode.CONTENT_PAYMENT, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostValidateStripeSession(string paymentId)
        {
            var payment = await _db.Payments.FindAsync(paymentId);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse($"Payment with id {paymentId} does not exist!"));

            var session = await _stripeService.GetSession(payment.PaymentSessionId);
            var paymentIntent = await _stripeService.GetPaymentIntent(session.PaymentIntentId);

            if (paymentIntent.Status == "succeeded")
            {
                // Then payment was successful
                payment.PaymentIntentId = paymentIntent.Id;
                payment.Status = PaymentStatus.PAID;

                _db.Payments.Update(payment);

                // TODO: Create tickets
                var paymentItems = (from _paymentItem in _db.PaymentItems.ToList()
                                    join _event in _db.Events.ToList()
                                    on _paymentItem.EventId equals _event.Id
                                    where _paymentItem.PaymentId == payment.Id
                                    select new PaymentItemVm
                                    {
                                        Id = _paymentItem.Id,
                                        Discount = _paymentItem.Discount,
                                        EventId = _event.Id,
                                        EventName = _event.Name,
                                        Quantity = _paymentItem.Quantity,
                                        TotalPrice = _paymentItem.TotalPrice,
                                        PaymentId = _paymentItem.PaymentId,
                                        TicketTypeId = _paymentItem.TicketTypeId,
                                        TicketTypeName = _paymentItem.Name,
                                        UserId = _paymentItem.UserId
                                    }).ToList();

                List<Task> tasks = new List<Task>();
                foreach (var item in paymentItems)
                {
                    tasks.Add(Task.Factory.StartNew(async () =>
                    {
                        var ticketGenerator = new Faker<Ticket>()
                        .RuleFor(e => e.Id, _ => Guid.NewGuid().ToString())
                        .RuleFor(e => e.CustomerEmail, _ => payment.CustomerEmail)
                        .RuleFor(e => e.CustomerName, _ => payment.CustomerName)
                        .RuleFor(e => e.CustomerPhone, _ => payment.CustomerPhone)
                        .RuleFor(e => e.EventId, _ => payment.EventId)
                        .RuleFor(e => e.PaymentId, _ => payment.Id)
                        .RuleFor(e => e.TicketTypeId, _ => item.TicketTypeId)
                        .RuleFor(e => e.UserId, _ => item.UserId)
                        .RuleFor(e => e.TicketNo, f => f.Hashids.Encode(
                            DateTime.Now.Hour,
                            DateTime.Now.Minute,
                            DateTime.Now.Second,
                            DateTime.Now.Millisecond,
                            DateTime.Now.Microsecond,
                            DateTime.Now.Day,
                            DateTime.Now.Month,
                            DateTime.Now.Year));
                        var tickets = ticketGenerator.Generate(item.Quantity);
                        await _db.Tickets.AddRangeAsync(tickets);
                    }));
                }
                Task.WaitAll(tasks.ToArray());
            }
            else
            {
                payment.Status = PaymentStatus.REJECTED;
            }

            return Ok(new ApiOkResponse());
        }

        #region Tickets
        [HttpGet("{paymentId}/tickets")]
        [ClaimRequirement(FunctionCode.CONTENT_TICKET, CommandCode.VIEW)]
        public async Task<IActionResult> GetTicketsByPaymentId(string paymentId, [FromQuery] PaginationFilter filter)
        {
            var payment = await _db.Payments.FindAsync(paymentId);
            if (payment == null)
                return NotFound(new ApiNotFoundResponse($"Payment with id {paymentId} does not exist!"));

            var tickets = _db.Tickets.Where(t => t.PaymentId == paymentId).ToList();

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
        #endregion
    }
}

using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    public class EventsController : BaseController
    {
        private readonly ApplicationDbContext _db;

        public EventsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostEvent([FromBody] EventCreateRequest request)
        {
            var dbEvent = await _db.Events.FindAsync(request.Id);
            if (dbEvent != null)
                return BadRequest(new ApiBadRequestResponse($"Event with id {request.Id} is existed."));

            var eventData = new Event()
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Url = request.Url,
                SortOrder = request.SortOrder,
                ParentId = request.ParentId,
            };
            _db.Events.Add(eventData);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { id = eventData.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetEvents([FromQuery] PaginationFilter filter)
        {
            var eventDatas = _db.Events.ToList();
            if (filter.search != null)
            {
                eventDatas = eventDatas.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            eventDatas = filter.order switch
            {
                PageOrder.ASC => eventDatas.OrderBy(c => c.SortOrder).ToList(),
                PageOrder.DESC => eventDatas.OrderByDescending(c => c.SortOrder).ToList(),
                _ => eventDatas
            };

            var metadata = new Metadata(eventDatas.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                eventDatas = eventDatas.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var eventDataVms = eventDatas.Select(f => new EventVm()
            {
                Id = f.Id,
                Name = f.Name,
                Url = f.Url,
                SortOrder = f.SortOrder,
                ParentId = f.ParentId,
            }).ToList();

            var pagination = new Pagination<EventVm>
            {
                Items = eventDataVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse(""));

            var eventDataVm = new EventVm()
            {
                Id = eventData.Id,
                Name = eventData.Name,
                Url = eventData.Url,
                SortOrder = eventData.SortOrder,
                ParentId = eventData.ParentId,
            };
            return Ok(eventDataVm);
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutEvent(string id, [FromBody] EventCreateRequest request)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse(""));

            eventData.Name = eventData.Name;
            eventData.Url = eventData.Url;
            eventData.SortOrder = eventData.SortOrder;
            eventData.ParentId = eventData.ParentId;

            _db.Events.Update(eventData);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteEvent(string id)
        {
            var eventData = await _db.Events.FindAsync(id);
            if (eventData == null)
                return NotFound(new ApiNotFoundResponse(""));

            _db.Events.Remove(eventData);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                var eventDatavm = new EventVm()
                {
                    Id = eventData.Id,
                    Name = eventData.Name,
                    Url = eventData.Url,
                    SortOrder = eventData.SortOrder,
                    ParentId = eventData.ParentId,
                };
                return Ok(eventDatavm);
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
    }
}

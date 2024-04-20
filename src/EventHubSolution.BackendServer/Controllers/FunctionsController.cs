using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.General;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/functions")]
    [ApiController]
    public class FunctionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public FunctionsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostFunction([FromBody] FunctionCreateRequest request)
        {
            var dbFunction = await _db.Functions.FindAsync(request.Id);
            if (dbFunction != null)
                return BadRequest(new ApiBadRequestResponse($"Function with id {request.Id} is existed."));

            var function = new Function()
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Url = request.Url,
                SortOrder = request.SortOrder,
                ParentId = request.ParentId,
            };
            _db.Functions.Add(function);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { id = function.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetFunctions([FromQuery] PaginationFilter filter)
        {
            var functions = _db.Functions.ToList();
            if (filter.search != null)
            {
                functions = functions.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            functions = filter.order switch
            {
                PageOrder.ASC => functions.OrderBy(c => c.SortOrder).ToList(),
                PageOrder.DESC => functions.OrderByDescending(c => c.SortOrder).ToList(),
                _ => functions
            };

            var metadata = new Metadata(functions.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                functions = functions.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var functionVms = functions.Select(f => new FunctionVm()
            {
                Id = f.Id,
                Name = f.Name,
                Url = f.Url,
                SortOrder = f.SortOrder,
                ParentId = f.ParentId,
            }).ToList();

            var pagination = new Pagination<FunctionVm>
            {
                Items = functionVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(new ApiOkResponse(pagination));
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var function = await _db.Functions.FindAsync(id);
            if (function == null)
                return NotFound(new ApiNotFoundResponse(""));

            var functionVm = new FunctionVm()
            {
                Id = function.Id,
                Name = function.Name,
                Url = function.Url,
                SortOrder = function.SortOrder,
                ParentId = function.ParentId,
            };
            return Ok(new ApiOkResponse(functionVm));
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutFunction(string id, [FromBody] FunctionCreateRequest request)
        {
            var function = await _db.Functions.FindAsync(id);
            if (function == null)
                return NotFound(new ApiNotFoundResponse(""));

            function.Name = function.Name;
            function.Url = function.Url;
            function.SortOrder = function.SortOrder;
            function.ParentId = function.ParentId;

            _db.Functions.Update(function);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteFunction(string id)
        {
            var function = await _db.Functions.FindAsync(id);
            if (function == null)
                return NotFound(new ApiNotFoundResponse(""));

            _db.Functions.Remove(function);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                var functionvm = new FunctionVm()
                {
                    Id = function.Id,
                    Name = function.Name,
                    Url = function.Url,
                    SortOrder = function.SortOrder,
                    ParentId = function.ParentId,
                };
                return Ok(new ApiOkResponse(functionvm));
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }

        [HttpGet("{functionId}/commands")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetCommandsInFunction(string functionId)
        {
            var query = from c in _db.Commands
                        join fc in _db.CommandInFunctions
                        on c.Id equals fc.FunctionId into result1
                        from commandInFunction in result1.DefaultIfEmpty()
                        join f in _db.Functions
                        on commandInFunction.FunctionId equals f.Id into result2
                        from function in result2.DefaultIfEmpty()
                        select new
                        {
                            Id = c.Id,
                            Name = c.Name,
                            commandInFunction.FunctionId,
                        };
            query = query.Where(x => x.FunctionId == functionId);

            var commandVms = await query.Select(x => new CommandVm
            {
                Id = x.Id,
                Name = x.Name,
            }).ToListAsync();

            return Ok(new ApiOkResponse(commandVms));
        }

        [HttpGet("{functionId}/commands/not-in-function")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.VIEW)]
        public async Task<IActionResult> GetCommandsNotInFunction(string functionId)
        {
            var query = from c in _db.Commands
                        join fc in _db.CommandInFunctions
                        on c.Id equals fc.FunctionId into result1
                        from commandInFunction in result1.DefaultIfEmpty()
                        join f in _db.Functions
                        on commandInFunction.FunctionId equals f.Id into result2
                        from function in result2.DefaultIfEmpty()
                        select new
                        {
                            Id = c.Id,
                            Name = c.Name,
                            commandInFunction.FunctionId,
                        };
            query = query.Where(x => x.FunctionId != functionId).Distinct();

            var commandVms = await query.Select(x => new CommandVm
            {
                Id = x.Id,
                Name = x.Name,
            }).ToListAsync();

            return Ok(new ApiOkResponse(commandVms));
        }

        [HttpPost("{functionId}/commands")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostCommandToFunction(string functionId, [FromBody] AddCommandToFunctionRequest request)
        {
            var commandInFunction = await _db.CommandInFunctions.FindAsync(request.CommandId, request.FunctionId);
            if (commandInFunction != null)
                return BadRequest(new ApiBadRequestResponse("This command has been added to function."));

            var entity = new CommandInFunction()
            {
                CommandId = request.CommandId,
                FunctionId = request.FunctionId,
            };
            _db.CommandInFunctions.Add(entity);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return CreatedAtAction(nameof(GetById), new { commandId = request.CommandId, functionId = request.FunctionId }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }

        [HttpDelete("{functionId}/commands/{commandId}")]
        [ClaimRequirement(FunctionCode.SYSTEM_FUNCTION, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteCommand(string functionId, string commandId)
        {
            var commandInFunction = await _db.CommandInFunctions.FindAsync(commandId, functionId);
            if (commandInFunction == null)
                return BadRequest(new ApiBadRequestResponse("This command is not existed in function."));

            var entity = new CommandInFunction()
            {
                CommandId = commandId,
                FunctionId = functionId,
            };
            _db.CommandInFunctions.Remove(entity);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                return Ok(new ApiOkResponse(entity));
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(""));
            }
        }
    }
}

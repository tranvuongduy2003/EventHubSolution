using Dapper;
using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/permissions")]
    [ApiController]
    public class PermissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public PermissionsController(ApplicationDbContext db, IConfiguration configuration, RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _configuration = configuration;
            _roleManager = roleManager;
        }


        /// <summary>
        /// Show list function with corressponding action included in each functions
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_PERMISSION, CommandCode.VIEW)]
        public async Task<IActionResult> GetCommandViews()
        {
            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                if (conn.State == ConnectionState.Closed)
                {
                    await conn.OpenAsync();
                }

                var sql = @"SELECT f.Id,
	                       f.Name,
	                       f.ParentId,
	                       sum(case when sa.Id = 'CREATE' then 1 else 0 end) as HasCreate,
	                       sum(case when sa.Id = 'UPDATE' then 1 else 0 end) as HasUpdate,
	                       sum(case when sa.Id = 'DELETE' then 1 else 0 end) as HasDelete,
	                       sum(case when sa.Id = 'VIEW' then 1 else 0 end) as HasView,
	                       sum(case when sa.Id = 'APPROVE' then 1 else 0 end) as HasApprove
                        from Functions f join CommandInFunctions cif on f.Id = cif.FunctionId
		                    left join Commands sa on cif.CommandId = sa.Id
                        GROUP BY f.Id,f.Name, f.ParentId
                        order BY f.ParentId";

                var result = await conn.QueryAsync<PermissionScreenVm>(sql, null, null, 120, CommandType.Text);
                return Ok(new ApiOkResponse(result.ToList()));
            }
        }

        [HttpGet("roles")]
        [ClaimRequirement(FunctionCode.SYSTEM_PERMISSION, CommandCode.VIEW)]
        public async Task<IActionResult> GetCommandByRoles()
        {
            var functionWithRoles = (from function in _db.Functions.ToList()
                                     join permission in _db.Permissions.ToList()
                                      on function.Id equals permission.FunctionId
                                     select new
                                     {
                                         FunctionId = function.Id,
                                         FunctionName = function.Name,
                                         RoleId = permission.RoleId
                                     });
            var rolePermissionVms = (from role in _roleManager.Roles.ToList()
                                     join functionWithRole in functionWithRoles
                                     on role.Id equals functionWithRole.RoleId
                                     into joinedRolePermissions
                                     from _joinedPermission in joinedRolePermissions.DefaultIfEmpty()
                                     select new
                                     {
                                         RoleId = role.Id,
                                         RoleName = role.Name,
                                         FunctionId = _joinedPermission != null && _joinedPermission.FunctionId != null ? _joinedPermission.FunctionId : null,
                                         FunctionName = _joinedPermission != null && _joinedPermission.FunctionName != null ? _joinedPermission.FunctionName : null,
                                     })
                                     .GroupBy(rolePermission => rolePermission.RoleId)
                                     .Select(groupedRolePermission => new RolePermissionVm
                                     {
                                         RoleId = groupedRolePermission.Key,
                                         RoleName = groupedRolePermission.FirstOrDefault().RoleName,
                                         FunctionIds = groupedRolePermission.Select(r => r.FunctionId).Distinct().ToList(),
                                         FunctionNames = groupedRolePermission.Select(r => r.FunctionName).Distinct().ToList(),
                                     })
                                     .ToList();

            return Ok(new ApiOkResponse(rolePermissionVms));
        }

        [HttpPut]
        [ClaimRequirement(FunctionCode.SYSTEM_PERMISSION, CommandCode.UPDATE)]
        public async Task<IActionResult> PutPermissionByCommand([FromBody] UpdatePermissionByCommandRequest request)
        {
            var function = await _db.Functions.FindAsync(request.FunctionId);
            if (function == null)
                return NotFound(new ApiNotFoundResponse("Funtion does not exist!"));

            var command = await _db.Commands.FindAsync(request.CommandId);
            if (command == null)
                return NotFound(new ApiNotFoundResponse("Command does not exist!"));

            if (request.Value == true)
            {
                var commandInFunction = await _db.CommandInFunctions.FirstOrDefaultAsync(cif => cif.FunctionId == request.FunctionId && cif.CommandId == request.CommandId);

                if (commandInFunction != null)
                    return BadRequest(new ApiBadRequestResponse("Permission already exists!"));

                commandInFunction = new CommandInFunction
                {
                    CommandId = request.CommandId,
                    FunctionId = request.FunctionId,
                };

                await _db.CommandInFunctions.AddAsync(commandInFunction);
                await _db.SaveChangesAsync();
            }
            else
            {
                var commandInFunction = await _db.CommandInFunctions.FirstOrDefaultAsync(cif => cif.FunctionId == request.FunctionId && cif.CommandId == request.CommandId);

                if (commandInFunction == null)
                    return BadRequest(new ApiBadRequestResponse("Permission does not exist!"));

                _db.CommandInFunctions.Remove(commandInFunction);
                await _db.SaveChangesAsync();
            }

            return await GetCommandViews();
        }

        [HttpPut("roles")]
        [ClaimRequirement(FunctionCode.SYSTEM_PERMISSION, CommandCode.UPDATE)]
        public async Task<IActionResult> PutPermissionByRole([FromBody] UpdatePermissionByRoleRequest request)
        {
            var function = await _db.Functions.FindAsync(request.FunctionId);
            if (function == null)
                return NotFound(new ApiNotFoundResponse("Funtion does not exist!"));

            var role = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
            if (role == null)
                return NotFound(new ApiNotFoundResponse("Role does not exist!"));

            if (request.Value == true)
            {
                var permissions = await _db.Permissions.Where(p => p.FunctionId == request.FunctionId && p.RoleId == request.RoleId).ToListAsync();

                if (permissions.Count > 0)
                    return BadRequest(new ApiBadRequestResponse("Permission already exists!"));
                permissions = await _db.CommandInFunctions.Where(cif => cif.FunctionId == request.FunctionId).Select(cif => new Permission(cif.FunctionId, request.RoleId, cif.CommandId)).ToListAsync();

                await _db.Permissions.AddRangeAsync(permissions);
                await _db.SaveChangesAsync();
            }
            else
            {
                var permissions = await _db.Permissions.Where(p => p.FunctionId == request.FunctionId && p.RoleId == request.RoleId).ToListAsync();

                if (permissions.Count <= 0)
                    return BadRequest(new ApiBadRequestResponse("Permission does not exist!"));

                _db.Permissions.RemoveRange(permissions);
                await _db.SaveChangesAsync();
            }

            return await GetCommandByRoles();
        }
    }
}

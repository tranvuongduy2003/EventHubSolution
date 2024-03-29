﻿using EventHubSolution.BackendServer.Authorization;
using EventHubSolution.BackendServer.Constants;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Helpers;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Systems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EventHubSolution.BackendServer.Controllers
{
    public class RolesController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;

        public RolesController(RoleManager<IdentityRole> roleManager, ApplicationDbContext db)
        {
            _roleManager = roleManager;
            _db = db;
        }

        #region Roles
        [HttpPost]
        [ClaimRequirement(FunctionCode.SYSTEM_ROLE, CommandCode.CREATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PostRole(RoleCreateRequest request)
        {
            var role = new IdentityRole()
            {
                Id = request.Id,
                Name = request.Name,
                NormalizedName = request.Name.ToUpper()
            };
            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                return CreatedAtAction(nameof(GetById), new { id = role.Id }, request);
            }
            else
            {
                return BadRequest(new ApiBadRequestResponse(result));
            }
        }

        [HttpGet]
        [ClaimRequirement(FunctionCode.SYSTEM_ROLE, CommandCode.VIEW)]
        public async Task<IActionResult> GetRoles([FromQuery] PaginationFilter filter)
        {
            var role = _roleManager.Roles.ToList();
            if (filter.search != null)
            {
                role = role.Where(c => c.Name.ToLower().Contains(filter.search.ToLower())).ToList();
            }

            role = filter.order switch
            {
                PageOrder.ASC => role.OrderBy(c => c.Name).ToList(),
                PageOrder.DESC => role.OrderByDescending(c => c.Name).ToList(),
                _ => role
            };

            var metadata = new Metadata(role.Count(), filter.page, filter.size, filter.takeAll);

            if (filter.takeAll == false)
            {
                role = role.Skip((filter.page - 1) * filter.size)
                    .Take(filter.size).ToList();
            }

            var roleVms = role.Select(r => new RoleVm()
            {
                Id = r.Id,
                Name = r.Name
            }).ToList();

            var pagination = new Pagination<RoleVm>
            {
                Items = roleVms,
                Metadata = metadata,
            };

            Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(pagination);
        }

        [HttpGet("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_ROLE, CommandCode.VIEW)]
        public async Task<IActionResult> GetById(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound(new ApiNotFoundResponse(""));

            var roleVm = new RoleVm()
            {
                Id = role.Id,
                Name = role.Name,
            };
            return Ok(roleVm);
        }

        [HttpPut("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_ROLE, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutRole(string id, [FromBody] RoleCreateRequest request)
        {
            if (id != request.Id)
                return BadRequest(new ApiBadRequestResponse(""));

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound(new ApiNotFoundResponse(""));

            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpper();

            var result = await _roleManager.UpdateAsync(role);

            if (result.Succeeded)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(result));
        }

        [HttpDelete("{id}")]
        [ClaimRequirement(FunctionCode.SYSTEM_ROLE, CommandCode.DELETE)]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound(new ApiNotFoundResponse(""));

            var result = await _roleManager.DeleteAsync(role);

            if (result.Succeeded)
            {
                var rolevm = new RoleVm()
                {
                    Id = role.Id,
                    Name = role.Name
                };
                return Ok(rolevm);
            }
            return BadRequest(new ApiBadRequestResponse(result));
        }
        #endregion

        #region Permissions
        [HttpGet("{roleId}/permissions")]
        [ClaimRequirement(FunctionCode.SYSTEM_PERMISSION, CommandCode.VIEW)]
        public async Task<IActionResult> GetPermissionByRoleId(string roleId)
        {
            var permissions = from p in _db.Permissions

                              join a in _db.Commands
                              on p.CommandId equals a.Id
                              where p.RoleId == roleId
                              select new PermissionVm()
                              {
                                  FunctionId = p.FunctionId,
                                  CommandId = p.CommandId,
                                  RoleId = p.RoleId
                              };

            return Ok(await permissions.ToListAsync());
        }

        [HttpPut("{roleId}/permissions")]
        [ClaimRequirement(FunctionCode.SYSTEM_PERMISSION, CommandCode.UPDATE)]
        [ApiValidationFilter]
        public async Task<IActionResult> PutPermissionByRoleId(string roleId, [FromBody] UpdatePermissionRequest request)
        {
            //create new permission list from user changed
            var newPermissions = new List<Permission>();
            foreach (var p in request.Permissions)
            {
                newPermissions.Add(new Permission(p.FunctionId, roleId, p.CommandId));
            }

            var existingPermissions = _db.Permissions.Where(x => x.RoleId == roleId);
            _db.Permissions.RemoveRange(existingPermissions);
            _db.Permissions.AddRange(newPermissions);
            var result = await _db.SaveChangesAsync();
            if (result > 0)
            {
                return NoContent();
            }
            return BadRequest(new ApiBadRequestResponse(""));
        }
        #endregion
    }
}

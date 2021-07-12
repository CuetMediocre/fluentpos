using AutoMapper;
using FluentPOS.Modules.Identity.Core.Abstractions;
using FluentPOS.Modules.Identity.Core.Entities;
using FluentPOS.Modules.Identity.Core.Exceptions;
using FluentPOS.Modules.Identity.Core.Helpers;
using FluentPOS.Shared.Core.Constants;
using FluentPOS.Shared.Core.Interfaces.Services.Identity;
using FluentPOS.Shared.Core.Wrapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentPOS.Shared.DTOs.Identity.Roles;
using FluentPOS.Shared.Infrastructure.Utilities;
using static FluentPOS.Shared.Core.Constants.Permissions;

namespace FluentPOS.Modules.Identity.Infrastructure.Services
{
    class RoleService : IRoleService
    {
        private readonly RoleManager<FluentRole> _roleManager;
        private readonly UserManager<FluentUser> _userManager;
        private readonly IRoleClaimService _roleClaimService;
        private readonly IStringLocalizer<RoleService> _localizer;
        private readonly ICurrentUser _currentUserService;
        private readonly IMapper _mapper;

        public RoleService(
            RoleManager<FluentRole> roleManager,
            IMapper mapper,
            UserManager<FluentUser> userManager,
            IRoleClaimService roleClaimService,
            IStringLocalizer<RoleService> localizer,
            ICurrentUser currentUserService)
        {
            _roleManager = roleManager;
            _mapper = mapper;
            _userManager = userManager;
            _roleClaimService = roleClaimService;
            _localizer = localizer;
            _currentUserService = currentUserService;
        }
        private static List<string> DefaultRoles()
        {
            return typeof(RoleConstants).GetAllPublicConstantValues<string>();
        }
        public async Task<Result<string>> DeleteAsync(string id)
        {
            var existingRole = await _roleManager.FindByIdAsync(id);
            if (existingRole == null) throw new IdentityException("Role Not Found", statusCode: System.Net.HttpStatusCode.NotFound);
            if (DefaultRoles().Contains(existingRole.Name))
            {
                return await Result<string>.SuccessAsync(string.Format(_localizer["Not allowed to delete {0} Role."], existingRole.Name));
            }
            bool roleIsNotUsed = true;
            var allUsers = await _userManager.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                if (await _userManager.IsInRoleAsync(user, existingRole.Name))
                {
                    roleIsNotUsed = false;
                }
            }
            if (roleIsNotUsed)
            {
                await _roleManager.DeleteAsync(existingRole);
                return await Result<string>.SuccessAsync(string.Format(_localizer["Role {0} Deleted."], existingRole.Name));
            }
            else
            {
                return await Result<string>.SuccessAsync(string.Format(_localizer["Not allowed to delete {0} Role as it is being used."], existingRole.Name));
            }
        }

        public async Task<Result<List<RoleResponse>>> GetAllAsync()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var rolesResponse = _mapper.Map<List<RoleResponse>>(roles);
            return await Result<List<RoleResponse>>.SuccessAsync(rolesResponse);
        }

        public async Task<Result<PermissionResponse>> GetAllPermissionsAsync(string roleId)
        {
            var model = new PermissionResponse();
            var allPermissions = GetAllPermissions();
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role != null)
            {
                model.RoleId = role.Id;
                model.RoleName = role.Name;
                var allRoleClaims = await _roleClaimService.GetAllAsync();
                var roleClaimsResult = await _roleClaimService.GetAllByRoleIdAsync(role.Id);
                if (roleClaimsResult.Succeeded)
                {
                    var roleClaims = roleClaimsResult.Data;
                    var allClaimValues = allPermissions.Select(a => a.Value).ToList();
                    var roleClaimValues = roleClaims.Select(a => a.Value).ToList();
                    var authorizedClaims = allClaimValues.Intersect(roleClaimValues).ToList();
                    foreach (var permission in allPermissions)
                    {
                        permission.Id = allRoleClaims.Data?.SingleOrDefault(x => x.RoleId == roleId && x.Type == permission.Type && x.Value == permission.Value)?.Id ?? 0;
                        permission.RoleId = roleId;
                        if (authorizedClaims.Any(a => a == permission.Value))
                        {
                            permission.Selected = true;
                            var roleClaim = roleClaims.SingleOrDefault(a => a.Type == permission.Type && a.Value == permission.Value);
                            if (roleClaim?.Description != null)
                            {
                                permission.Description = roleClaim.Description;
                            }
                            if (roleClaim?.Group != null)
                            {
                                permission.Group = roleClaim.Group;
                            }
                        }
                    }
                }
                else
                {
                    model.RoleClaims = new List<RoleClaimResponse>();
                    return await Result<PermissionResponse>.FailAsync(roleClaimsResult.Messages);
                }
            }
            model.RoleClaims = allPermissions;
            return await Result<PermissionResponse>.SuccessAsync(model);
        }

        private List<RoleClaimResponse> GetAllPermissions()
        {
            var allPermissions = new List<RoleClaimResponse>();

            #region GetPermissions

            allPermissions.GetAllPermissions();

            #endregion GetPermissions

            return allPermissions;
        }

        public async Task<Result<RoleResponse>> GetByIdAsync(string id)
        {
            var roles = await _roleManager.Roles.SingleOrDefaultAsync(x => x.Id == id);
            var rolesResponse = _mapper.Map<RoleResponse>(roles);
            return await Result<RoleResponse>.SuccessAsync(rolesResponse);
        }

        public async Task<Result<string>> SaveAsync(RoleRequest request)
        {
            if (string.IsNullOrEmpty(request.Id))
            {
                var existingRole = await _roleManager.FindByNameAsync(request.Name);
                if (existingRole != null) throw new IdentityException(_localizer["Similar Role already exists."], statusCode: System.Net.HttpStatusCode.BadRequest);
                var response = await _roleManager.CreateAsync(new FluentRole(request.Name,request.Description));
                if (response.Succeeded)
                {
                    return await Result<string>.SuccessAsync(string.Format(_localizer["Role {0} Created."], request.Name));
                }
                else
                {
                    return await Result<string>.FailAsync(response.Errors.Select(e => _localizer[e.Description].ToString()).ToList());
                }
            }
            else
            {
                var existingRole = await _roleManager.FindByIdAsync(request.Id);
                if (existingRole == null) return await Result<string>.FailAsync(_localizer["Role does not exist."]);
                if (DefaultRoles().Contains(existingRole.Name))
                {
                    return await Result<string>.SuccessAsync(string.Format(_localizer["Not allowed to modify {0} Role."], existingRole.Name));
                }
                existingRole.Name = request.Name;
                existingRole.NormalizedName = request.Name.ToUpper();
                existingRole.Description = request.Description;
                await _roleManager.UpdateAsync(existingRole);
                return await Result<string>.SuccessAsync(string.Format(_localizer["Role {0} Updated."], existingRole.Name));
            }
        }

        public async Task<Result<string>> UpdatePermissionsAsync(PermissionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RoleId))
                {
                    return await Result<string>.FailAsync(_localizer["Role is required."]);
                }

                var errors = new List<string>();
                var role = await _roleManager.FindByIdAsync(request.RoleId);
                if (role.Name == RoleConstants.SuperAdmin)
                {
                    var currentUser = await _userManager.Users.SingleAsync(x => x.Id == _currentUserService.GetUserId().ToString());
                    if (!await _userManager.IsInRoleAsync(currentUser, RoleConstants.SuperAdmin))
                    {
                        return await Result<string>.FailAsync(_localizer["Not allowed to modify Permissions for this Role."]);
                    }
                }

                var selectedClaims = request.RoleClaims.Where(a => a.Selected).ToList();
                if (role.Name == RoleConstants.SuperAdmin)
                {
                    if (selectedClaims.All(x => x.Value != Roles.View) || 
                        selectedClaims.All(x => x.Value != RoleClaims.View) || 
                        selectedClaims.All(x => x.Value != RoleClaims.Edit))
                    {
                        return await Result<string>.FailAsync(string.Format(
                            _localizer["Not allowed to deselect {0} or {1} or {2} for this Role."],
                            Roles.View, RoleClaims.View, RoleClaims.Edit));
                    }
                }

                var claims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in claims)
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }
                foreach (var claim in selectedClaims)
                {
                    var addResult = await _roleManager.AddPermissionClaim(role, claim.Value);
                    if (!addResult.Succeeded)
                    {
                        errors.AddRange(addResult.Errors.Select(e => _localizer[e.Description].ToString()));
                    }
                }

                var addedClaims = await _roleClaimService.GetAllByRoleIdAsync(role.Id);
                if (addedClaims.Succeeded)
                {
                    foreach (var claim in selectedClaims)
                    {
                        var addedClaim = addedClaims.Data.SingleOrDefault(x => x.Type == claim.Type && x.Value == claim.Value);
                        if (addedClaim != null)
                        {
                            claim.Id = addedClaim.Id;
                            claim.RoleId = addedClaim.RoleId;
                            var saveResult = await _roleClaimService.SaveAsync(claim);
                            if (!saveResult.Succeeded)
                            {
                                errors.AddRange(saveResult.Messages);
                            }
                        }
                    }
                }
                else
                {
                    errors.AddRange(addedClaims.Messages);
                }

                if (errors.Any())
                {
                    return await Result<string>.FailAsync(errors);
                }

                return await Result<string>.SuccessAsync(_localizer["Permissions Updated."]);
            }
            catch (Exception ex)
            {
                return await Result<string>.FailAsync(ex.Message);
            }
        }

        public async Task<int> GetCountAsync()
        {
            var count = await _roleManager.Roles.CountAsync();
            return count;
        }
    }
}
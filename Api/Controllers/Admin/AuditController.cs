using Api.Authentication;
using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/audit"), Authorize(Policy = TouredAuthorizationPolicies.CliImport)]
public sealed class AuditController(AdminUserManager manager) : ControllerBase
{
    [HttpGet]
    public Task<List<AdminAuditEntryDto>> GetAuditEntries(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
        => manager.GetAuditEntriesAsync(
            Math.Max(0, offset),
            Math.Clamp(limit, 1, 250),
            cancellationToken);
}

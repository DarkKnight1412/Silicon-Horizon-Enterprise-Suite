using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD nhân viên — <c>dbo.Employees</c> + đồng bộ <c>dbo.Users</c> và <c>dbo.UserRoles</c>
/// (Database/README.md mục 10: <c>/api/admin/employees</c>, kèm ghi chú "khi tạo/sửa nhân viên cần đồng bộ
/// <c>Users</c>, <c>UserRoles</c>, <c>Employees</c>").
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>.
/// <list type="bullet">
/// <item>POST tạo tài khoản (băm mật khẩu bằng PBKDF2 qua <c>IPasswordHasher</c>), gán vai trò theo chức vụ và
/// tạo hồ sơ nhân viên trong <b>một giao dịch</b>.</item>
/// <item>PUT cập nhật hồ sơ + tài khoản, và thay vai trò chức vụ nếu chức vụ đổi (vai trò khác như Admin giữ nguyên).</item>
/// <item>PATCH <c>/status</c>: cho nghỉ việc sẽ ghi <c>terminatedDate</c> và khóa tài khoản — không xóa cứng.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/admin/employees")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService) => _employeeService = employeeService;

    /// <summary>GET /api/v1/admin/employees?keyword=&amp;branchId=&amp;position=&amp;status=...</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetAll(
        [FromQuery] EmployeeListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _employeeService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/employees/{id}</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _employeeService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/admin/employees — tạo <c>Users</c> + <c>UserRoles</c> + <c>Employees</c> trong một giao dịch.
    /// Trùng tên đăng nhập hoặc mã nhân viên trả 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _employeeService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/employees/{id} — cập nhật hồ sơ và đồng bộ tài khoản/vai trò.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> Update(
        long id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
        => Ok(await _employeeService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// PATCH /api/v1/admin/employees/{id}/status — 0 Đang làm việc, 1 Đã nghỉ việc
    /// (cần <c>terminatedDate</c> khi nghỉ việc; tài khoản đăng nhập bị khóa tương ứng).
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> SetStatus(
        long id,
        [FromBody] SetEmployeeStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await _employeeService.SetStatusAsync(id, request, cancellationToken));
}

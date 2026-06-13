using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Authorize]
    [Route("api/accounts")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        /// <summary>UC2 — Consultar cuenta por ID.</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _accountService.GetByIdAsync(id);
            return Ok(result);
        }

        /// <summary>UC1 — Crear cuenta.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            var result = await _accountService.CreateAccountAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.AccountId }, result);
        }

        /// <summary>UC3 — Realizar depósito.</summary>
        [HttpPost("{id:guid}/deposits")]
        public async Task<IActionResult> Deposit(Guid id, [FromBody] DepositRequest request)
        {
            var result = await _accountService.DepositAsync(id, request);
            return Ok(result);
        }

        /// <summary>UC4 — Realizar retiro.</summary>
        [HttpPost("{id:guid}/withdrawals")]
        public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawalRequest request)
        {
            var result = await _accountService.WithdrawAsync(id, request);
            return Ok(result);
        }

        /// <summary>UC6 — Historial de movimientos paginado.</summary>
        [HttpGet("{id:guid}/movements")]
        public async Task<IActionResult> GetMovements(
            Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var result = await _accountService.GetMovementsAsync(id, page, pageSize);
            return Ok(result);
        }
    }
}

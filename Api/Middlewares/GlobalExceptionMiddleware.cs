using Application.Exceptions;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Api.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no manejado: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                UnsupportedCurrencyException e => (StatusCodes.Status400BadRequest, e.Message),
                InvalidBalanceException e      => (StatusCodes.Status400BadRequest, e.Message),
                InvalidAmountException e       => (StatusCodes.Status400BadRequest, e.Message),
                AccountNotActiveException e    => (StatusCodes.Status400BadRequest, e.Message),
                AccountNotFoundException e     => (StatusCodes.Status404NotFound, e.Message),
                CustomerNotFoundException e    => (StatusCodes.Status404NotFound, e.Message),
                _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor.")
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var body = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
            return context.Response.WriteAsync(body);
        }
    }
}

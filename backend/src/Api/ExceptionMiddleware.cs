using Microsoft.EntityFrameworkCore;
using Npgsql;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Api;

public sealed record ApiError(string Error, string Message, string TraceId, string? Field = null);

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request cancelled. TraceId {TraceId}", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            var (status, code, message, field) = exception switch
            {
                ValidationException validation => (400, "INVALID_CONFIGURATION", validation.Message, validation.Field),
                BadHttpRequestException => (400, "INVALID_REQUEST", "Verifique o formato dos dados enviados.", (string?)null),
                ResourceNotFoundException => (404, "PROJECT_NOT_FOUND", exception.Message, null),
                CatalogNotFoundException => (404, "CATALOG_NOT_FOUND", exception.Message, null),
                CatalogConflictException => (409, "CONCURRENT_UPDATE", exception.Message, null),
                SuiteNotFoundException => (404, "TEST_SUITE_NOT_FOUND", exception.Message, null),
                SuiteConflictException => (409, "CONCURRENT_UPDATE", exception.Message, null),
                ProjectArchivedException => (409, "PROJECT_ARCHIVED", exception.Message, null),
                RequestConflictException => (409, "CONCURRENT_UPDATE", exception.Message, null),
                NpgsqlException or DbUpdateException { InnerException: NpgsqlException }
                    or InvalidOperationException { InnerException: NpgsqlException } =>
                    (503, "DATABASE_UNAVAILABLE", "Banco indisponível ou ainda não preparado. Verifique o PostgreSQL e aplique as migrações.", null),
                _ => (500, "INTERNAL_ERROR", "Não foi possível concluir a solicitação.", null)
            };
            if (status >= 500) logger.LogError(exception, "Request failed. TraceId {TraceId}", context.TraceIdentifier);
            else logger.LogWarning("Request rejected. Error {Error} TraceId {TraceId}", code, context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ApiError(code, message, context.TraceIdentifier, field));
        }
    }
}

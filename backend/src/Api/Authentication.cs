using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using QaTestOrchestrator.Infrastructure;

namespace QaTestOrchestrator.Api;

public sealed record AccountDto(Guid Id, string Login, string Name, string Role, bool Active, Guid Version);
public sealed record LoginInput(string? Login, string? Password);
public sealed record AccountInput(string? Login, string? Name, string? Role, string? Password, bool Active = true, Guid? Version = null);
public sealed record PasswordInput(string? CurrentPassword, string? NewPassword);
public sealed class AccountConflictException() : Exception("O usuário mudou ou o login já está em uso. Recarregue antes de tentar novamente.");
public sealed class HttpActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Guid? Id => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Name => accessor.HttpContext?.User.Identity?.Name;
}
public sealed class AccountService(OrchestratorDbContext db, IPasswordHasher<Account> hasher, TimeProvider clock)
{
    public static AccountDto Dto(Account user) => new(user.Id, user.Login, user.Name, user.Role, user.Active, user.Version);
    public static void Password(string? password)
    {
        if (password is null || password.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Use uma senha de 12 a 128 caracteres.", "password");
    }
    public static string NormalizeLogin(string? login) => (login ?? "").Trim().ToLowerInvariant();
    public async Task<AccountDto> Save(Guid? id, AccountInput input, CancellationToken ct)
    {
        // Read registry before users: a concurrent admin update invalidates this transaction.
        var registry = await db.AccountRegistries.SingleAsync(ct);
        var user = id.HasValue ? await db.Accounts.SingleOrDefaultAsync(x => x.Id == id, ct) : new Account();
        if (user is null) throw new ValidationException("Usuário não encontrado.", "id");
        if (id.HasValue && input.Version != user.Version) throw new AccountConflictException();
        var login = NormalizeLogin(input.Login); var name = input.Name?.Trim() ?? "";
        if (!Regex.IsMatch(login, "^[a-z0-9][a-z0-9._@+-]{2,79}$")) throw new ValidationException("Login deve ter de 3 a 80 caracteres válidos.", "login");
        if (name.Length is < 1 or > 120) throw new ValidationException("Nome deve ter de 1 a 120 caracteres.", "name");
        if (input.Role is not ("Admin" or "Operator" or "Reader")) throw new ValidationException("Perfil inválido.", "role");
        if (await db.Accounts.AnyAsync(x => x.Login == login && x.Id != user.Id, ct)) throw new AccountConflictException();
        if (id.HasValue && user.Active && user.Role == "Admin" && (!input.Active || input.Role != "Admin") &&
            !await db.Accounts.AnyAsync(x => x.Id != user.Id && x.Active && x.Role == "Admin", ct))
            throw new ValidationException("Mantenha pelo menos um administrador ativo.", "role");
        if (!id.HasValue || input.Password is not null) { Password(input.Password); user.PasswordHash = hasher.HashPassword(user, input.Password!); }
        user.Login = login; user.Name = name; user.Role = input.Role; user.Active = input.Active;
        user.Version = Guid.NewGuid(); user.FailedAttempts = 0; user.LockedUntil = null;
        registry.Version = Guid.NewGuid();
        if (!id.HasValue) db.Accounts.Add(user);
        await Commit(ct); return Dto(user);
    }
    public async Task Bootstrap(string? login, string? name, string? password, CancellationToken ct)
    {
        // Load concurrency guard before checking whether bootstrap is still allowed.
        await db.AccountRegistries.SingleAsync(ct);
        if (await db.Accounts.AnyAsync(ct)) throw new ValidationException("Administrador inicial já configurado.", "login");
        await Save(null, new(login, name, "Admin", password), ct);
    }
    public async Task Commit(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AccountConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { throw new AccountConflictException(); }
    }
    public DateTime Now => clock.GetUtcNow().UtcDateTime;
}

public static class AuthenticationSetup
{
    private static readonly string DummyPasswordHash = new PasswordHasher<Account>().HashPassword(new Account(), Guid.NewGuid().ToString("N"));
    public static void AddWorkspaceAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActor, HttpActor>();
        builder.Services.AddScoped<AccountService>();
        builder.Services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();
        builder.Services.AddAntiforgery(o => {
            o.HeaderName = "X-CSRF-Token"; o.Cookie.Name = "qa.csrf"; o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
            o.Cookie.Name = "qa.session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = c => Reject(c.HttpContext, 401, "AUTH_REQUIRED", "Entre para continuar.");
            o.Events.OnRedirectToAccessDenied = c => Reject(c.HttpContext, 403, "FORBIDDEN", "Seu perfil não permite esta operação.");
            o.Events.OnValidatePrincipal = async c => {
                var db = c.HttpContext.RequestServices.GetRequiredService<OrchestratorDbContext>();
                var now = c.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                if (!Guid.TryParse(c.Principal?.FindFirstValue("session"), out var sessionId)) { c.RejectPrincipal(); return; }
                var valid = await (from session in db.LoginSessions join user in db.Accounts on session.AccountId equals user.Id
                    where session.Id == sessionId && session.ExpiresAt > now && user.Active && session.AccountVersion == user.Version
                    select session.Id).AnyAsync(c.HttpContext.RequestAborted);
                if (!valid) { c.RejectPrincipal(); await c.HttpContext.SignOutAsync(); }
            };
        });
        builder.Services.AddAuthorization(o => {
            o.AddPolicy("SignedIn", p => p.RequireAuthenticatedUser());
            o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole("Admin"));
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(c =>
                c.Resource is HttpContext h && (HttpMethods.IsGet(h.Request.Method) || HttpMethods.IsHead(h.Request.Method)
                || c.User.IsInRole("Admin") || c.User.IsInRole("Operator"))).Build();
        });
        builder.Services.AddRateLimiter(o => {
            o.RejectionStatusCode = 429;
            o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
        });
    }
    private static Task Reject(HttpContext h, int status, string code, string message)
    { h.Response.StatusCode = status; return h.Response.WriteAsJsonAsync(new ApiError(code, message, h.TraceIdentifier)); }
    public static void UseWorkspaceAuthentication(this WebApplication app)
    {
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.Use(async (context, next) => {
            if (context.Request.Path.StartsWithSegments("/api")) {
                context.Response.Headers.CacheControl = "no-store";
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method)) {
                    try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
                    catch (AntiforgeryValidationException) { await Reject(context, 400, "CSRF_INVALID", "A verificação da sessão expirou. Recarregue e tente novamente."); return; }
                }
            }
            await next(context);
        });
    }
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");
        auth.MapGet("/csrf", (HttpContext h, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(h).RequestToken })).AllowAnonymous();
        auth.MapPost("/login", async (LoginInput input, HttpContext h, OrchestratorDbContext db, AccountService service, IPasswordHasher<Account> hasher, CancellationToken ct) => {
            if (input.Login?.Length > 80 || input.Password is null || input.Password.Length > 128) return InvalidLogin();
            var login = AccountService.NormalizeLogin(input.Login);
            var user = await db.Accounts.SingleOrDefaultAsync(x => x.Login == login, ct);
            var verified = hasher.VerifyHashedPassword(user ?? new Account(), user?.PasswordHash ?? DummyPasswordHash, input.Password);
            if (user is null || !user.Active || user.LockedUntil > service.Now) return InvalidLogin();
            if (verified == PasswordVerificationResult.Failed) {
                user.FailedAttempts++;
                if (user.FailedAttempts >= 5) { user.LockedUntil = service.Now.AddMinutes(15); user.FailedAttempts = 0; }
                await service.Commit(ct); return InvalidLogin();
            }
            user.FailedAttempts = 0; user.LockedUntil = null;
            if (verified == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, input.Password);
            // Rotate any session presented during login and discard expired session rows.
            if (Guid.TryParse(h.User.FindFirstValue("session"), out var oldId)) await db.LoginSessions.Where(x => x.Id == oldId).ExecuteDeleteAsync(ct);
            await db.LoginSessions.Where(x => x.ExpiresAt <= service.Now).ExecuteDeleteAsync(ct);
            var session = new LoginSession { AccountId = user.Id, AccountVersion = user.Version, ExpiresAt = service.Now.AddHours(8) };
            db.LoginSessions.Add(session); await service.Commit(ct);
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Role, user.Role), new Claim("session", session.Id.ToString()) };
            await h.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = false, ExpiresUtc = new DateTimeOffset(session.ExpiresAt) });
            return Results.Ok(AccountService.Dto(user));
        }).AllowAnonymous().RequireRateLimiting("login");
        auth.MapGet("/me", async (ICurrentActor actor, OrchestratorDbContext db, CancellationToken ct) => AccountService.Dto(await db.Accounts.SingleAsync(x => x.Id == actor.Id, ct))).RequireAuthorization("SignedIn");
        auth.MapPost("/logout", async (HttpContext h, OrchestratorDbContext db, CancellationToken ct) => {
            if (Guid.TryParse(h.User.FindFirstValue("session"), out var id)) await db.LoginSessions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            await h.SignOutAsync(); return Results.Ok(new { signedOut = true });
        }).RequireAuthorization("SignedIn");
        auth.MapPost("/password", async (PasswordInput input, ICurrentActor actor, HttpContext h, OrchestratorDbContext db, AccountService service, IPasswordHasher<Account> hasher, CancellationToken ct) => {
            var user = await db.Accounts.SingleAsync(x => x.Id == actor.Id, ct);
            if (input.CurrentPassword is null || input.CurrentPassword.Length > 128 || hasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed) return InvalidLogin();
            AccountService.Password(input.NewPassword); user.PasswordHash = hasher.HashPassword(user, input.NewPassword!); user.Version = Guid.NewGuid();
            await service.Commit(ct); await h.SignOutAsync(); return Results.Ok(new { signedOut = true });
        }).RequireAuthorization("SignedIn").RequireRateLimiting("login");
        var users = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("Admin");
        users.MapGet("/", async (OrchestratorDbContext db, CancellationToken ct, int page = 1) => {
            if (page is < 1 or > 100000) throw new ValidationException("Página inválida.", "page");
            var total = await db.Accounts.CountAsync(ct);
            var list = await db.Accounts.AsNoTracking().OrderBy(x => x.Login).Skip((page - 1) * 20).Take(20).ToListAsync(ct);
            return Results.Ok(new { items = list.Select(AccountService.Dto), total, page });
        });
        users.MapPost("/", async (AccountInput input, AccountService service, CancellationToken ct) => Results.Ok(await service.Save(null, input, ct)));
        users.MapPut("/{id:guid}", (Guid id, AccountInput input, AccountService service, CancellationToken ct) => service.Save(id, input, ct));
    }
    private static IResult InvalidLogin() => Results.Json(new { error = "INVALID_CREDENTIALS", message = "Login ou senha inválidos, ou conta temporariamente indisponível." }, statusCode: 401);
}

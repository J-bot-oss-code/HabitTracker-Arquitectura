using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AccessControl.Application.Interfaces;
using AccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Middlewares;
using Xunit;

namespace WebApi.Tests;

public sealed class TokenBlacklistMiddlewareTests
{
    [Fact]
    public async Task ConservaLaPrecisionDeTicksUTCAlLeerElToken()
    {
        var emitidoEn = new DateTimeOffset(638960000000000001, TimeSpan.Zero);
        var usuarioId = Guid.NewGuid();
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
                new Claim(JwtProvider.SessionIssuedAtTicksClaim, emitidoEn.UtcDateTime.Ticks.ToString())
            ]));
        var blacklist = new BlacklistFalsa();
        var middleware = new TokenBlacklistMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TokenBlacklistMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString())], "test"));
        context.Request.Headers.Authorization = $"Bearer {token}";

        await middleware.InvokeAsync(context, blacklist);

        Assert.Equal(emitidoEn.UtcTicks, blacklist.EmitidoEnCapturado?.UtcTicks);
    }

    private sealed class BlacklistFalsa : ITokenBlacklist
    {
        public DateTimeOffset? EmitidoEnCapturado { get; private set; }

        public void InvalidarToken(string token) { }
        public bool EstaInvalidado(string token) => false;
        public void RevocarSesionesAnteriores(Guid usuarioId) { }
        public bool SesionVigente(Guid usuarioId, DateTimeOffset emitidoEn)
        {
            EmitidoEnCapturado = emitidoEn;
            return true;
        }
    }
}

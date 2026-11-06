using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>
/// Gestor de sesiones real con repositorios simulados. Las pruebas de UsuarioService comprueban que cada evento que debe
/// cortar sesiones llega hasta <c>ITokenBlacklist.RevocarSesionesAnteriores</c> a través del único punto de revocación.
/// </summary>
internal static class GestorSesionesDePrueba
{
    public static IGestorSesiones Crear(ITokenBlacklist? blacklist = null, ISesionRepository? sesiones = null) =>
        new GestorSesiones(
            sesiones ?? Mock.Of<ISesionRepository>(),
            Mock.Of<IUsuarioRepository>(),
            blacklist ?? Mock.Of<ITokenBlacklist>(),
            Mock.Of<IJwtProvider>(),
            OpcionesSesion.PorDefecto,
            Mock.Of<ILogger<GestorSesiones>>());
}

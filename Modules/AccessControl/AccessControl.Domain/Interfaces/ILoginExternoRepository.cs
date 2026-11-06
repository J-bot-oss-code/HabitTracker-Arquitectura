using AccessControl.Domain.Entities;

namespace AccessControl.Domain.Interfaces;

public interface ILoginExternoRepository
{
    /// <summary>El vínculo de esa identidad del proveedor, sea de quien sea.</summary>
    Task<LoginExterno?> ObtenerPorSubjectAsync(string proveedor, string subject);

    Task<LoginExterno?> ObtenerDeUsuarioAsync(Guid usuarioId, string proveedor);

    Task<IReadOnlyList<LoginExterno>> ListarDeUsuarioAsync(Guid usuarioId);

    Task AgregarAsync(LoginExterno vinculo);

    /// <summary>Quita el vínculo. False si no existía.</summary>
    Task<bool> EliminarAsync(Guid usuarioId, string proveedor);
}

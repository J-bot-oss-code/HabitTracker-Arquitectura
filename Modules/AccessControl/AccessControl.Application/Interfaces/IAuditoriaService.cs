namespace AccessControl.Application.Interfaces
{
    public interface IAuditoriaService
    {
        Task RegistroAuditoriaAsync
        (Guid usuarioId,
         string nombreUsuarioResponsable,
          string accion,
           string entidadAfectada,
           string identificador,
           string valorAnterior,
           string valorNuevo
            );
    }
}
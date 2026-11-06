namespace AccessControl.Application.Service;

/// <summary>Configuración de la baja de cuenta (sección "Privacidad").</summary>
public sealed class OpcionesPrivacidad
{
    public const string Seccion = "Privacidad";
    public const int MaximoDiasGracia = 365;
    public const int MaximoIntervaloMinutos = 7 * 24 * 60;

    /// <summary>Días entre que se pide eliminar la cuenta y que se borra de verdad. Durante ese tiempo se puede cancelar.</summary>
    public int DiasGraciaBaja { get; set; } = 30;

    /// <summary>Cada cuántos minutos el proceso en segundo plano busca cuentas cuyo plazo terminó (por defecto, una vez al día).</summary>
    public int IntervaloEjecucionMinutos { get; set; } = 24 * 60;

    public static OpcionesPrivacidad PorDefecto { get; } = new();

    /// <summary>Una configuración inválida detiene el arranque de la aplicación (RD-07).</summary>
    public void Validar()
    {
        if (DiasGraciaBaja < 0 || DiasGraciaBaja > MaximoDiasGracia)
        {
            throw new InvalidOperationException($"Privacidad:DiasGraciaBaja debe estar entre 0 y {MaximoDiasGracia}.");
        }

        if (IntervaloEjecucionMinutos < 1 || IntervaloEjecucionMinutos > MaximoIntervaloMinutos)
        {
            throw new InvalidOperationException($"Privacidad:IntervaloEjecucionMinutos debe estar entre 1 y {MaximoIntervaloMinutos}.");
        }
    }
}

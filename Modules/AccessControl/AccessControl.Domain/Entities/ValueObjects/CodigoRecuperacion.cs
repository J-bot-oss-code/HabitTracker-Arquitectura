using System;

namespace AccessControl.Domain.Entities.ValueObjects;

public sealed class CodigoRecuperacion
{
    public string Valor { get; private set; } = null!;
    public DateTime FechaEmision { get; private set; }
    public DateTime FechaVencimiento { get; private set; }
    public bool Usado { get; private set; }

    private CodigoRecuperacion() {} // Constructor para EF Core

    public CodigoRecuperacion(string valor, int horasValidez)
    {
        if (string.IsNullOrWhiteSpace(valor)) throw new ArgumentException("El código no puede estar vacío.", nameof(valor));
        
        Valor = valor;
        FechaEmision = DateTime.UtcNow;
        FechaVencimiento = DateTime.UtcNow.AddHours(horasValidez);
        Usado = false;
    }

    public bool EsValido(string codigoIngresado) => 
        !Usado && Valor == codigoIngresado && FechaVencimiento > DateTime.UtcNow;

    public void MarcarComoUsado() => Usado = true;
}

using System;
using System.Security.Cryptography;
using System.Text;

namespace AccessControl.Domain.Entities.ValueObjects;

public sealed class CodigoRecuperacion
{
    private const int MaxIntentos = 5;

    public string Valor { get; private set; } = null!;
    public DateTime FechaEmision { get; private set; }
    public DateTime FechaVencimiento { get; private set; }
    public bool Usado { get; private set; }
    public int IntentosFallidos { get; private set; }

    private CodigoRecuperacion() {} // Constructor para EF Core

    public CodigoRecuperacion(string valor, int horasValidez)
    {
        if (string.IsNullOrWhiteSpace(valor)) throw new ArgumentException("El código no puede estar vacío.", nameof(valor));
        
        Valor = valor;
        FechaEmision = DateTime.UtcNow;
        FechaVencimiento = DateTime.UtcNow.AddHours(horasValidez);
        Usado = false;
    }

    public bool EsValido(string codigoIngresado)
    {
        if (Usado || codigoIngresado is null || FechaVencimiento <= DateTime.UtcNow)
        {
            return false;
        }

        // Comparación en tiempo constante, igual que TokenActivacion: con == el tiempo de respuesta crece con cada carácter
        // acertado y permite adivinar el código por partes.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Valor),
            Encoding.UTF8.GetBytes(codigoIngresado));
    }

    public void MarcarComoUsado() => Usado = true;

    public void RegistrarFallo()
    {
        IntentosFallidos++;

        // Tras 5 códigos incorrectos el código se invalida: evita fuerza bruta.
        if (IntentosFallidos >= MaxIntentos)
        {
            Usado = true;
        }
    }
}

namespace AccessControl.Domain.Entities.ValueObjects
{
    /// <summary>
    /// Código con el que el usuario activa su cuenta (hoy, 6 dígitos que llegan por correo).
    /// Como es corto, no se acepta sin límite: tras <see cref="MaxIntentos"/> códigos incorrectos queda
    /// invalidado y hay que pedir uno nuevo. El vencimiento NO se toca al invalidar, para que se pueda saber
    /// cuándo se emitió (enfriamiento del reenvío).
    /// </summary>
    public sealed class TokenActivacion
    {
        public const int MaxIntentos = 5;

        public string Valor { get; private set; } = null!;
        public DateTime Vencimiento {get; private set;}

        // Nullable a propósito: las cuentas que ya estaban activas antes de esta columna tienen NULL, y así EF
        // sigue entendiendo que NO tienen un código de activación pendiente.
        private int? _intentosFallidos;

        public int IntentosFallidos => _intentosFallidos ?? 0;

        private TokenActivacion() {}

        public TokenActivacion(string valor, DateTime vencimiento)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                throw new ArgumentException("El token de activación no puede estar vacío.", nameof(valor));
            }

            if(vencimiento <= DateTime.UtcNow)
            {
                throw new ArgumentException("El vencimiento debe ser futuro. ", nameof(vencimiento));
            }

            Valor = valor;
            Vencimiento = vencimiento;
            _intentosFallidos = 0;
        }

        public bool EsValido(string tokenIngresado) =>
            tokenIngresado is not null
            && CoincideEnTiempoConstante(Valor, tokenIngresado)
            && Vencimiento > DateTime.UtcNow
            && IntentosFallidos < MaxIntentos;

        // Comparación de tiempo constante: no revela cuántos dígitos iniciales acertó quien adivina.
        private static bool CoincideEnTiempoConstante(string esperado, string ingresado) =>
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(esperado),
                System.Text.Encoding.UTF8.GetBytes(ingresado));

        public void RegistrarFallo() => _intentosFallidos = IntentosFallidos + 1;
    }
}

namespace AccessControl.Application.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verificar(string hash, string passwordPlana);

        /// <summary>
        /// Gasta el mismo tiempo que <see cref="Verificar"/> contra un hash real y descarta el resultado. Se usa cuando el
        /// correo no existe: sin esto, el inicio de sesión respondería antes y delataría qué correos están registrados.
        /// </summary>
        void SimularVerificacion(string passwordPlana);

    }
}
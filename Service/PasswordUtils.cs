using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Utilidades para manejo de contraseñas (hash compatible con sl_login).
    /// </summary>
    public static class PasswordUtils
    {
        public const int LongitudMinima = 8;

        // Logins viejos sin password_iteraciones guardado se verifican con este valor
        public const int IteracionesLegado = 10000;
        public const int IteracionesActuales = 100000;

        // Misma regla que el frontend, validada de nuevo acá por si llaman a la API directo
        public static void ValidarFortaleza(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < LongitudMinima)
                throw new Exception($"La contraseña debe tener al menos {LongitudMinima} caracteres.");

            if (!password.Any(char.IsUpper))
                throw new Exception("La contraseña debe tener al menos una letra mayúscula.");

            if (!password.Any(c => char.IsDigit(c) || !char.IsLetterOrDigit(c)))
                throw new Exception("La contraseña debe tener al menos un número o un carácter especial.");
        }
        public static void CreateHash(string password, out byte[] salt, out byte[] hash)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                salt = new byte[16];
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, IteracionesActuales))
            {
                hash = pbkdf2.GetBytes(32);
            }
        }

        public static bool VerificarHash(string password, byte[] salt, byte[] hash, int? iteraciones)
        {
            if (string.IsNullOrEmpty(password) || salt == null || hash == null)
                return false;

            var iters = iteraciones.HasValue && iteraciones.Value > 0 ? iteraciones.Value : IteracionesLegado;

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iters))
            {
                var calculado = pbkdf2.GetBytes(hash.Length);
                return FixedTimeEquals(calculado, hash);
            }
        }

        // Comparación sin cortocircuito: recorre todo el array aunque encuentre una diferencia antes
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }

        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null)
                return false;

            return FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
        }

        /// <summary>
        /// Genera una clave aleatoria segura (letras, dígitos y al menos un carácter especial).
        /// </summary>
        public static string GenerarClaveAleatoria(int longitud = 12)
        {
            const string mayus = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string minus = "abcdefghjkmnpqrstuvwxyz";
            const string numeros = "23456789";
            const string especial = "!@#$%";
            var todos = mayus + minus + numeros + especial;
            var resultado = new char[longitud];
            byte[] bytes;
            using (var rng = RandomNumberGenerator.Create())
            {
                bytes = new byte[longitud * 2];
                rng.GetBytes(bytes);
            }
            resultado[0] = mayus[bytes[0] % mayus.Length];
            resultado[1] = minus[bytes[1] % minus.Length];
            resultado[2] = numeros[bytes[2] % numeros.Length];
            resultado[3] = especial[bytes[3] % especial.Length];
            for (var i = 4; i < longitud; i++)
                resultado[i] = todos[bytes[(i * 2) % bytes.Length] % todos.Length];
            return new string(resultado);
        }
    }
}

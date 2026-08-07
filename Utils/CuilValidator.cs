using System;
using System.Text.RegularExpressions;

namespace smartlunch_api.Utils
{
    /// <summary>
    /// Utilidad para validar CUIL (Código Único de Identificación Laboral) argentino
    /// </summary>
    public static class CuilValidator
    {
        /// <summary>
        /// Valida el formato y el dígito verificador del CUIL
        /// </summary>
        /// <param name="cuil">CUIL a validar (puede venir con o sin guiones)</param>
        /// <returns>True si el CUIL es válido</returns>
        public static bool EsValido(string cuil)
        {
            if (string.IsNullOrWhiteSpace(cuil))
                return false;

            // Remover guiones y espacios
            var cuilLimpio = cuil.Replace("-", "").Replace(" ", "").Trim();

            // Debe tener exactamente 11 dígitos
            if (!Regex.IsMatch(cuilLimpio, @"^\d{11}$"))
                return false;

            // Extraer los dígitos
            var digitos = cuilLimpio.ToCharArray();
            var multiplicadores = new int[] { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

            // Calcular el dígito verificador
            int suma = 0;
            for (int i = 0; i < 10; i++)
            {
                suma += int.Parse(digitos[i].ToString()) * multiplicadores[i];
            }

            int resto = suma % 11;
            int digitoVerificador;

            if (resto < 2)
            {
                digitoVerificador = resto;
            }
            else
            {
                digitoVerificador = 11 - resto;
            }

            // Comparar con el último dígito
            int ultimoDigito = int.Parse(digitos[10].ToString());
            return digitoVerificador == ultimoDigito;
        }

        /// <summary>
        /// Formatea el CUIL con guiones (XX-XXXXXXXX-X)
        /// </summary>
        public static string Formatear(string cuil)
        {
            if (string.IsNullOrWhiteSpace(cuil))
                return cuil;

            var cuilLimpio = cuil.Replace("-", "").Replace(" ", "").Trim();
            
            if (cuilLimpio.Length == 11)
            {
                return $"{cuilLimpio.Substring(0, 2)}-{cuilLimpio.Substring(2, 8)}-{cuilLimpio.Substring(10, 1)}";
            }

            return cuil;
        }
    }
}


using System;

namespace Boleto2Net.Util
{
    /// <summary>
    /// PAY-2324: helpers para sanitização e validação de CPF/CNPJ
    /// compartilhados entre setters Cedente.CPFCNPJ e Sacado.CPFCNPJ.
    /// </summary>
    internal static class CpfCnpjValidator
    {
        private const string ErroFormato = "CPF/CNPJ inválido: Utilize 11 dígitos para CPF ou 14 caracteres para CNPJ (somente dígitos quando flag CnabSettings.SuportarCnpjAlfanumerico OFF; 12 alfanuméricos + 2 dígitos verificadores quando ON).";

        /// <summary>
        /// Sanitiza separadores comuns (., -, /) e aplica trim.
        /// </summary>
        public static string SanitizeSeparators(string value)
        {
            if (value == null)
                throw new ArgumentException(ErroFormato);
            return value.Trim().Replace(".", "").Replace("-", "").Replace("/", "");
        }

        /// <summary>
        /// Aplica regras flag-aware. Lança ArgumentException em qualquer formato inválido.
        /// Caminho ON: aceita CPF (11 dígitos), CNPJ numérico (14 dígitos) e CNPJ alfa (12 alfanuméricos + 2 dígitos).
        /// Caminho OFF: aceita apenas CPF/CNPJ totalmente numérico (rejeita letras explicitamente).
        /// </summary>
        public static string NormalizeAndValidate(string sanitized)
        {
            if (CnabSettings.SuportarCnpjAlfanumerico)
            {
                var upper = sanitized.ToUpperInvariant();
                if (upper.Length == 11 && IsAllDigits(upper)) return upper;
                if (upper.Length == 14 && IsValidAlphaCnpjFormat(upper)) return upper;
                throw new ArgumentException(ErroFormato);
            }

            if ((sanitized.Length != 11 && sanitized.Length != 14) || !IsAllDigits(sanitized))
                throw new ArgumentException(ErroFormato);
            return sanitized;
        }

        private static bool IsAllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        private static bool IsValidAlphaCnpjFormat(string cnpj)
        {
            for (int i = 0; i < 12; i++)
            {
                var c = cnpj[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            }
            for (int i = 12; i < 14; i++)
            {
                var c = cnpj[i];
                if (c < '0' || c > '9') return false;
            }
            return true;
        }
    }
}

namespace Boleto2Net
{
    /// <summary>
    /// Configuracoes estaticas globais do Boleto2Net.
    /// Lido em hot path; escrito 1x por scope no consumer (per-call setup).
    /// Default preserva comportamento legacy 100%.
    /// </summary>
    public static class CnabSettings
    {
        /// <summary>
        /// Habilita suporte a CNPJ alfanumerico (IN RFB 2.229/2024).
        /// Quando true:
        ///   - Setters Cedente.CPFCNPJ e Sacado.CPFCNPJ aceitam [A-Z0-9]{12}\d{2}
        ///   - Geracao de CNAB de remessa lanca NotSupportedException
        ///     se algum CPFCNPJ contiver letras (PAY-2100 cobrira CNAB write).
        /// Default: false (preserva comportamento pre-PAY-2324).
        /// </summary>
        public static bool SuportarCnpjAlfanumerico { get; set; } = false;
    }
}

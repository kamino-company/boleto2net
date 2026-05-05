namespace Boleto2Net
{
    /// <summary>
    /// Configuracoes estaticas globais do Boleto2Net.
    /// Default preserva comportamento legacy 100%.
    /// </summary>
    public static class CnabSettings
    {
        private static volatile bool _suportarCnpjAlfanumerico = false;

        /// <summary>
        /// Habilita CNPJ alfanumerico (IN RFB 2.229/2024) nos setters
        /// Cedente.CPFCNPJ e Sacado.CPFCNPJ — aceita [A-Z0-9]{12} + 2 digitos verificadores.
        /// Geracao de CNAB de remessa com CNPJ alfa ainda nao e suportada
        /// (aguardando spec FEBRABAN). Default: false.
        /// </summary>
        public static bool SuportarCnpjAlfanumerico
        {
            get => _suportarCnpjAlfanumerico;
            set => _suportarCnpjAlfanumerico = value;
        }
    }
}

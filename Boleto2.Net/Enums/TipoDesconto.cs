namespace Boleto2Net
{
    public enum TipoDesconto
    {
        /// <summary>
        /// Título não tem desconto
        /// </summary>
        SemDesconto,
        /// <summary>
        /// Valor Fixo Até a Data Informada
        /// </summary>
        ValorDataFixa,
        /// <summary>
        /// Valor por Antecipação Dia Corrido
        /// </summary>
        ValorDiaCorrido,
        /// <summary>
        /// Valor por Antecipação Dia Útil
        /// </summary>
        ValorDiaUtil,
        /// <summary>
        /// Percentual Até a Data Informada
        /// </summary>
        PercDataFixa,
        /// <summary>
        /// Percentual Sobre o Valor Nominal Dia Corrido
        /// </summary>
        PercDiaCorrido,
        /// <summary>
        /// Percentual Sobre o Valor Nominal Dia Útil
        /// </summary>
        PercDiaUtil
    }
}

using System;

namespace Boleto2Net
{
    /// <summary>
    /// Cobrança Caucionada do BTG Pactual. Mesma montagem da simples - o que muda é o contrato com o
    /// banco, não o campo livre.
    /// </summary>
    [CarteiraCodigo("3", "03")]
    internal class BancoBTGPactualCarteira03 : ICarteira<BancoBTGPactual>
    {
        internal static Lazy<ICarteira<BancoBTGPactual>> Instance { get; } = new Lazy<ICarteira<BancoBTGPactual>>(() => new BancoBTGPactualCarteira03());

        private BancoBTGPactualCarteira03()
        {
        }

        public void FormataNossoNumero(Boleto boleto)
            => BancoBTGPactualCarteira.FormataNossoNumero(boleto);

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
            => BancoBTGPactualCarteira.FormataCodigoBarraCampoLivre(boleto);
    }
}

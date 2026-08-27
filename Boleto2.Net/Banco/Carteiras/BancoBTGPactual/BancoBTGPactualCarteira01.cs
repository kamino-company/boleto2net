using System;
using Boleto2Net.Extensions;
using static System.String;

namespace Boleto2Net
{
    /// <summary>
    /// Cobrança Simples do BTG Pactual - a modalidade que a VAN bancária registra.
    ///
    /// Aceita "1" e "01": o cadastro da conta guarda a carteira como o cliente a digitou, e o campo livre
    /// exige dois dígitos. Deixar só uma das grafias implementada faria a composição falhar por diferença
    /// de zero à esquerda, que é indistinguível de "banco não implementado" para quem lê o erro.
    /// </summary>
    [CarteiraCodigo("1", "01")]
    internal class BancoBTGPactualCarteira01 : ICarteira<BancoBTGPactual>
    {
        internal static Lazy<ICarteira<BancoBTGPactual>> Instance { get; } = new Lazy<ICarteira<BancoBTGPactual>>(() => new BancoBTGPactualCarteira01());

        private BancoBTGPactualCarteira01()
        {
        }

        public void FormataNossoNumero(Boleto boleto)
            => BancoBTGPactualCarteira.FormataNossoNumero(boleto);

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
            => BancoBTGPactualCarteira.FormataCodigoBarraCampoLivre(boleto);
    }
}

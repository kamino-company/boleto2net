using System;
using Boleto2Net.Extensions;
using static System.String;

namespace Boleto2Net
{
    [CarteiraCodigo("112")]
    internal class BancoInterCarteira112 : ICarteira<BancoInter>
    {
        internal static Lazy<ICarteira<BancoInter>> Instance { get; } = new Lazy<ICarteira<BancoInter>>(() => new BancoInterCarteira112());

        private BancoInterCarteira112()
        {

        }

        public void FormataNossoNumero(Boleto boleto)
        {
            if (IsNullOrWhiteSpace(boleto.NossoNumero) || boleto.NossoNumero == "00000000000")
            {
                // Banco irá gerar Nosso Número
                boleto.NossoNumero = new String('0', 11);
                boleto.NossoNumeroDV = "0";
                boleto.NossoNumeroFormatado = $"{boleto.Carteira}/{boleto.NossoNumero}-{boleto.NossoNumeroDV}";
            }
            else
            {
                // Banco Inter não permite números gerados pela empresa, o nosso número sempre é disponibilizado pelo banco no arquivo de retorno
                // Nosso número não pode ter mais de 11 dígitos (Nosso Número com DV)
                if (boleto.NossoNumero.Length > 11)
                    throw new Exception($"Nosso Número ({boleto.NossoNumero}) deve conter 11 dígitos.");

                boleto.NossoNumero = boleto.NossoNumero.PadLeft(11, '0');                
            }
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            return $"0001112{boleto.NumeroOperacao.PadLeft(7, '0')}{boleto.NossoNumero.PadLeft(11, '0')}";
        }
    }
}
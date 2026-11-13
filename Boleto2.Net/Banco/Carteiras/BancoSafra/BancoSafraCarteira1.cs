using System;
using Boleto2Net.Extensions;
using static System.String;

namespace Boleto2Net
{
    [CarteiraCodigo("1")]
    internal class BancoSafraCarteira1 : ICarteira<BancoSafra>
    {
        internal static Lazy<ICarteira<BancoSafra>> Instance { get; } = new Lazy<ICarteira<BancoSafra>>(() => new BancoSafraCarteira1());

        private BancoSafraCarteira1()
        {

        }

        public void FormataNossoNumero(Boleto boleto)
        {
            if (IsNullOrWhiteSpace(boleto.NossoNumero))
                throw new Exception("Nosso Número não informado.");

            // Nosso número não pode ter mais de 8 dígitos
            if (boleto.NossoNumero.Length > 8)
                throw new Exception($"Nosso Número ({boleto.NossoNumero}) deve conter 8 dígitos.");

            boleto.NossoNumero = boleto.NossoNumero.PadLeft(8, '0');
            boleto.NossoNumeroDV = boleto.NossoNumero.CalcularDVSafra();
            // O guia Safra pede no boleto os mesmos 9 caracteres do arquivo remessa (038-046), sem hífen.
            boleto.NossoNumeroFormatado = $"{boleto.NossoNumero}{boleto.NossoNumeroDV}";
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            // 7 + agência (5 posições, sem DV) + conta (9 posições, com DV) + nosso número (9) + 2 = cobrança registrada.
            var contaBancaria = boleto.Banco.Cedente.ContaBancaria;
            return $"{boleto.Banco.Digito}{contaBancaria.Agencia}{BancoSafra.ContaCodigoBeneficiario(contaBancaria)}{boleto.NossoNumero}{boleto.NossoNumeroDV}2";
        }
    }
}
using System;
using System.Linq;
using Boleto2Net.Extensions;
using static System.String;

namespace Boleto2Net
{
    [CarteiraCodigo("1/01")]
    internal class BancoSincoobCarteira1: ICarteira<BancoSicoob>
    {
        private const string NovoContratoIdNegocio = "9";
        private const string NovoContratoModalidade = "01";
        private const int TamanhoCooperativa = 4;
        private const int TamanhoContrato = 9;
        private const int TamanhoNossoNumero = 9;

        internal static Lazy<ICarteira<BancoSicoob>> Instance { get; } = new Lazy<ICarteira<BancoSicoob>>(() => new BancoSincoobCarteira1());

        private BancoSincoobCarteira1()
        {

        }

        public void FormataNossoNumero(Boleto boleto)
        {
            var cedente = boleto.Banco.Cedente;
            if (cedente.ContaBancaria.TipoImpressaoBoleto == TipoImpressaoBoleto.Empresa & boleto.NossoNumero == Empty)
                throw new Exception("Nosso Número não informado.");

            if (boleto.SicoobNovoContrato)
            {
                // No Novo Contrato o nosso número ocupa 9 posições do campo livre, sem DV embutido.
                boleto.NossoNumero = ExigirDigitos(boleto.NossoNumero, TamanhoNossoNumero, "Nosso Número");
                boleto.NossoNumeroDV = Empty;
                boleto.NossoNumeroFormatado = boleto.NossoNumero;
                return;
            }

            // Nosso número não pode ter mais de 7 dígitos
            if (boleto.NossoNumero.Length > 7)
                throw new Exception("Nosso Número (" + boleto.NossoNumero + ") deve conter 7 dígitos.");

            boleto.NossoNumero = boleto.NossoNumero.PadLeft(7, '0');

            // Base para calcular DV: Agencia (4 caracteres) Código do Cedente com dígito (10 caracteres) Nosso Número (7 caracteres)
            var baseCalculoDV = $"{cedente.ContaBancaria.Agencia}{cedente.Codigo.PadLeft(9, '0')}{cedente.CodigoDV}{boleto.NossoNumero}";
            boleto.NossoNumeroDV = baseCalculoDV.CalcularDVSicoob();
            boleto.NossoNumeroFormatado = $"{boleto.NossoNumero}-{boleto.NossoNumeroDV}";
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            var cedente = boleto.Banco.Cedente;
            var contaBancaria = cedente.ContaBancaria;

            if (boleto.SicoobNovoContrato)
            {
                // Id.Negócio(1) + Cooperativa(4) + Contrato(9) + NossoNúmero(9) + Modalidade(2) = 25 posições.
                var cooperativa = ExigirDigitos(contaBancaria.Agencia, TamanhoCooperativa, "Cooperativa");
                var contrato = ExigirDigitos(boleto.SicoobNumeroContrato, TamanhoContrato, "Número do Contrato");
                var nossoNumero = ExigirDigitos(boleto.NossoNumero, TamanhoNossoNumero, "Nosso Número");

                return $"{NovoContratoIdNegocio}{cooperativa}{contrato}{nossoNumero}{NovoContratoModalidade}";
            }

            return $"{boleto.Carteira}{contaBancaria.Agencia}{boleto.VariacaoCarteira}{cedente.Codigo}{cedente.CodigoDV}{boleto.NossoNumero}{boleto.NossoNumeroDV}001";
        }

        /// <summary>
        /// Completa o valor com zeros à esquerda, recusando o que não couber no campo. Truncar ou zerar em
        /// silêncio geraria uma linha com dígitos verificadores válidos apontando para outro título.
        /// Exige dígito ASCII: char.IsDigit aceita algarismo Unicode, que depois quebra o cálculo do DV.
        /// </summary>
        private static string ExigirDigitos(string valor, int tamanho, string campo)
        {
            valor = valor ?? Empty;

            if (valor.Length == 0 || valor.Length > tamanho || !valor.All(c => c >= '0' && c <= '9'))
                throw new Exception($"{campo} ({valor}) deve conter de 1 a {tamanho} dígitos numéricos.");

            return valor.PadLeft(tamanho, '0');
        }
    }
}

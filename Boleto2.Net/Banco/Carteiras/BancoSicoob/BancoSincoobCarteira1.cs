using System;
using Boleto2Net.Extensions;
using static System.String;

namespace Boleto2Net
{
    [CarteiraCodigo("1/01")]
    internal class BancoSincoobCarteira1: ICarteira<BancoSicoob>
    {
        private const string NovoContratoIdNegocio = "9";
        private const string NovoContratoModalidade = "01";

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
                // Nosso número de 9 dígitos, sem DV embutido.
                if (boleto.NossoNumero.Length > 9)
                    throw new Exception("Nosso Número (" + boleto.NossoNumero + ") deve conter até 9 dígitos.");

                boleto.NossoNumero = boleto.NossoNumero.PadLeft(9, '0');
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
                var cooperativa = (contaBancaria.Agencia ?? Empty).PadLeft(4, '0').Right(4);
                var contrato = (boleto.SicoobNumeroContrato ?? Empty).PadLeft(9, '0').Right(9);
                var nossoNumero = boleto.NossoNumero.PadLeft(9, '0').Right(9);
                return $"{NovoContratoIdNegocio}{cooperativa}{contrato}{nossoNumero}{NovoContratoModalidade}";
            }

            return $"{boleto.Carteira}{contaBancaria.Agencia}{boleto.VariacaoCarteira}{cedente.Codigo}{cedente.CodigoDV}{boleto.NossoNumero}{boleto.NossoNumeroDV}001";
        }
    }
}

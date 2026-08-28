using System;
using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("BTG Pactual Carteira 01")]
    public class BancoBTGPactualCarteira01Tests
    {
        readonly IBanco _banco;

        public BancoBTGPactualCarteira01Tests()
        {
            var contaBancaria = new ContaBancaria
            {
                Agencia = "1234",
                DigitoAgencia = "",
                Conta = "123456",
                DigitoConta = "X",
                CarteiraPadrao = "1",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
                TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
            };
            _banco = Banco.Instancia(Bancos.BtgPactual);
            _banco.Cedente = Utils.GerarCedente("1213141", "", "", contaBancaria);
            _banco.FormataCedente();
        }

        // Campo livre: Agência(4) + Carteira(2) + Nosso Número(11) + Conta(7) + '0'
        //              1234        01           00000000453        0123456     0
        [TestCase(141.50, "453", "0", "001/00000000453-0", "20892690400000141501234010000000045301234560", "20891.23406 10000.000041 53012.345608 2 69040000014150", 2016, 9, 1)]
        [TestCase(2717.16, "456", "5", "001/00000000456-5", "20895693400002717161234010000000045601234560", "20891.23406 10000.000041 56012.345601 5 69340000271716", 2016, 10, 1)]
        [TestCase(297.21, "444", "1", "001/00000000444-1", "20896690500000297211234010000000044401234560", "20891.23406 10000.000041 44012.345607 6 69050000029721", 2016, 9, 2)]
        [TestCase(830, "562", "6", "001/00000000562-6", "20891702600000830001234010000000056201234560", "20891.23406 10000.000058 62012.345609 1 70260000083000", 2017, 1, 1)]
        // Nosso número de 8 dígitos, como a plataforma gera (prefixo + id do boleto), e resto 1 no módulo
        // 11 - o único caso em que o DV do nosso número é a letra 'P' em vez de dígito.
        [TestCase(22.00, "19323046", "P", "001/00019323046-P", "20898155500000022001234010001932304601234560", "20891.23406 10001.932309 46012.345602 8 15550000002200", 2026, 8, 31)]
        public void BtgPactual_01_BoletoOK(decimal valorTitulo, string nossoNumero, string digitoNossoNumero, string nossoNumeroFormatado, string codigoDeBarras, string linhaDigitavel, params int[] anoMesDia)
        {
            var boleto = new Boleto(_banco)
            {
                DataVencimento = new DateTime(anoMesDia[0], anoMesDia[1], anoMesDia[2]),
                ValorTitulo = valorTitulo,
                NossoNumero = nossoNumero,
                NumeroDocumento = "BTG001",
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Utils.GerarSacado()
            };

            boleto.ValidarDados();

            Assert.That(boleto.NossoNumeroDV, Is.EqualTo(digitoNossoNumero), "Dígito do nosso número inválido");
            Assert.That(boleto.NossoNumeroFormatado, Is.EqualTo(nossoNumeroFormatado), "Nosso número inválido");
            Assert.That(boleto.CodigoBarra.CodigoDeBarras, Is.EqualTo(codigoDeBarras), "Código de Barra inválido");
            Assert.That(boleto.CodigoBarra.LinhaDigitavel, Is.EqualTo(linhaDigitavel), "Linha digitável inválida");
        }

        [Test]
        public void BtgPactual_01_CampoLivreTem25Posicoes()
        {
            var boleto = new Boleto(_banco)
            {
                DataVencimento = new DateTime(2026, 8, 31),
                ValorTitulo = 22.00m,
                NossoNumero = "19323046",
                NumeroDocumento = "BTG001",
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Utils.GerarSacado()
            };

            boleto.ValidarDados();

            // A montagem do código de barras já rejeita campo livre fora de 25, mas a asserção explícita
            // aponta o erro para a composição do banco em vez de para a validação genérica.
            Assert.That(boleto.CodigoBarra.CampoLivre.Length, Is.EqualTo(25));
            Assert.That(boleto.CodigoBarra.CampoLivre, Is.EqualTo("1234010001932304601234560"));
        }

        [Test]
        public void BtgPactual_01_CarteiraAceitaComEsemZeroAEsquerda()
        {
            // O cadastro da conta guarda a carteira como o cliente digitou. "1" e "01" têm de produzir o
            // mesmo boleto, senão a composição falha por diferença de zero à esquerda.
            var contaBancaria = new ContaBancaria
            {
                Agencia = "1234",
                DigitoAgencia = "",
                Conta = "123456",
                DigitoConta = "X",
                CarteiraPadrao = "01",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
                TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
            };
            var banco = Banco.Instancia(Bancos.BtgPactual);
            banco.Cedente = Utils.GerarCedente("1213141", "", "", contaBancaria);
            banco.FormataCedente();

            var boleto = new Boleto(banco)
            {
                DataVencimento = new DateTime(2026, 8, 31),
                ValorTitulo = 22.00m,
                NossoNumero = "19323046",
                NumeroDocumento = "BTG001",
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Utils.GerarSacado()
            };

            boleto.ValidarDados();

            Assert.That(boleto.CodigoBarra.CodigoDeBarras, Is.EqualTo("20898155500000022001234010001932304601234560"));
        }

        [Test]
        public void BtgPactual_RenderizaBoletoMesmoSemLogoDoBanco()
        {
            // O assembly não embute logo para o 208. O logo é decoração; a falta dele não pode impedir a
            // renderização de um boleto cujo código de barras está correto - antes da guarda, o stream nulo
            // derrubava a montagem com NullReferenceException.
            var boleto = new Boleto(_banco)
            {
                DataVencimento = new DateTime(2026, 8, 31),
                ValorTitulo = 22.00m,
                NossoNumero = "19323046",
                NumeroDocumento = "BTG001",
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Utils.GerarSacado()
            };
            boleto.ValidarDados();

            var boletoBancario = new BoletoBancario { Boleto = boleto, OcultarInstrucoes = false, MostrarComprovanteEntrega = false };

            string html = null;
            Assert.DoesNotThrow(() => html = boletoBancario.MontaHtmlEmbedded());
            Assert.That(html, Is.Not.Null.And.Not.Empty);
            Assert.That(html, Does.Contain("20898155500000022001234010001932304601234560").Or.Contain("20891.23406"));
        }

        [Test]
        public void BtgPactual_RemessaNaoImplementada()
        {
            // A cobrança do BTG é registrada pela VAN, não por arquivo. Falhar alto é melhor que devolver
            // uma remessa formatada por analogia com outro banco, que o banco recusaria depois.
            var numeroRegistro = 0;

            Assert.Throws<NotImplementedException>(
                () => _banco.GerarHeaderRemessa(TipoArquivo.CNAB240, 1, ref numeroRegistro));
        }
    }
}

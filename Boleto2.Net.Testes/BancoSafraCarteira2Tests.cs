using System;
using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("Safra Carteira 2")]
    public class BancoSafraCarteira2
    {
        IBanco _banco;

        // BancoSafra é singleton e outros testes trocam o cedente dele: reconfigura antes de cada teste.
        [SetUp]
        public void ConfigurarCedente()
        {
            var contaBancaria = new ContaBancaria
            {
                Agencia = "1234",
                DigitoAgencia = "5",
                Conta = "123456",
                DigitoConta = "7",
                CarteiraPadrao = "2",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaVinculada,
                TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
            };
            _banco = Banco.Instancia(Bancos.Safra);
            _banco.Cedente = Utils.GerarCedente("", "", "", contaBancaria);
            _banco.FormataCedente();
        }

        [Test]
        public void Safra_2_REM400()
        {
            Utils.TestarHomologacao(_banco, TipoArquivo.CNAB400, nameof(BancoSafraCarteira2), 5, true, "?", 223344);
        }


        [TestCase(141.50, "453", "BB943A", "2", "000004537", "42292690400000141507012340012345670000045372", "42297.01232 40012.345670 00000.453720 2 69040000014150", 2016, 9, 1)]
        [TestCase(2711.12, "456", "BB874A", "4", "000004561", "42294693400002711127012340012345670000045612", "42297.01232 40012.345670 00000.456129 4 69340000271112", 2016, 10, 1)]
        [TestCase(645.39, "414", "BB815A", "5", "000004146", "42295687300000645397012340012345670000041462", "42297.01232 40012.345670 00000.414623 5 68730000064539", 2016, 8, 1)]
        [TestCase(292.21, "444", "BB834A", "6", "000004448", "42296690500000292217012340012345670000044482", "42297.01232 40012.345670 00000.444828 6 69050000029221", 2016, 9, 2)]
        [TestCase(838, "562", "BB933A", "7", "000005622", "42297702600000838007012340012345670000056222", "42297.01232 40012.345670 00000.562223 7 70260000083800", 2017, 1, 1)]
        [TestCase(2927.11, "445", "BB874A", "8", "000004456", "42298690500002927117012340012345670000044562", "42297.01232 40012.345670 00000.445627 8 69050000292711", 2016, 9, 2)]
        [TestCase(2921.27, "443", "BB833A", "9", "000004430", "42299690500002921277012340012345670000044302", "42297.01232 40012.345670 00000.443028 9 69050000292127", 2016, 9, 2)]
        [TestCase(293.21, "468", "BB856A", "1", "000004685", "42291693500000293217012340012345670000046852", "42297.01232 40012.345670 00000.468520 1 69350000029321", 2016, 10, 2)]
        [TestCase(276, "561", "BB932A", "1", "000005614", "42291702600000276007012340012345670000056142", "42297.01232 40012.345670 00000.561423 1 70260000027600", 2017, 1, 1)]

        public void Safra_2_BoletoOK(decimal valorTitulo, string nossoNumero, string numeroDocumento, string digitoVerificador, string nossoNumeroFormatado, string codigoDeBarras, string linhaDigitavel, params int[] anoMesDia)
        {
            //Ambiente
            var boleto = new Boleto(_banco)
            {
                DataVencimento = new DateTime(anoMesDia[0], anoMesDia[1], anoMesDia[2]),
                ValorTitulo = valorTitulo,
                NossoNumero = nossoNumero,
                NumeroDocumento = numeroDocumento,
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Utils.GerarSacado()
            };

            //Ação
            boleto.ValidarDados();


            //Assertivas
            const string LOCAL_DE_PAGAMENTO_ESPERADO = "Pagável em qualquer Banco do Sistema de Compensação";
            StringAssert.AreEqualIgnoringCase(LOCAL_DE_PAGAMENTO_ESPERADO, boleto.Banco.Cedente.ContaBancaria.LocalPagamento, $"Local de pagamento diferente de {LOCAL_DE_PAGAMENTO_ESPERADO}");
            Assert.That(boleto.CodigoBarra.DigitoVerificador, Is.EqualTo(digitoVerificador), $"Dígito Verificador diferente de {digitoVerificador}");
            Assert.That(boleto.NossoNumeroFormatado, Is.EqualTo(nossoNumeroFormatado), "Nosso número inválido");
            Assert.That(boleto.CodigoBarra.CodigoDeBarras, Is.EqualTo(codigoDeBarras), "Código de Barra inválido");
            Assert.That(boleto.CodigoBarra.LinhaDigitavel, Is.EqualTo(linhaDigitavel), "Linha digitável inválida");
        }

        // Agência com 5 posições sem DV no campo livre (manual Safra 240, p.8 e p.44-47); valores calculados à parte.
        [TestCase("160", "7", "42291162600000141507001600058019570000045372", "42297.00168 00058.019571 00000.453720 1 16260000014150")]
        [TestCase("01600", "", "42294162600000141507016000058019570000045372", "42297.01604 00058.019571 00000.453720 4 16260000014150")]
        public void Safra_2_CodigoDeBarras_UsaAgenciaDe5PosicoesSemDigito(string agencia, string digitoAgencia, string codigoDeBarras, string linhaDigitavel)
        {
            var banco = Banco.Instancia(Bancos.Safra);
            banco.Cedente = Utils.GerarCedente("", "", "", new ContaBancaria
            {
                Agencia = agencia,
                DigitoAgencia = digitoAgencia,
                Conta = "580195",
                DigitoConta = "7",
                CarteiraPadrao = "2",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaVinculada,
                TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
            });
            banco.FormataCedente();
            var boleto = new Boleto(banco)
            {
                DataVencimento = new DateTime(2026, 11, 10),
                ValorTitulo = 141.50M,
                NossoNumero = "453",
                NumeroDocumento = "BB943A",
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Utils.GerarSacado()
            };

            boleto.ValidarDados();

            Assert.That(boleto.CodigoBarra.CodigoDeBarras, Is.EqualTo(codigoDeBarras), "Código de Barra inválido");
            Assert.That(boleto.CodigoBarra.LinhaDigitavel, Is.EqualTo(linhaDigitavel), "Linha digitável inválida");
        }
    }
}
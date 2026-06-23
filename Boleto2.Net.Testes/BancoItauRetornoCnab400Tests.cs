using System;
using System.Text;
using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("Itau Retorno CNAB400")]
    public class BancoItauRetornoCnab400Tests
    {
        private const string Zeros = "0000000000000"; // 13-char zero-filled numeric field
        private const string Blanks = "             "; // 13 spaces, as sent by Itau for empty optional values

        // Builds a 400-char Itau CNAB 400 return detail record (registro tipo 1).
        // The 9 decimal value fields are placed at the exact offsets read by the parser;
        // every other position is zero-filled so helper lookups (ocorrencia, especie, dates) stay valid.
        private static string BuildDetailLine(
            string valorTitulo = Zeros,
            string valorTarifas = Zeros,
            string valorOutrasDespesas = Zeros,
            string valorIOF = Zeros,
            string valorAbatimento = Zeros,
            string valorDesconto = Zeros,
            string valorPagoCredito = Zeros,
            string valorJurosDia = Zeros,
            string valorOutrosCreditos = Zeros)
        {
            var sb = new StringBuilder(new string('0', 400));

            void Put(int start, string value)
            {
                for (var i = 0; i < value.Length; i++)
                    sb[start + i] = value[i];
            }

            Put(0, "1"); // tipo de registro = detalhe
            Put(152, valorTitulo);
            Put(175, valorTarifas);
            Put(188, valorOutrasDespesas);
            Put(214, valorIOF);
            Put(227, valorAbatimento);
            Put(240, valorDesconto);
            Put(253, valorPagoCredito);
            Put(266, valorJurosDia);
            Put(279, valorOutrosCreditos);

            return sb.ToString();
        }

        private static Boleto ParseDetail(string registro)
        {
            var banco = Banco.Instancia(Bancos.Itau);
            // Mirror ArquivoRetorno: build with ignorarCarteira = true so a Cedente is not required.
            var boleto = new Boleto(banco, true);
            banco.LerDetalheRetornoCNAB400Segmento1(ref boleto, registro);
            return boleto;
        }

        [Test]
        public void LerDetalhe_ComValorOutrasDespesasEmBranco_NaoLancaEAssumeZero()
        {
            // Reproduz N3-6120: Itau envia ValorOutrasDespesas (pos 189-201) em branco.
            var registro = BuildDetailLine(valorOutrasDespesas: Blanks);

            Boleto boleto = null;
            Assert.DoesNotThrow(() => boleto = ParseDetail(registro));
            Assert.AreEqual(0m, boleto.ValorOutrasDespesas);
        }

        [Test]
        public void LerDetalhe_ComTodosOsCamposDecimaisEmBranco_NaoLancaEAssumeZero()
        {
            var registro = BuildDetailLine(
                valorTitulo: Blanks,
                valorTarifas: Blanks,
                valorOutrasDespesas: Blanks,
                valorIOF: Blanks,
                valorAbatimento: Blanks,
                valorDesconto: Blanks,
                valorPagoCredito: Blanks,
                valorJurosDia: Blanks,
                valorOutrosCreditos: Blanks);

            Boleto boleto = null;
            Assert.DoesNotThrow(() => boleto = ParseDetail(registro));

            Assert.AreEqual(0m, boleto.ValorTitulo);
            Assert.AreEqual(0m, boleto.ValorTarifas);
            Assert.AreEqual(0m, boleto.ValorOutrasDespesas);
            Assert.AreEqual(0m, boleto.ValorIOF);
            Assert.AreEqual(0m, boleto.ValorAbatimento);
            Assert.AreEqual(0m, boleto.ValorDesconto);
            Assert.AreEqual(0m, boleto.ValorPagoCredito);
            Assert.AreEqual(0m, boleto.ValorJurosDia);
            Assert.AreEqual(0m, boleto.ValorOutrosCreditos);
        }

        [Test]
        public void LerDetalhe_ComCamposZerados_RetornaZero()
        {
            var registro = BuildDetailLine(); // all zeros

            var boleto = ParseDetail(registro);

            Assert.AreEqual(0m, boleto.ValorTitulo);
            Assert.AreEqual(0m, boleto.ValorOutrasDespesas);
            Assert.AreEqual(0m, boleto.ValorOutrosCreditos);
        }

        [Test]
        public void LerDetalhe_ComValoresPreenchidos_DividePorCemEMapeiaCadaCampo()
        {
            // Distinct values per field guarantee correct offset->property mapping (no off-by-one).
            var registro = BuildDetailLine(
                valorTitulo: "0000000150000",        // 1500.00
                valorTarifas: "0000000000250",       // 2.50
                valorOutrasDespesas: "0000000000099", // 0.99
                valorIOF: "0000000000125",           // 1.25
                valorAbatimento: "0000000001000",    // 10.00
                valorDesconto: "0000000000500",      // 5.00
                valorPagoCredito: "0000000160075",   // 1600.75
                valorJurosDia: "0000000000033",      // 0.33
                valorOutrosCreditos: "0000000000077"); // 0.77

            var boleto = ParseDetail(registro);

            Assert.AreEqual(1500.00m, boleto.ValorTitulo);
            Assert.AreEqual(2.50m, boleto.ValorTarifas);
            Assert.AreEqual(0.99m, boleto.ValorOutrasDespesas);
            Assert.AreEqual(1.25m, boleto.ValorIOF);
            Assert.AreEqual(10.00m, boleto.ValorAbatimento);
            Assert.AreEqual(5.00m, boleto.ValorDesconto);
            Assert.AreEqual(1600.75m, boleto.ValorPagoCredito);
            Assert.AreEqual(0.33m, boleto.ValorJurosDia);
            Assert.AreEqual(0.77m, boleto.ValorOutrosCreditos);
        }

        // Each decimal field individually blank must not break the others -> all 9 call sites are protected.
        [TestCase(152, TestName = "ValorTitulo em branco")]
        [TestCase(175, TestName = "ValorTarifas em branco")]
        [TestCase(188, TestName = "ValorOutrasDespesas em branco")]
        [TestCase(214, TestName = "ValorIOF em branco")]
        [TestCase(227, TestName = "ValorAbatimento em branco")]
        [TestCase(240, TestName = "ValorDesconto em branco")]
        [TestCase(253, TestName = "ValorPagoCredito em branco")]
        [TestCase(266, TestName = "ValorJurosDia em branco")]
        [TestCase(279, TestName = "ValorOutrosCreditos em branco")]
        public void LerDetalhe_ComUmCampoDecimalEmBranco_NaoLanca(int offsetCampoEmBranco)
        {
            var sb = new StringBuilder(BuildDetailLine(
                valorTitulo: "0000000150000",
                valorTarifas: "0000000000250",
                valorOutrasDespesas: "0000000000099",
                valorIOF: "0000000000125",
                valorAbatimento: "0000000001000",
                valorDesconto: "0000000000500",
                valorPagoCredito: "0000000160075",
                valorJurosDia: "0000000000033",
                valorOutrosCreditos: "0000000000077"));

            for (var i = 0; i < 13; i++)
                sb[offsetCampoEmBranco + i] = ' ';

            Assert.DoesNotThrow(() => ParseDetail(sb.ToString()));
        }
    }
}

using NUnit.Framework;

namespace Boleto2.Net.Testes
{
    /// <summary>
    /// PAY-2324: sanity tests para Boleto2Net.Utils.FormataCPFCNPJ.
    /// Helper position-based via Substring (não interpreta valor numericamente).
    /// Vetor SERPRO oficial CNPJ alfa: AB12CD34EFGH83 (DV1=8, DV2=3).
    /// </summary>
    [TestFixture]
    public class CnpjAlfanumericoFormattingTests
    {
        [Test]
        public void FormataCPFCNPJ_CpfNumerico_AplicaMascaraXxxXxxXxxXx()
        {
            var result = Boleto2Net.Utils.FormataCPFCNPJ("12345678901");
            Assert.AreEqual("123.456.789-01", result);
        }

        [Test]
        public void FormataCPFCNPJ_CnpjNumerico_AplicaMascaraXxXxxXxxXxxxXx()
        {
            var result = Boleto2Net.Utils.FormataCPFCNPJ("12345678000195");
            Assert.AreEqual("12.345.678/0001-95", result);
        }

        [Test]
        public void FormataCPFCNPJ_CnpjAlfanumerico_AplicaMascaraPositionBased()
        {
            var result = Boleto2Net.Utils.FormataCPFCNPJ("AB12CD34EFGH83");
            Assert.AreEqual("AB.12C.D34/EFGH-83", result);
        }

        [Test]
        public void FormataCPFCNPJ_LengthInvalido_LancaExcecao()
        {
            // Length 10 — entre CPF (11) e CNPJ (14) e fora dos buckets.
            Assert.Throws<System.Exception>(() => Boleto2Net.Utils.FormataCPFCNPJ("1234567890"));

            // Length 13 — fora dos buckets.
            Assert.Throws<System.Exception>(() => Boleto2Net.Utils.FormataCPFCNPJ("1234567890123"));

            // Length 15 — fora dos buckets.
            Assert.Throws<System.Exception>(() => Boleto2Net.Utils.FormataCPFCNPJ("123456789012345"));
        }

        [Test]
        public void FormataCPFCNPJ_DvComLetras_FormataPositionalmente()
        {
            // Helper e position-based — nao valida DV. Documenta comportamento atual:
            // input com letras nas posicoes de DV e formatado mesmo assim.
            // Validacao de DV e responsabilidade da camada superior (PAY-2097 ValidaCNPJAlphanumeric).
            var result = Boleto2Net.Utils.FormataCPFCNPJ("AB12CD34EFGHXY");
            Assert.AreEqual("AB.12C.D34/EFGH-XY", result);
        }
    }
}

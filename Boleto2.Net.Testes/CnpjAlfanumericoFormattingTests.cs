using NUnit.Framework;

namespace Boleto2.Net.Testes
{
    /// <summary>
    /// PAY-2324: sanity tests para Boleto2Net.Utils.FormataCPFCPPJ.
    /// Helper position-based via Substring (não interpreta valor numericamente).
    /// Vetor SERPRO oficial CNPJ alfa: AB12CD34EFGH83 (DV1=8, DV2=3).
    /// </summary>
    [TestFixture]
    public class CnpjAlfanumericoFormattingTests
    {
        [Test]
        public void FormataCPFCPPJ_CpfNumerico_AplicaMascaraXxxXxxXxxXx()
        {
            var result = Boleto2Net.Utils.FormataCPFCPPJ("12345678901");
            Assert.AreEqual("123.456.789-01", result);
        }

        [Test]
        public void FormataCPFCPPJ_CnpjNumerico_AplicaMascaraXxXxxXxxXxxxXx()
        {
            var result = Boleto2Net.Utils.FormataCPFCPPJ("12345678000195");
            Assert.AreEqual("12.345.678/0001-95", result);
        }

        [Test]
        public void FormataCPFCPPJ_CnpjAlfanumerico_AplicaMascaraPositionBased()
        {
            var result = Boleto2Net.Utils.FormataCPFCPPJ("AB12CD34EFGH83");
            Assert.AreEqual("AB.12C.D34/EFGH-83", result);
        }
    }
}

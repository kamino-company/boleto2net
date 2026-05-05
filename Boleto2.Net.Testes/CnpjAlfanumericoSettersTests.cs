using NUnit.Framework;

namespace Boleto2.Net.Testes
{
    /// <summary>
    /// PAY-2324: testes do setter CPFCNPJ em Cedente e Sacado com flag
    /// CnabSettings.SuportarCnpjAlfanumerico ON e OFF.
    /// Vetor SERPRO: AB12CD34EFGH83 (DV1=8, DV2=3).
    /// </summary>
    [TestFixture]
    public class CnpjAlfanumericoSettersTests
    {
        [TearDown]
        public void Cleanup()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
        }

        // === Cedente — flag ON ===

        [Test]
        public void Cedente_FlagOn_AceitaAlfaValido()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "AB12CD34EFGH83" };
            Assert.AreEqual("AB12CD34EFGH83", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOn_NormalizaLowercase()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "ab12cd34efgh83" };
            Assert.AreEqual("AB12CD34EFGH83", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOn_TrimWhitespace()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "  AB12CD34EFGH83  " };
            Assert.AreEqual("AB12CD34EFGH83", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOn_RemoveSeparadores()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "AB.12C.D34/EFGH-83" };
            Assert.AreEqual("AB12CD34EFGH83", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOn_AceitaCnpjNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "12345678000195" };
            Assert.AreEqual("12345678000195", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOn_AceitaCpfNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "12345678901" };
            Assert.AreEqual("12345678901", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOn_RejeitaCpfComLetras()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Cedente { CPFCNPJ = "ABC45678901" });
        }

        [Test]
        public void Cedente_FlagOn_RejeitaCnpjComCaractereEspecial()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Cedente { CPFCNPJ = "AB12CD34EFG@83" });
        }

        [Test]
        public void Cedente_FlagOn_RejeitaDvComLetras()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            // DV (posicoes 13-14) deve ser numerico
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Cedente { CPFCNPJ = "AB12CD34EFGHAB" });
        }

        [Test]
        public void Cedente_FlagOn_RejeitaLengthInvalido()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Cedente { CPFCNPJ = "AB12" });
        }

        // === Cedente — flag OFF (regressao) ===

        [Test]
        public void Cedente_FlagOff_RejeitaAlfa()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Cedente { CPFCNPJ = "AB12CD34EFGH83" });
        }

        [Test]
        public void Cedente_FlagOff_AceitaCnpjNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "12345678000195" };
            Assert.AreEqual("12345678000195", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOff_AceitaCpfNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "12345678901" };
            Assert.AreEqual("12345678901", cedente.CPFCNPJ);
        }

        [Test]
        public void Cedente_FlagOff_AceitaCnpjComMascara()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            var cedente = new Boleto2Net.Cedente { CPFCNPJ = "12.345.678/0001-95" };
            Assert.AreEqual("12345678000195", cedente.CPFCNPJ);
        }

        // === Sacado — paridade com Cedente ===

        [Test]
        public void Sacado_FlagOn_AceitaAlfaValido()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "AB12CD34EFGH83" };
            Assert.AreEqual("AB12CD34EFGH83", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOn_NormalizaLowercase()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "ab12cd34efgh83" };
            Assert.AreEqual("AB12CD34EFGH83", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOn_TrimWhitespace()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "  AB12CD34EFGH83  " };
            Assert.AreEqual("AB12CD34EFGH83", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOn_RemoveSeparadores()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "AB.12C.D34/EFGH-83" };
            Assert.AreEqual("AB12CD34EFGH83", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOn_AceitaCnpjNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "12345678000195" };
            Assert.AreEqual("12345678000195", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOn_AceitaCpfNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "12345678901" };
            Assert.AreEqual("12345678901", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOn_RejeitaCpfComLetras()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Sacado { CPFCNPJ = "ABC45678901" });
        }

        [Test]
        public void Sacado_FlagOn_RejeitaCnpjComCaractereEspecial()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Sacado { CPFCNPJ = "AB12CD34EFG@83" });
        }

        [Test]
        public void Sacado_FlagOn_RejeitaDvComLetras()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Sacado { CPFCNPJ = "AB12CD34EFGHAB" });
        }

        [Test]
        public void Sacado_FlagOn_RejeitaLengthInvalido()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Sacado { CPFCNPJ = "AB12" });
        }

        [Test]
        public void Sacado_FlagOff_RejeitaAlfa()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            Assert.Throws<System.ArgumentException>(() =>
                new Boleto2Net.Sacado { CPFCNPJ = "AB12CD34EFGH83" });
        }

        [Test]
        public void Sacado_FlagOff_AceitaCnpjNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "12345678000195" };
            Assert.AreEqual("12345678000195", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOff_AceitaCpfNumerico()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "12345678901" };
            Assert.AreEqual("12345678901", sacado.CPFCNPJ);
        }

        [Test]
        public void Sacado_FlagOff_AceitaCnpjComMascara()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = false;
            var sacado = new Boleto2Net.Sacado { CPFCNPJ = "12.345.678/0001-95" };
            Assert.AreEqual("12345678000195", sacado.CPFCNPJ);
        }

        // === Avalista ===

        [Test]
        public void Avalista_FlagOn_AceitaAlfaTransitivamente()
        {
            Boleto2Net.CnabSettings.SuportarCnpjAlfanumerico = true;
            var boleto = new Boleto2Net.Boleto { Avalista = new Boleto2Net.Sacado { CPFCNPJ = "AB12CD34EFGH83" } };
            Assert.AreEqual("AB12CD34EFGH83", boleto.Avalista.CPFCNPJ);
        }
    }
}

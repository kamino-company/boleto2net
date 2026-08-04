using System;
using System.Text;
using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("Santander Retorno CNAB240")]
    public class BancoSantanderRetornoCnab240Tests
    {
        private const int TamanhoRegistro = 240;

        // Field offsets are 0-based Substring indexes, derived from BancoSantander.LerDetalheRetornoCNAB240SegmentoT/U
        // and cross-checked against the Santander CNAB 240 manual (1-based positions in the comments).
        private const int PosOcorrencia = 15;          // 016-017
        private const int PosNossoNumero = 40;         // 041-052 (parser reads 12 of the 13-position field)
        private const int PosNossoNumeroDV = 52;       // 053
        private const int PosCarteira = 53;            // 054
        private const int PosNumeroDocumento = 54;     // 055-069
        private const int PosDataVencimento = 69;      // 070-077
        private const int PosValorTitulo = 77;         // 078-092
        private const int PosBancoCobrador = 92;       // 093-095
        private const int PosControleParticipante = 100; // 101-125
        // 129-143 is a 15-position field, so it starts at 0-based index 128. The parser reads
        // Substring(129, 14) - the LAST 14 positions - because Sacado.CPFCNPJ only accepts 11 or 14
        // digits, so the leading pad position must be dropped.
        private const int PosInscricaoPagador = 128;   // 129-143
        private const int PosNomePagador = 143;        // 144-183
        private const int PosValorTarifa = 193;        // 194-208
        private const int PosOcorrenciaAuxiliar = 208; // 209-218

        // Segment U offsets
        private const int PosUJurosMulta = 17;         // 018-032
        private const int PosUDesconto = 32;           // 033-047
        private const int PosUAbatimento = 47;         // 048-062
        private const int PosUIof = 62;                // 063-077
        private const int PosUValorPago = 77;          // 078-092
        private const int PosUValorLiquido = 92;       // 093-107
        private const int PosUOutrasDespesas = 107;    // 108-122
        private const int PosUOutrosCreditos = 122;    // 123-137
        private const int PosUDataOcorrencia = 137;    // 138-145
        private const int PosUDataCredito = 145;       // 146-153

        private static StringBuilder NovoRegistro()
        {
            return new StringBuilder(new string('0', TamanhoRegistro));
        }

        private static void Put(StringBuilder sb, int start, string value)
        {
            for (var i = 0; i < value.Length; i++)
                sb[start + i] = value[i];
        }

        private static void Blank(StringBuilder sb, int start, int length)
        {
            for (var i = 0; i < length; i++)
                sb[start + i] = ' ';
        }

        // Builds a 240-char segment T detail record using the values observed in a real Santander
        // settlement return file. Defaults are the happy-path record; each argument overrides one field.
        private static StringBuilder BuildSegmentoT(
            string ocorrencia = "06",
            string nossoNumero13 = "0000000015156",
            string carteira = "2",
            string numeroDocumento = "1515           ",
            string dataVencimento = "14072026",
            string valorTitulo = "000000000013800",
            string bancoCobrador = "341",
            string inscricaoPagador = "060258985000181",
            string nomePagador = "PAGADOR TESTE LTDA                      ",
            string valorTarifa = "000000000000000",
            string ocorrenciaAuxiliar = "0400000000")
        {
            var sb = NovoRegistro();

            Put(sb, 0, "033");   // 001-003 banco
            Put(sb, 7, "3");     // 008 tipo de registro
            Put(sb, 13, "T");    // 014 codigo do segmento

            Put(sb, PosOcorrencia, ocorrencia);
            Put(sb, PosNossoNumero, nossoNumero13); // single 13-position field; parser splits it 12 + 1
            Put(sb, PosCarteira, carteira);
            Put(sb, PosNumeroDocumento, numeroDocumento);
            Put(sb, PosDataVencimento, dataVencimento);
            Put(sb, PosValorTitulo, valorTitulo);
            Put(sb, PosBancoCobrador, bancoCobrador);
            Put(sb, PosInscricaoPagador, inscricaoPagador);
            Put(sb, PosNomePagador, nomePagador);
            Put(sb, PosValorTarifa, valorTarifa);
            Put(sb, PosOcorrenciaAuxiliar, ocorrenciaAuxiliar);

            return sb;
        }

        private static StringBuilder BuildSegmentoU(
            string jurosMulta = "000000000000000",
            string desconto = "000000000000000",
            string abatimento = "000000000000000",
            string iof = "000000000000000",
            string valorPago = "000000000013800",
            string valorLiquido = "000000000013800",
            string outrasDespesas = "000000000000000",
            string outrosCreditos = "000000000000000",
            string dataOcorrencia = "13072026",
            string dataCredito = "15072026")
        {
            var sb = NovoRegistro();

            Put(sb, 0, "033");
            Put(sb, 7, "3");
            Put(sb, 13, "U");

            Put(sb, PosUJurosMulta, jurosMulta);
            Put(sb, PosUDesconto, desconto);
            Put(sb, PosUAbatimento, abatimento);
            Put(sb, PosUIof, iof);
            Put(sb, PosUValorPago, valorPago);
            Put(sb, PosUValorLiquido, valorLiquido);
            Put(sb, PosUOutrasDespesas, outrasDespesas);
            Put(sb, PosUOutrosCreditos, outrosCreditos);
            Put(sb, PosUDataOcorrencia, dataOcorrencia);
            Put(sb, PosUDataCredito, dataCredito);

            return sb;
        }

        private static Boleto ParseSegmentoT(string registro)
        {
            var banco = Banco.Instancia(Bancos.Santander);
            // Mirror ArquivoRetorno: build with ignorarCarteira = true so a Cedente is not required.
            var boleto = new Boleto(banco, true);
            banco.LerDetalheRetornoCNAB240SegmentoT(ref boleto, registro);
            return boleto;
        }

        private static Boleto ParseSegmentoU(string registro)
        {
            var banco = Banco.Instancia(Bancos.Santander);
            var boleto = new Boleto(banco, true);
            banco.LerDetalheRetornoCNAB240SegmentoU(ref boleto, registro);
            return boleto;
        }

        #region Segmento T

        [Test]
        public void LerDetalheSegmentoT_ComOcorrencia06Liquidacao_MapeiaTodosOsCampos()
        {
            var registro = BuildSegmentoT().ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual("06", boleto.CodigoOcorrencia);
            Assert.AreEqual("Liquidação", boleto.DescricaoOcorrencia);
            Assert.AreEqual("000000001515", boleto.NossoNumero);
            Assert.AreEqual("6", boleto.NossoNumeroDV);
            Assert.AreEqual("000000001515-6", boleto.NossoNumeroFormatado);
            Assert.AreEqual("2", boleto.Carteira);
            Assert.AreEqual(TipoCarteira.CarteiraCobrancaSimples, boleto.TipoCarteira);
            Assert.AreEqual("1515           ", boleto.NumeroDocumento);
            Assert.AreEqual(new DateTime(2026, 7, 14), boleto.DataVencimento);
            Assert.AreEqual(138.00m, boleto.ValorTitulo);
            Assert.AreEqual(0m, boleto.ValorTarifas);
            Assert.AreEqual("341", boleto.BancoCobradorRecebedor);
            Assert.AreEqual("0400000000", boleto.CodigoOcorrenciaAuxiliar);
            Assert.AreEqual("60258985000181", boleto.Sacado.CPFCNPJ);
            Assert.AreEqual("PAGADOR TESTE LTDA", boleto.Sacado.Nome.Trim());
        }

        // The manual describes 041-053 as ONE 13-position field; the parser splits it into
        // NossoNumero (12) + NossoNumeroDV (1). A single-position drift here would silently
        // corrupt document matching for every record in the file, so the split is pinned by
        // asserting that the NEXT two fields are not shifted: distinct sentinel values are
        // written to carteira (054) and numero do documento (055-069).
        [Test]
        public void LerDetalheSegmentoT_ComNossoNumeroDe13Posicoes_DivideDozeMaisDvSemDeslocarCamposSeguintes()
        {
            var registro = BuildSegmentoT(
                nossoNumero13: "1234567890123",
                carteira: "4",
                numeroDocumento: "ABCDEFGHIJKLMNO").ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual("123456789012", boleto.NossoNumero, "NossoNumero must be the first 12 of the 13 positions.");
            Assert.AreEqual("3", boleto.NossoNumeroDV, "DV must be the 13th position.");
            Assert.AreEqual("4", boleto.Carteira, "Carteira (054) must not absorb the NossoNumero DV.");
            Assert.AreEqual("ABCDEFGHIJKLMNO", boleto.NumeroDocumento, "Numero do documento (055-069) must not be shifted.");
            Assert.AreEqual(TipoCarteira.CarteiraCobrancaDescontada, boleto.TipoCarteira);
        }

        // The payer document field is 15 positions (129-143) but Sacado.CPFCNPJ only accepts 11 or 14
        // digits, so the parser must drop the leading pad position and keep the trailing 14. Reading
        // the field one position to the left would yield "06025898500018" - a valid-length but wrong
        // document, which would silently mismatch the payer instead of failing loudly.
        [Test]
        public void LerDetalheSegmentoT_ComInscricaoPagadorDe15Posicoes_DescartaPrimeiraPosicaoEMantemQuatorze()
        {
            var registro = BuildSegmentoT(
                inscricaoPagador: "060258985000181",
                nomePagador: "PAGADOR OFFSET                          ").ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual("60258985000181", boleto.Sacado.CPFCNPJ);
            Assert.AreEqual("PAGADOR OFFSET", boleto.Sacado.Nome.Trim(), "Nome do pagador (144-183) must not be shifted.");
        }

        // Characterization test: a CPF payer is NOT recognized as a natural person on the return path.
        // The 11-digit CPF arrives right-aligned in the 15-position field, the parser always takes a
        // fixed 14-character slice, and CpfCnpjValidator only accepts exactly 11 or 14 digits without
        // trimming leading zeros. The value is therefore stored as 14 digits, and TipoCPFCNPJ - which
        // classifies with "CPFCNPJ.Length <= 11 ? F : J" - reports "J" (legal entity) for an individual.
        // Pinning current behavior; flip these assertions if the parser starts trimming the pad.
        [Test]
        public void LerDetalheSegmentoT_ComCpfDePagador_MantemQuatorzeDigitosEClassificaComoJuridico()
        {
            var registro = BuildSegmentoT(inscricaoPagador: "000012345678909").ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual("00012345678909", boleto.Sacado.CPFCNPJ, "Parser keeps the fixed 14-char slice; the leading zero is not trimmed.");
            Assert.AreEqual("J", boleto.Sacado.TipoCPFCNPJ("A"), "Known limitation: a CPF payer is classified as legal entity because the stored value has 14 characters.");
        }

        // An 11-digit CPF only survives the setter when it reaches it already trimmed, which the
        // return path never does. Asserting it here documents that the validator itself supports CPF -
        // the loss of that support is in the fixed-width read above, not in CpfCnpjValidator.
        [Test]
        public void SacadoCpfCnpj_ComCpfDeOnzeDigitos_AceitaEClassificaComoFisico()
        {
            var sacado = new Sacado { CPFCNPJ = "12345678909" };

            Assert.AreEqual("12345678909", sacado.CPFCNPJ);
            Assert.AreEqual("F", sacado.TipoCPFCNPJ("A"));
        }

        [TestCase("3", TipoCarteira.CarteiraCobrancaCaucionada, TestName = "Carteira 3 => Caucionada")]
        [TestCase("6", TipoCarteira.CarteiraCobrancaCaucionada, TestName = "Carteira 6 => Caucionada")]
        [TestCase("4", TipoCarteira.CarteiraCobrancaDescontada, TestName = "Carteira 4 => Descontada")]
        [TestCase("1", TipoCarteira.CarteiraCobrancaSimples, TestName = "Carteira 1 => Simples")]
        [TestCase("2", TipoCarteira.CarteiraCobrancaSimples, TestName = "Carteira 2 => Simples")]
        public void LerDetalheSegmentoT_ComCarteira_MapeiaTipoCarteira(string carteira, TipoCarteira esperado)
        {
            var registro = BuildSegmentoT(carteira: carteira).ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual(carteira, boleto.Carteira);
            Assert.AreEqual(esperado, boleto.TipoCarteira);
        }

        // Occurrence codes that appear in real Santander files but that the ingestion flow does not
        // act on. They must still parse and expose the code, so downstream logic can decide to skip them.
        [TestCase("02", "Entrada Confirmada", TestName = "Ocorrencia 02 - Entrada Confirmada")]
        [TestCase("03", "Entrada Rejeitada", TestName = "Ocorrencia 03 - Entrada Rejeitada")]
        public void LerDetalheSegmentoT_ComOcorrenciaNaoTratada_ExpoeCodigoEDescricao(string codigo, string descricaoEsperada)
        {
            var registro = BuildSegmentoT(ocorrencia: codigo).ToString();

            Boleto boleto = null;
            Assert.DoesNotThrow(() => boleto = ParseSegmentoT(registro));
            Assert.AreEqual(codigo, boleto.CodigoOcorrencia);
            Assert.AreEqual(descricaoEsperada, boleto.DescricaoOcorrencia);
            Assert.AreEqual("000000001515", boleto.NossoNumero);
        }

        // Codes the manual documents as settlements but that the platform's settlement whitelist
        // does not include. This asserts parser-level reading ONLY - no write-off behaviour is implied.
        // Code 93 is not mapped by Cnab.OcorrenciaCnab240, so its description comes back empty;
        // that is the current behaviour being pinned, not an endorsement of it.
        [TestCase("17", "Liquidação Após Baixa ou Liquidação Título Não Registrado", TestName = "Ocorrencia 17 - Liquidacao apos baixa")]
        [TestCase("93", "", TestName = "Ocorrencia 93 - nao mapeada em Cnab.OcorrenciaCnab240")]
        public void LerDetalheSegmentoT_ComOcorrenciaDeLiquidacaoForaDaWhitelist_LeCodigoCorretamente(string codigo, string descricaoEsperada)
        {
            var registro = BuildSegmentoT(ocorrencia: codigo).ToString();

            Boleto boleto = null;
            Assert.DoesNotThrow(() => boleto = ParseSegmentoT(registro));
            Assert.AreEqual(codigo, boleto.CodigoOcorrencia);
            Assert.AreEqual(descricaoEsperada, boleto.DescricaoOcorrencia);
            Assert.AreEqual(138.00m, boleto.ValorTitulo);
        }

        [Test]
        public void LerDetalheSegmentoT_ComCamposNumericosZerados_RetornaZero()
        {
            var registro = BuildSegmentoT(
                valorTitulo: "000000000000000",
                valorTarifa: "000000000000000").ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual(0m, boleto.ValorTitulo);
            Assert.AreEqual(0m, boleto.ValorTarifas);
        }

        [Test]
        public void LerDetalheSegmentoT_ComValoresDistintos_DividePorCemEMapeiaCadaCampo()
        {
            // Distinct values per field guarantee correct offset->property mapping (no off-by-one).
            var registro = BuildSegmentoT(
                valorTitulo: "000000000150000", // 1500.00
                valorTarifa: "000000000000250") // 2.50
                .ToString();

            var boleto = ParseSegmentoT(registro);

            Assert.AreEqual(1500.00m, boleto.ValorTitulo);
            Assert.AreEqual(2.50m, boleto.ValorTarifas);
        }

        [Test]
        public void LerDetalheSegmentoT_ComDataVencimentoEmBranco_NaoLancaERetornaDataMinima()
        {
            // Utils.ToDateTime/ToInt32 swallow conversion errors, so a blank date degrades to
            // DateTime.MinValue instead of throwing. Pinning this so the fallback is not lost.
            var sb = BuildSegmentoT();
            Blank(sb, PosDataVencimento, 8);

            Boleto boleto = null;
            Assert.DoesNotThrow(() => boleto = ParseSegmentoT(sb.ToString()));
            Assert.AreEqual(DateTime.MinValue, boleto.DataVencimento);
        }

        // Characterization tests: unlike the Itau CNAB 400 parser (which routes every decimal field
        // through a SafeToDecimal guard added after a production crash), the Santander segment T
        // parser calls Convert.ToDecimal directly. A blank numeric field therefore throws.
        // These tests document the CURRENT behaviour. If the parser is hardened with a blank-safe
        // conversion, they must be flipped to assert 0m.
        [TestCase(PosValorTitulo, 15, TestName = "ValorTitulo em branco lanca")]
        [TestCase(PosValorTarifa, 15, TestName = "ValorTarifa em branco lanca")]
        public void LerDetalheSegmentoT_ComCampoDecimalEmBranco_LancaExcecao(int offset, int tamanho)
        {
            var sb = BuildSegmentoT();
            Blank(sb, offset, tamanho);

            var ex = Assert.Throws<Exception>(() => ParseSegmentoT(sb.ToString()));
            Assert.AreEqual("Erro ao ler detalhe do arquivo de RETORNO / CNAB 240 / T.", ex.Message);
        }

        #endregion

        #region Segmento U

        [Test]
        public void LerDetalheSegmentoU_ComLiquidacao_MapeiaValoresEDatas()
        {
            var registro = BuildSegmentoU().ToString();

            var boleto = ParseSegmentoU(registro);

            Assert.AreEqual(138.00m, boleto.ValorPago);
            Assert.AreEqual(138.00m, boleto.ValorPagoCredito);
            Assert.AreEqual(0m, boleto.ValorJurosDia);
            Assert.AreEqual(0m, boleto.ValorDesconto);
            Assert.AreEqual(0m, boleto.ValorAbatimento);
            Assert.AreEqual(0m, boleto.ValorIOF);
            Assert.AreEqual(0m, boleto.ValorOutrasDespesas);
            Assert.AreEqual(0m, boleto.ValorOutrosCreditos);
            // The parser stores the "data da ocorrencia" (138-145) into DataProcessamento.
            Assert.AreEqual(new DateTime(2026, 7, 13), boleto.DataProcessamento);
            Assert.AreEqual(new DateTime(2026, 7, 15), boleto.DataCredito);
        }

        [Test]
        public void LerDetalheSegmentoU_ComValoresDistintos_MapeiaCadaCampoNoOffsetCorreto()
        {
            // Distinct values per field prove the 8 consecutive 15-position decimals are not shifted.
            var registro = BuildSegmentoU(
                jurosMulta: "000000000000101",     // 1.01
                desconto: "000000000000202",       // 2.02
                abatimento: "000000000000303",     // 3.03
                iof: "000000000000404",            // 4.04
                valorPago: "000000000050505",      // 505.05
                valorLiquido: "000000000060606",   // 606.06
                outrasDespesas: "000000000000707", // 7.07
                outrosCreditos: "000000000000808") // 8.08
                .ToString();

            var boleto = ParseSegmentoU(registro);

            Assert.AreEqual(1.01m, boleto.ValorJurosDia);
            Assert.AreEqual(2.02m, boleto.ValorDesconto);
            Assert.AreEqual(3.03m, boleto.ValorAbatimento);
            Assert.AreEqual(4.04m, boleto.ValorIOF);
            Assert.AreEqual(505.05m, boleto.ValorPago);
            Assert.AreEqual(606.06m, boleto.ValorPagoCredito);
            Assert.AreEqual(7.07m, boleto.ValorOutrasDespesas);
            Assert.AreEqual(8.08m, boleto.ValorOutrosCreditos);
        }

        [TestCase(PosUDataOcorrencia, TestName = "Data da ocorrencia em branco")]
        [TestCase(PosUDataCredito, TestName = "Data do credito em branco")]
        public void LerDetalheSegmentoU_ComDataEmBranco_NaoLancaERetornaDataMinima(int offset)
        {
            var sb = BuildSegmentoU();
            Blank(sb, offset, 8);

            Boleto boleto = null;
            Assert.DoesNotThrow(() => boleto = ParseSegmentoU(sb.ToString()));
            Assert.AreEqual(138.00m, boleto.ValorPago, "Values must still parse when a date is blank.");

            var dataAfetada = offset == PosUDataOcorrencia ? boleto.DataProcessamento : boleto.DataCredito;
            Assert.AreEqual(DateTime.MinValue, dataAfetada);
        }

        // Same characterization as segment T: Convert.ToDecimal is unguarded here as well.
        [TestCase(PosUJurosMulta, TestName = "Juros/Multa em branco lanca")]
        [TestCase(PosUDesconto, TestName = "Desconto em branco lanca")]
        [TestCase(PosUValorPago, TestName = "Valor pago em branco lanca")]
        [TestCase(PosUValorLiquido, TestName = "Valor liquido em branco lanca")]
        public void LerDetalheSegmentoU_ComCampoDecimalEmBranco_LancaExcecao(int offset)
        {
            var sb = BuildSegmentoU();
            Blank(sb, offset, 15);

            var ex = Assert.Throws<Exception>(() => ParseSegmentoU(sb.ToString()));
            Assert.AreEqual("Erro ao ler detalhe do arquivo de RETORNO / CNAB 240 / U.", ex.Message);
        }

        #endregion
    }
}

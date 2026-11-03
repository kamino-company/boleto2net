using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("Safra Carteira 1")]
    public class BancoSafraCarteira1
    {
        readonly IBanco _banco;
        public BancoSafraCarteira1()
        {
            var contaBancaria = new ContaBancaria
            {
                Agencia = "1234",
                DigitoAgencia = "5",
                Conta = "123456",
                DigitoConta = "7",
                CarteiraPadrao = "1",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
                TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
            };
            _banco = Banco.Instancia(Bancos.Safra);
            _banco.Cedente = Utils.GerarCedente("", "", "", contaBancaria);
            _banco.FormataCedente();
        }

        [Test]
        public void Safra_1_REM400()
        {
            Utils.TestarHomologacao(_banco, TipoArquivo.CNAB400, nameof(BancoSafraCarteira1), 5, true, "?", 223344);
        }


        [TestCase(141.50, "453", "BB943A", "1", "000004537", "42291690400000141507123450012345670000045372", "42297.12346 50012.345679 00000.453720 1 69040000014150", 2016, 9, 1)]
        [TestCase(2711.12, "456", "BB874A", "2", "000004561", "42292693400002711127123450012345670000045612", "42297.12346 50012.345679 00000.456129 2 69340000271112", 2016, 10, 1)]
        [TestCase(645.39, "414", "BB815A", "3", "000004146", "42293687300000645397123450012345670000041462", "42297.12346 50012.345679 00000.414623 3 68730000064539", 2016, 8, 1)]
        [TestCase(292.21, "444", "BB834A", "4", "000004448", "42294690500000292217123450012345670000044482", "42297.12346 50012.345679 00000.444828 4 69050000029221", 2016, 9, 2)]
        [TestCase(838, "562", "BB933A", "5", "000005622", "42295702600000838007123450012345670000056222", "42297.12346 50012.345679 00000.562223 5 70260000083800", 2017, 1, 1)]
        [TestCase(2927.11, "445", "BB874A", "6", "000004456", "42296690500002927117123450012345670000044562", "42297.12346 50012.345679 00000.445627 6 69050000292711", 2016, 9, 2)]
        [TestCase(2921.27, "443", "BB833A", "7", "000004430", "42297690500002921277123450012345670000044302", "42297.12346 50012.345679 00000.443028 7 69050000292127", 2016, 9, 2)]
        [TestCase(293.21, "468", "BB856A", "8", "000004685", "42298693500000293217123450012345670000046852", "42297.12346 50012.345679 00000.468520 8 69350000029321", 2016, 10, 2)]
        [TestCase(276, "561", "BB932A", "9", "000005614", "42299702600000276007123450012345670000056142", "42297.12346 50012.345679 00000.561423 9 70260000027600", 2017, 1, 1)]

        public void Safra_1_BoletoOK(decimal valorTitulo, string nossoNumero, string numeroDocumento, string digitoVerificador, string nossoNumeroFormatado, string codigoDeBarras, string linhaDigitavel, params int[] anoMesDia)
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
    }

    /// <summary>
    /// Remessa e retorno CNAB240 do Safra conforme o "Guia para implantação de transmissão e troca de
    /// arquivos - Layout padrão Safra CNAB 240". As posições nos comentários são as do guia (base 1).
    /// </summary>
    [TestFixture]
    [Category("Safra CNAB240")]
    public class BancoSafraCnab240Tests
    {
        private static readonly DateTime Vencimento = new DateTime(2026, 11, 10);

        // Posições do CNAB são base 1; Substring é base 0.
        private static string Campo(string linha, int posicao, int tamanho) => linha.Substring(posicao - 1, tamanho);

        private static IBanco BancoSafra()
        {
            var banco = Banco.Instancia(Bancos.Safra);
            banco.Cedente = new Cedente
            {
                CPFCNPJ = "11.222.333/0001-81",
                Nome = "Cedente Teste Ltda",
                ContaBancaria = new ContaBancaria
                {
                    Agencia = "1234",
                    DigitoAgencia = "5",
                    Conta = "123456",
                    DigitoConta = "7",
                    CarteiraPadrao = "1",
                    TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
                    TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                    TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
                }
            };
            banco.FormataCedente();
            return banco;
        }

        private static Boleto BoletoComEncargos(IBanco banco)
        {
            var boleto = new Boleto(banco)
            {
                DataVencimento = Vencimento,
                DataEmissao = new DateTime(2026, 10, 27),
                ValorTitulo = 141.50M,
                NossoNumero = "453",
                NumeroDocumento = "BB943A",
                NumeroControleParticipante = "CTRL-1",
                EspecieDocumento = TipoEspecieDocumento.DS,
                ValorJurosDia = 0.05M,
                DataJuros = Vencimento.AddDays(1),
                PercentualMulta = 2M,
                DataMulta = Vencimento.AddDays(1),
                ValorDesconto = 5M,
                DataDesconto = Vencimento.AddDays(-5),
                Sacado = Sacado()
            };
            boleto.ValidarDados();
            return boleto;
        }

        private static Boleto BoletoSemEncargos(IBanco banco)
        {
            var boleto = new Boleto(banco)
            {
                DataVencimento = Vencimento,
                DataEmissao = new DateTime(2026, 10, 27),
                ValorTitulo = 200M,
                NossoNumero = "454",
                NumeroDocumento = "77",
                Sacado = Sacado()
            };
            boleto.ValidarDados();
            return boleto;
        }

        private static Sacado Sacado() => new Sacado
        {
            CPFCNPJ = "987.654.321-00",
            Nome = "Sacado Teste",
            Endereco = new Endereco
            {
                LogradouroEndereco = "Rua de Teste",
                LogradouroNumero = "100",
                Bairro = "Centro",
                Cidade = "Sao Paulo",
                UF = "SP",
                CEP = "01310-100"
            }
        };

        private static string[] GerarRemessa(IBanco banco, params Boleto[] boletos) => GerarRemessa(banco, 7, boletos);

        private static string[] GerarRemessa(IBanco banco, int numeroArquivoRemessa, params Boleto[] boletos)
        {
            var colecao = new Boletos { Banco = banco };
            foreach (var boleto in boletos)
                colecao.Add(boleto);

            using (var stream = new MemoryStream())
            {
                new ArquivoRemessa(banco, TipoArquivo.CNAB240, numeroArquivoRemessa).GerarArquivoRemessa(colecao, stream);
                return Encoding.GetEncoding("ISO-8859-1").GetString(stream.ToArray())
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.TrimEnd('\r'))
                    .ToArray();
            }
        }

        [Test]
        public void Remessa_GeraHeaderLoteSegmentosPQRETrailers_Com240Posicoes()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));

            Assert.That(linhas.Length, Is.EqualTo(7));
            foreach (var linha in linhas)
                Assert.That(linha.Length, Is.EqualTo(240));
            Assert.That(string.Join(",", linhas.Select(l => Campo(l, 8, 1) + (Campo(l, 8, 1) == "3" ? Campo(l, 14, 1) : ""))),
                Is.EqualTo("0,1,3P,3Q,3R,5,9"));
            foreach (var linha in linhas)
                Assert.That(Campo(linha, 1, 3), Is.EqualTo("422"));
        }

        [Test]
        public void Remessa_HeaderDeArquivo_SegueOGuiaSafra()
        {
            var banco = BancoSafra();
            var header = GerarRemessa(banco, BoletoComEncargos(banco))[0];

            Assert.That(Campo(header, 4, 4), Is.EqualTo("0000"));
            Assert.That(Campo(header, 9, 9), Is.EqualTo(new string(' ', 9)));
            Assert.That(Campo(header, 18, 1), Is.EqualTo("2"));
            Assert.That(Campo(header, 19, 14), Is.EqualTo("11222333000181"));
            Assert.That(Campo(header, 33, 20), Is.EqualTo(new string(' ', 20)));   // convênio: Safra não trata
            Assert.That(Campo(header, 53, 5), Is.EqualTo("12345"));                // agência + DV
            Assert.That(Campo(header, 58, 1), Is.EqualTo(" "));
            Assert.That(Campo(header, 59, 12), Is.EqualTo("000000123456"));
            Assert.That(Campo(header, 71, 1), Is.EqualTo("7"));
            Assert.That(Campo(header, 72, 1), Is.EqualTo(" "));
            Assert.That(Campo(header, 73, 30).TrimEnd(), Is.EqualTo("CEDENTE TESTE LTDA"));
            Assert.That(Campo(header, 103, 30).TrimEnd(), Is.EqualTo("BANCO SAFRA S/A"));
            Assert.That(Campo(header, 143, 1), Is.EqualTo("1"));
            Assert.That(Campo(header, 144, 8), Is.EqualTo(DateTime.Now.ToString("ddMMyyyy")));
            Assert.That(Campo(header, 152, 6), Is.EqualTo("000000"));
            Assert.That(Campo(header, 158, 6), Is.EqualTo("000007"));
            Assert.That(Campo(header, 164, 3), Is.EqualTo("087"));
        }

        [Test]
        public void Remessa_HeaderDeLote_RepeteAgenciaEContaDoHeaderDeArquivo()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));
            var lote = linhas[1];

            Assert.That(Campo(lote, 4, 4), Is.EqualTo("0007"));                   // lote = número do arquivo (7)
            Assert.That(Campo(lote, 9, 1), Is.EqualTo("R"));
            Assert.That(Campo(lote, 10, 2), Is.EqualTo("01"));
            Assert.That(Campo(lote, 14, 3), Is.EqualTo("060"));
            Assert.That(Campo(lote, 18, 1), Is.EqualTo("2"));
            Assert.That(Campo(lote, 19, 15), Is.EqualTo("011222333000181"));
            Assert.That(Campo(lote, 34, 20), Is.EqualTo(new string(' ', 20)));
            Assert.That(Campo(lote, 54, 19), Is.EqualTo(Campo(linhas[0], 53, 19)));  // 054-072 = 053-071 do header
            Assert.That(Campo(lote, 74, 30).TrimEnd(), Is.EqualTo("CEDENTE TESTE LTDA"));
            Assert.That(Campo(lote, 104, 80), Is.EqualTo(new string(' ', 80)));
            Assert.That(Campo(lote, 184, 8), Is.EqualTo("00000007"));             // repete o lote de 004-007
            Assert.That(Campo(lote, 192, 8), Is.EqualTo(DateTime.Now.ToString("ddMMyyyy")));
            Assert.That(Campo(lote, 200, 8), Is.EqualTo("00000000"));
        }

        [Test]
        public void Remessa_SegmentoP_TituloComJurosDescontoEspecieSafra()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));
            var p = linhas[2];

            Assert.That(Campo(p, 9, 5), Is.EqualTo("00001"));
            Assert.That(Campo(p, 16, 2), Is.EqualTo("01"));
            Assert.That(Campo(p, 18, 19), Is.EqualTo(Campo(linhas[1], 54, 19)));  // 018-036 = 054-072 do lote
            Assert.That(Campo(p, 38, 9), Is.EqualTo("000004537"));                // nosso número 8 + DV
            Assert.That(Campo(p, 47, 11), Is.EqualTo(new string(' ', 11)));
            Assert.That(Campo(p, 58, 5), Is.EqualTo("11222"));                    // carteira, cadastro, documento, emissão, distribuição
            Assert.That(Campo(p, 63, 15), Is.EqualTo("BB943A".PadRight(15)));
            Assert.That(Campo(p, 78, 8), Is.EqualTo("10112026"));
            Assert.That(Campo(p, 86, 15), Is.EqualTo("000000000014150"));
            Assert.That(Campo(p, 101, 6), Is.EqualTo("12345 "));
            Assert.That(Campo(p, 107, 2), Is.EqualTo("04"));                      // DS = Duplicata de serviço
            Assert.That(Campo(p, 109, 1), Is.EqualTo("N"));
            Assert.That(Campo(p, 110, 8), Is.EqualTo("27102026"));
            Assert.That(Campo(p, 118, 1), Is.EqualTo("1"));
            Assert.That(Campo(p, 119, 8), Is.EqualTo("11112026"));
            Assert.That(Campo(p, 127, 15), Is.EqualTo("000000000000005"));
            Assert.That(Campo(p, 142, 1), Is.EqualTo("1"));
            Assert.That(Campo(p, 143, 8), Is.EqualTo("05112026"));
            Assert.That(Campo(p, 151, 15), Is.EqualTo("000000000000500"));
            Assert.That(Campo(p, 166, 30), Is.EqualTo(new string('0', 30)));    // IOF e abatimento
            Assert.That(Campo(p, 196, 25).TrimEnd(), Is.EqualTo("CTRL-1"));
            Assert.That(Campo(p, 221, 3), Is.EqualTo("300"));                     // não protestar
            Assert.That(Campo(p, 224, 4), Is.EqualTo("1060"));                    // baixar e devolver em 60 dias
            Assert.That(Campo(p, 228, 2), Is.EqualTo("09"));
            Assert.That(Campo(p, 230, 10), Is.EqualTo("0000000000"));
            Assert.That(Campo(p, 240, 1), Is.EqualTo("1"));
        }

        [Test]
        public void Remessa_SegmentosQER_PagadorEMultaPercentual()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));
            var q = linhas[3];
            var r = linhas[4];

            Assert.That(Campo(q, 9, 5), Is.EqualTo("00002"));
            Assert.That(Campo(q, 16, 2), Is.EqualTo("01"));
            Assert.That(Campo(q, 18, 1), Is.EqualTo("1"));
            Assert.That(Campo(q, 19, 15), Is.EqualTo("000098765432100"));
            Assert.That(Campo(q, 34, 40).TrimEnd(), Is.EqualTo("SACADO TESTE"));
            Assert.That(Campo(q, 114, 15).TrimEnd(), Is.EqualTo("CENTRO"));
            Assert.That(Campo(q, 129, 8), Is.EqualTo("01310100"));
            Assert.That(Campo(q, 137, 15).TrimEnd(), Is.EqualTo("SAO PAULO"));
            Assert.That(Campo(q, 152, 2), Is.EqualTo("SP"));
            Assert.That(Campo(q, 154, 16), Is.EqualTo(new string('0', 16)));     // sem sacador/avalista
            Assert.That(Campo(q, 210, 3), Is.EqualTo("000"));

            Assert.That(Campo(r, 9, 5), Is.EqualTo("00003"));
            Assert.That(Campo(r, 16, 2), Is.EqualTo("01"));
            Assert.That(Campo(r, 18, 48), Is.EqualTo(new string('0', 48)));
            Assert.That(Campo(r, 66, 1), Is.EqualTo("2"));
            Assert.That(Campo(r, 67, 8), Is.EqualTo("11112026"));
            Assert.That(Campo(r, 75, 15), Is.EqualTo("000000000000200"));
            Assert.That(Campo(r, 90, 151), Is.EqualTo(new string(' ', 151)));
        }

        [Test]
        public void Remessa_Trailers_TotalizamLoteEArquivo()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco), BoletoSemEncargos(banco));
            var trailerLote = linhas[linhas.Length - 2];
            var trailerArquivo = linhas[linhas.Length - 1];

            // 2 headers + P,Q,R + P,Q + 2 trailers
            Assert.That(linhas.Length, Is.EqualTo(9));
            Assert.That(Campo(trailerLote, 4, 5), Is.EqualTo("00075"));
            Assert.That(Campo(trailerLote, 18, 6), Is.EqualTo("000007"));        // 5 detalhes + header/trailer do lote
            Assert.That(Campo(trailerLote, 24, 6), Is.EqualTo("000002"));
            Assert.That(Campo(trailerLote, 30, 17), Is.EqualTo("00000000000034150"));
            Assert.That(Campo(trailerLote, 47, 6), Is.EqualTo("000000"));
            Assert.That(Campo(trailerLote, 53, 17), Is.EqualTo(new string('0', 17)));
            Assert.That(Campo(trailerArquivo, 4, 5), Is.EqualTo("99999"));
            Assert.That(Campo(trailerArquivo, 18, 6), Is.EqualTo("000001"));
            Assert.That(Campo(trailerArquivo, 24, 6), Is.EqualTo("000009"));
            Assert.That(Campo(trailerArquivo, 30, 6), Is.EqualTo(new string(' ', 6)));
        }

        [TestCase(1, "0001")]
        [TestCase(2, "0002")]
        [TestCase(9999, "9999")]
        [TestCase(10000, "0001")]
        public void Remessa_Lote_AcompanhaONumeroDoArquivoEmTodosOsRegistrosDoLote(int numeroArquivoRemessa, string loteEsperado)
        {
            // Guia: lote a partir de 0001, somando 1 a cada novo arquivo; 184-191 do header do lote repete o lote.
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, numeroArquivoRemessa, BoletoComEncargos(banco), BoletoSemEncargos(banco));

            Assert.That(Campo(linhas[0], 4, 4), Is.EqualTo("0000"));
            Assert.That(Campo(linhas[0], 158, 6), Is.EqualTo(numeroArquivoRemessa.ToString("000000")));
            for (var i = 1; i < linhas.Length - 1; i++)
                Assert.That(Campo(linhas[i], 4, 4), Is.EqualTo(loteEsperado), $"Lote da linha {i + 1}");
            Assert.That(Campo(linhas[1], 184, 8), Is.EqualTo(loteEsperado.PadLeft(8, '0')));
            Assert.That(Campo(linhas[linhas.Length - 1], 4, 4), Is.EqualTo("9999"));
            foreach (var linha in linhas)
                Assert.That(linha.Length, Is.EqualTo(240));
        }

        [Test]
        public void Boleto_NossoNumeroFormatado_EspelhaAsPosicoes038a046DaRemessa()
        {
            // Guia (estrutura do boleto): o nosso número impresso tem 9 caracteres, igual ao arquivo remessa.
            var banco = BancoSafra();
            var boleto = BoletoComEncargos(banco);
            var p = GerarRemessa(banco, boleto)[2];

            Assert.That(boleto.NossoNumeroFormatado, Is.EqualTo("000004537"));
            Assert.That(boleto.NossoNumeroFormatado, Is.EqualTo(Campo(p, 38, 9)));
        }

        [Test]
        public void Remessa_TituloSemEncargos_JurosIsentoSemDescontoEspecieOutrosESemSegmentoR()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoSemEncargos(banco));
            var p = linhas[2];

            Assert.That(string.Join(",", linhas.Select(l => Campo(l, 8, 1) + (Campo(l, 8, 1) == "3" ? Campo(l, 14, 1) : ""))),
                Is.EqualTo("0,1,3P,3Q,5,9"));
            Assert.That(Campo(p, 107, 2), Is.EqualTo("99"));                      // espécie não definida = outros
            Assert.That(Campo(p, 118, 24), Is.EqualTo("3" + new string('0', 23)));
            Assert.That(Campo(p, 142, 24), Is.EqualTo(new string('0', 24)));
        }

        [Test]
        public void Remessa_JurosPorPercentual_ViramValorPorDia()
        {
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.PercentualJurosDia = 0.033M;   // 0,033% de R$ 200,00 = R$ 0,066 -> 0,07
            boleto.DataJuros = Vencimento.AddDays(1);

            var p = GerarRemessa(banco, boleto)[2];

            Assert.That(Campo(p, 118, 1), Is.EqualTo("1"));
            Assert.That(Campo(p, 127, 15), Is.EqualTo("000000000000007"));
        }

        [Test]
        public void Remessa_Baixa_UsaMovimento02()
        {
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.CodigoOcorrencia = "02";

            var linhas = GerarRemessa(banco, boleto);

            Assert.That(Campo(linhas[2], 16, 2), Is.EqualTo("02"));
            Assert.That(Campo(linhas[3], 16, 2), Is.EqualTo("02"));
        }

        [Test]
        public void Remessa_CNAB400_ContinuaComHeaderDe400Posicoes()
        {
            var banco = BancoSafra();
            var colecao = new Boletos { Banco = banco };
            colecao.Add(BoletoSemEncargos(banco));

            using (var stream = new MemoryStream())
            {
                new ArquivoRemessa(banco, TipoArquivo.CNAB400, 7).GerarArquivoRemessa(colecao, stream);
                var linhas = Encoding.GetEncoding("ISO-8859-1").GetString(stream.ToArray())
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.TrimEnd('\r'))
                    .ToArray();

                Assert.That(linhas.Length, Is.EqualTo(3));
                Assert.That(linhas[0].Substring(0, 9), Is.EqualTo("01REMESSA"));
                foreach (var linha in linhas)
                    Assert.That(linha.Length, Is.EqualTo(400));
            }
        }

        // Monta um registro de 240 posições a partir de pares (posição base 1, valor).
        private static string Registro(params object[] posicaoValor)
        {
            var sb = new StringBuilder(new string(' ', 240));
            for (var c = 0; c < posicaoValor.Length; c += 2)
            {
                var posicao = (int)posicaoValor[c];
                var valor = (string)posicaoValor[c + 1];
                for (var i = 0; i < valor.Length; i++)
                    sb[posicao - 1 + i] = valor[i];
            }
            return sb.ToString();
        }

        private static string SegmentoT(int sequencial, string movimento, string nossoNumero, string motivo) => Registro(
            1, "4220001300", 9, sequencial.ToString("00000"), 14, "T", 16, movimento,
            18, "12345 0000001234567 ", 38, nossoNumero, 58, "1", 59, "BB943A",
            74, "10112026", 82, "000000000014150", 97, "42212345", 106, "CTRL-1",
            131, "09", 133, "1000098765432100", 149, "SACADO TESTE", 189, "0000000000",
            199, "000000000000250", 214, motivo);

        private static string SegmentoU(int sequencial, string valorPago, string dataOcorrencia, string dataCredito) => Registro(
            1, "4220001300", 9, sequencial.ToString("00000"), 14, "U", 16, "06",
            18, "000000000000010", 33, "000000000000500", 48, "000000000000000", 63, "000000000000000",
            78, valorPago, 93, valorPago, 108, "000000000000000", 123, "000000000000000",
            138, dataOcorrencia, 146, dataCredito);

        [Test]
        public void Retorno_LeHeaderESegmentosTEU_DeLiquidacaoEDeRejeicao()
        {
            var linhas = new[]
            {
                Registro(1, "42200000", 18, "211222333000181", 53, "12345 0000001234567 ",
                    73, "CEDENTE TESTE LTDA", 103, "BANCO SAFRA S/A", 143, "2", 144, "28102026",
                    152, "000000", 158, "000123", 164, "087"),
                Registro(1, "42200011T01  060", 18, "2011222333000181"),
                SegmentoT(1, "06", "000004537", "0000000000"),
                SegmentoU(2, "000000000013660", "27102026", "28102026"),
                SegmentoT(3, "03", "000004545", "0012000000"),
                SegmentoU(4, "000000000000000", "27102026", "00000000"),
                Registro(1, "42200015", 18, "000006"),
                Registro(1, "42299999", 18, "000001000008")
            };
            var conteudo = string.Join("\r\n", linhas) + "\r\n";

            ArquivoRetorno retorno;
            using (var stream = new MemoryStream(Encoding.GetEncoding("ISO-8859-1").GetBytes(conteudo)))
                retorno = new ArquivoRetorno(stream);

            Assert.That(retorno.TipoArquivo, Is.EqualTo(TipoArquivo.CNAB240));
            Assert.That(retorno.Banco.Codigo, Is.EqualTo(422));
            Assert.That(retorno.NumeroSequencial, Is.EqualTo(123));
            Assert.That(retorno.DataGeracao, Is.EqualTo(new DateTime(2026, 10, 28)));
            Assert.That(retorno.Banco.Cedente.CPFCNPJ, Is.EqualTo("11222333000181"));
            // 053-057 traz agência (4) + DV (1), como a remessa grava; 058 fica em branco.
            Assert.That(retorno.Banco.Cedente.ContaBancaria.Agencia, Is.EqualTo("1234"));
            Assert.That(retorno.Banco.Cedente.ContaBancaria.DigitoAgencia, Is.EqualTo("5"));
            Assert.That(retorno.Banco.Cedente.ContaBancaria.Conta, Is.EqualTo("000000123456"));
            Assert.That(retorno.Banco.Cedente.ContaBancaria.DigitoConta, Is.EqualTo("7"));
            Assert.That(retorno.Boletos.Count, Is.EqualTo(2));

            var liquidado = retorno.Boletos[0];
            Assert.That(liquidado.CodigoOcorrencia, Is.EqualTo("06"));
            Assert.That(liquidado.DescricaoOcorrencia, Is.EqualTo(Cnab.OcorrenciaCnab240("06")));
            Assert.That(liquidado.NossoNumero, Is.EqualTo("00000453"));
            Assert.That(liquidado.NossoNumeroDV, Is.EqualTo("7"));
            Assert.That(liquidado.NossoNumeroFormatado, Is.EqualTo("000004537"));   // mesmo formato do boleto emitido
            Assert.That(liquidado.NumeroDocumento, Is.EqualTo("BB943A"));
            Assert.That(liquidado.NumeroControleParticipante, Is.EqualTo("CTRL-1"));
            Assert.That(liquidado.TipoCarteira, Is.EqualTo(TipoCarteira.CarteiraCobrancaSimples));
            Assert.That(liquidado.DataVencimento, Is.EqualTo(Vencimento));
            Assert.That(liquidado.ValorTitulo, Is.EqualTo(141.50M));
            Assert.That(liquidado.ValorTarifas, Is.EqualTo(2.50M));
            Assert.That(liquidado.Sacado.CPFCNPJ, Is.EqualTo("98765432100"));
            Assert.That(liquidado.ValorJurosDia, Is.EqualTo(0.10M));
            Assert.That(liquidado.ValorDesconto, Is.EqualTo(5M));
            Assert.That(liquidado.ValorPago, Is.EqualTo(136.60M));
            Assert.That(liquidado.ValorPagoCredito, Is.EqualTo(136.60M));
            Assert.That(liquidado.DataProcessamento, Is.EqualTo(new DateTime(2026, 10, 27)));
            Assert.That(liquidado.DataCredito, Is.EqualTo(new DateTime(2026, 10, 28)));

            var rejeitado = retorno.Boletos[1];
            Assert.That(rejeitado.CodigoOcorrencia, Is.EqualTo("03"));
            Assert.That(rejeitado.DescricaoOcorrencia, Is.EqualTo("Entrada Rejeitada"));
            Assert.That(rejeitado.CodigoOcorrenciaAuxiliar, Is.EqualTo("0012000000"));
            Assert.That(rejeitado.NossoNumero, Is.EqualTo("00000454"));
            Assert.That(rejeitado.NossoNumeroDV, Is.EqualTo("5"));
            Assert.That(rejeitado.ValorPago, Is.EqualTo(0M));
        }
    }
}
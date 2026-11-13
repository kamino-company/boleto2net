using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("Safra Carteira 1")]
    public class BancoSafraCarteira1
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


        // Campo livre: 7 + agência (5 posições, sem DV) + conta (9, com DV) + nosso número (9) + 2. Agência "1234" vira "01234".
        [TestCase(141.50, "453", "BB943A", "2", "000004537", "42292690400000141507012340012345670000045372", "42297.01232 40012.345670 00000.453720 2 69040000014150", 2016, 9, 1)]
        [TestCase(2711.12, "456", "BB874A", "4", "000004561", "42294693400002711127012340012345670000045612", "42297.01232 40012.345670 00000.456129 4 69340000271112", 2016, 10, 1)]
        [TestCase(645.39, "414", "BB815A", "5", "000004146", "42295687300000645397012340012345670000041462", "42297.01232 40012.345670 00000.414623 5 68730000064539", 2016, 8, 1)]
        [TestCase(292.21, "444", "BB834A", "6", "000004448", "42296690500000292217012340012345670000044482", "42297.01232 40012.345670 00000.444828 6 69050000029221", 2016, 9, 2)]
        [TestCase(838, "562", "BB933A", "7", "000005622", "42297702600000838007012340012345670000056222", "42297.01232 40012.345670 00000.562223 7 70260000083800", 2017, 1, 1)]
        [TestCase(2927.11, "445", "BB874A", "8", "000004456", "42298690500002927117012340012345670000044562", "42297.01232 40012.345670 00000.445627 8 69050000292711", 2016, 9, 2)]
        [TestCase(2921.27, "443", "BB833A", "9", "000004430", "42299690500002921277012340012345670000044302", "42297.01232 40012.345670 00000.443028 9 69050000292127", 2016, 9, 2)]
        [TestCase(293.21, "468", "BB856A", "1", "000004685", "42291693500000293217012340012345670000046852", "42297.01232 40012.345670 00000.468520 1 69350000029321", 2016, 10, 2)]
        [TestCase(276, "561", "BB932A", "1", "000005614", "42291702600000276007012340012345670000056142", "42297.01232 40012.345670 00000.561423 1 70260000027600", 2017, 1, 1)]

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

        // Manual Safra 240 (p.8 nota 1, p.44-47): agência com 5 posições sem dígito, no campo livre e nos campos 1/2 da
        // linha digitável. A Hub informa a agência sem DV como número ("160"); o DV da agência não entra.
        // Valores esperados calculados à parte pelo algoritmo do manual (DAC módulo 11, DV dos campos módulo 10).
        [TestCase("160", "7", "42291162600000141507001600058019570000045372", "42297.00168 00058.019571 00000.453720 1 16260000014150")]
        [TestCase("0160", "7", "42291162600000141507001600058019570000045372", "42297.00168 00058.019571 00000.453720 1 16260000014150")]
        [TestCase("01600", "", "42294162600000141507016000058019570000045372", "42297.01604 00058.019571 00000.453720 4 16260000014150")]
        public void Safra_1_CodigoDeBarras_UsaAgenciaDe5PosicoesSemDigito(string agencia, string digitoAgencia, string codigoDeBarras, string linhaDigitavel)
        {
            var banco = Banco.Instancia(Bancos.Safra);
            banco.Cedente = Utils.GerarCedente("", "", "", new ContaBancaria
            {
                Agencia = agencia,
                DigitoAgencia = digitoAgencia,
                Conta = "580195",
                DigitoConta = "7",
                CarteiraPadrao = "1",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
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
            // Agência/código do beneficiário no boleto: "00000 / 000000000" (5 / 9 com DV), modelos p.56-57.
            Assert.That(banco.Cedente.CodigoFormatado, Is.EqualTo($"{codigoDeBarras.Substring(20, 5)} / 005801957"));
        }

        // Código de barras 26-34: conta com 9 posições "com dígito, complementando com zeros à esquerda" (p.51), ou
        // seja, conta de até 8 dígitos + DV. Valores esperados calculados à parte pelo algoritmo do manual.
        [TestCase("580195", "005801957", "42291162600000141507001600058019570000045372", "42297.00168 00058.019571 00000.453720 1 16260000014150")]
        [TestCase("1580195", "015801957", "42298162600000141507001600158019570000045372", "42297.00168 00158.019570 00000.453720 8 16260000014150")]
        [TestCase("12580195", "125801957", "42291162600000141507001601258019570000045372", "42297.00168 01258.019577 00000.453720 1 16260000014150")]
        public void Safra_1_CodigoDeBarras_ContaDe6a8DigitosComDV(string conta, string codigoBeneficiario, string codigoDeBarras, string linhaDigitavel)
        {
            var banco = Banco.Instancia(Bancos.Safra);
            banco.Cedente = Utils.GerarCedente("", "", "", new ContaBancaria
            {
                Agencia = "160",
                DigitoAgencia = "7",
                Conta = conta,
                DigitoConta = "7",
                CarteiraPadrao = "1",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
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
            Assert.That(banco.Cedente.CodigoFormatado, Is.EqualTo($"00160 / {codigoBeneficiario}"));
        }
    }

    /// <summary>
    /// Remessa e retorno CNAB240 do Safra conforme o "Manual de Transferência de Arquivos - Cobrança - Layout Padrão
    /// Safra 240" (versão agosto/2026). As posições nos comentários são as do manual (base 1).
    /// </summary>
    [TestFixture]
    [Category("Safra CNAB240")]
    public class BancoSafraCnab240Tests
    {
        private static readonly DateTime Vencimento = new DateTime(2026, 11, 10);

        // Posições do CNAB são base 1; Substring é base 0.
        private static string Campo(string linha, int posicao, int tamanho) => linha.Substring(posicao - 1, tamanho);

        private static IBanco BancoSafra(string agencia = "1234", string digitoAgencia = "5", string carteira = "1",
            string digitoConta = "7", TipoCarteira? tipoCarteiraPadrao = null, string conta = "123456")
        {
            var banco = Banco.Instancia(Bancos.Safra);
            banco.Cedente = new Cedente
            {
                CPFCNPJ = "11.222.333/0001-81",
                Nome = "Cedente Teste Ltda",
                ContaBancaria = new ContaBancaria
                {
                    Agencia = agencia,
                    DigitoAgencia = digitoAgencia,
                    Conta = conta,
                    DigitoConta = digitoConta,
                    CarteiraPadrao = carteira,
                    TipoCarteiraPadrao = tipoCarteiraPadrao
                        ?? (carteira == "2" ? TipoCarteira.CarteiraCobrancaVinculada : TipoCarteira.CarteiraCobrancaSimples),
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
                EspecieDocumento = TipoEspecieDocumento.DM,
                Sacado = Sacado()
            };
            boleto.ValidarDados();
            return boleto;
        }

        private static Sacado Sacado(string cep = "01310-100") => new Sacado
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
                CEP = cep
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

        // A remessa embrulha a falha de validação em exceções de geração; a mensagem útil fica numa interna.
        private static string MensagensDaCadeia(Exception ex)
        {
            var mensagens = new StringBuilder();
            for (var atual = ex; atual != null; atual = atual.InnerException)
                mensagens.AppendLine(atual.Message);
            return mensagens.ToString();
        }

        private static string Estrutura(string[] linhas) =>
            string.Join(",", linhas.Select(l => Campo(l, 8, 1) + (Campo(l, 8, 1) == "3" ? Campo(l, 14, 1) : "")));

        [Test]
        public void Remessa_GeraHeaderLoteSegmentosPQRETrailers_Com240Posicoes()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));

            Assert.That(linhas.Length, Is.EqualTo(7));
            foreach (var linha in linhas)
                Assert.That(linha.Length, Is.EqualTo(240));
            Assert.That(Estrutura(linhas), Is.EqualTo("0,1,3P,3Q,3R,5,9"));
            foreach (var linha in linhas)
                Assert.That(Campo(linha, 1, 3), Is.EqualTo("422"));
        }

        [Test]
        public void Remessa_HeaderDeArquivo_SegueOManualSafra()
        {
            var banco = BancoSafra();
            var header = GerarRemessa(banco, BoletoComEncargos(banco))[0];

            Assert.That(Campo(header, 4, 4), Is.EqualTo("0000"));
            Assert.That(Campo(header, 9, 9), Is.EqualTo(new string(' ', 9)));
            Assert.That(Campo(header, 18, 1), Is.EqualTo("2"));
            Assert.That(Campo(header, 19, 14), Is.EqualTo("11222333000181"));
            Assert.That(Campo(header, 33, 20), Is.EqualTo(new string(' ', 20)));   // convênio: Safra não trata
            Assert.That(Campo(header, 53, 5), Is.EqualTo("01234"));                // agência: 5 posições sem DV (p.8)
            Assert.That(Campo(header, 58, 1), Is.EqualTo(" "));                    // DV da agência fica em branco
            Assert.That(Campo(header, 59, 12), Is.EqualTo("000000123456"));
            Assert.That(Campo(header, 71, 1), Is.EqualTo("7"));
            Assert.That(Campo(header, 72, 1), Is.EqualTo(" "));
            Assert.That(Campo(header, 73, 30).TrimEnd(), Is.EqualTo("CEDENTE TESTE LTDA"));
            Assert.That(Campo(header, 103, 30).TrimEnd(), Is.EqualTo("BANCO SAFRA S/A"));
            Assert.That(Campo(header, 143, 1), Is.EqualTo("1"));
            Assert.That(Campo(header, 144, 8), Is.EqualTo(DateTime.Now.ToString("ddMMyyyy")));
            Assert.That(Campo(header, 152, 6), Is.EqualTo("000000"));
            Assert.That(Campo(header, 158, 6), Is.EqualTo("000007"));
            Assert.That(Campo(header, 164, 3), Is.EqualTo("103"));                 // versão do layout do arquivo (p.9)
        }

        [TestCase("1234", "5", "01234")]
        [TestCase("160", "7", "00160")]
        [TestCase("0160", "7", "00160")]
        [TestCase("1600", "", "01600")]
        [TestCase("01600", "", "01600")]
        public void Remessa_Agencia_5PosicoesSemDigitoEmHeadersESegmentoP(string agencia, string digitoAgencia, string agenciaEsperada)
        {
            var banco = BancoSafra(agencia, digitoAgencia);
            var linhas = GerarRemessa(banco, BoletoSemEncargos(banco));
            var p = linhas[2];

            Assert.That(Campo(linhas[0], 53, 6), Is.EqualTo(agenciaEsperada + " "));  // header de arquivo 053-058
            Assert.That(Campo(linhas[1], 54, 6), Is.EqualTo(agenciaEsperada + " "));  // header de lote 054-059
            Assert.That(Campo(p, 18, 6), Is.EqualTo(agenciaEsperada + " "));          // segmento P 018-023
            Assert.That(Campo(p, 101, 6), Is.EqualTo("00000 "));                      // agência cobradora: atribuída pelo CEP (C014)
        }

        [TestCase("123456")]
        [TestCase("0160-7")]
        [TestCase("")]
        [TestCase("０１６０")]   // dígitos Unicode (fullwidth) não são dígitos ASCII
        public void Cedente_AgenciaInvalidaParaOSafra_LancaExcecao(string agencia)
        {
            var ex = Assert.Catch(() => BancoSafra(agencia, "7"));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("agência"));
        }

        // Conta com 9 posições "com dígito" (p.8) no campo livre: o DV da conta é obrigatório e tem 1 dígito.
        [TestCase("")]
        [TestCase("12")]
        [TestCase("X")]
        [TestCase("７")]
        public void Cedente_DigitoDaContaInvalido_LancaExcecao(string digitoConta)
        {
            var ex = Assert.Catch(() => BancoSafra(digitoConta: digitoConta));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("dígito verificador").And.Contain("conta"));
            Assert.That(MensagensDaCadeia(ex), Does.Not.Contain("7 dígitos"));
        }

        // Manual p.8/p.51: conta com até 8 dígitos + DV (9 posições no código de barras; 12 + DV no arquivo).
        [TestCase("123456", "000000123456")]
        [TestCase("1580195", "000001580195")]
        [TestCase("12580195", "000012580195")]
        public void Remessa_Conta_De6a8DigitosNoHeaderENoSegmentoP(string conta, string contaEsperada)
        {
            var banco = BancoSafra(conta: conta);
            var linhas = GerarRemessa(banco, BoletoSemEncargos(banco));

            Assert.That(Campo(linhas[0], 59, 13), Is.EqualTo(contaEsperada + "7"));   // header de arquivo 059-071
            Assert.That(Campo(linhas[1], 60, 13), Is.EqualTo(contaEsperada + "7"));   // header de lote 060-072
            Assert.That(Campo(linhas[2], 24, 13), Is.EqualTo(contaEsperada + "7"));   // segmento P 024-036
        }

        [Test]
        public void Cedente_ContaComMaisDe8Digitos_LancaExcecao()
        {
            var ex = Assert.Catch(() => BancoSafra(conta: "123456789"));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("123456789"));
        }

        [Test]
        public void Remessa_CNAB400_ContaComMaisDe6Digitos_LancaExcecao()
        {
            // O CNAB400 do Safra tem 6 posições de conta; conta de 7 ou 8 dígitos só cabe no CNAB240.
            var banco = BancoSafra(conta: "1580195");
            var colecao = new Boletos { Banco = banco };
            colecao.Add(BoletoSemEncargos(banco));

            using (var stream = new MemoryStream())
            {
                var ex = Assert.Catch(() => new ArquivoRemessa(banco, TipoArquivo.CNAB400, 7).GerarArquivoRemessa(colecao, stream));
                Assert.That(MensagensDaCadeia(ex), Does.Contain("1580195").And.Contain("6 dígitos"));
            }
        }

        // P 058 e os totais por carteira do trailer de lote seguem a carteira do cedente (a mesma do código de barras),
        // mesmo quando o chamador não informa um TipoCarteiraPadrao coerente.
        [TestCase("2", TipoCarteira.CarteiraCobrancaSimples, "2", "000000", "000001")]
        [TestCase("1", TipoCarteira.CarteiraCobrancaVinculada, "1", "000001", "000000")]
        public void Remessa_TipoCarteira_SegueACarteiraDoCedente(string carteira, TipoCarteira tipoInformado,
            string carteiraNoP, string qtdeSimples, string qtdeVinculada)
        {
            var banco = BancoSafra(carteira: carteira, tipoCarteiraPadrao: tipoInformado);
            var linhas = GerarRemessa(banco, BoletoSemEncargos(banco));
            var trailerLote = linhas[linhas.Length - 2];

            Assert.That(Campo(linhas[2], 58, 1), Is.EqualTo(carteiraNoP));
            Assert.That(Campo(trailerLote, 24, 6), Is.EqualTo(qtdeSimples));
            Assert.That(Campo(trailerLote, 47, 6), Is.EqualTo(qtdeVinculada));
        }

        [Test]
        public void Remessa_CNAB400_AgenciaCom5Digitos_LancaExcecao()
        {
            // O CNAB400 do Safra tem 4 posições de agência; agência com 5 dígitos significativos só cabe no CNAB240.
            var banco = BancoSafra("12400", "");
            var colecao = new Boletos { Banco = banco };
            colecao.Add(BoletoSemEncargos(banco));

            using (var stream = new MemoryStream())
            {
                var ex = Assert.Catch(() => new ArquivoRemessa(banco, TipoArquivo.CNAB400, 7).GerarArquivoRemessa(colecao, stream));
                Assert.That(MensagensDaCadeia(ex), Does.Contain("12400"));
            }
        }

        [Test]
        public void Remessa_HeaderDeLote_RepeteAgenciaEContaDoHeaderDeArquivo()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));
            var lote = linhas[1];

            Assert.That(Campo(lote, 4, 4), Is.EqualTo("0001"));                   // 1º (e único) lote do arquivo (G002)
            Assert.That(Campo(lote, 9, 1), Is.EqualTo("R"));
            Assert.That(Campo(lote, 10, 2), Is.EqualTo("01"));
            Assert.That(Campo(lote, 14, 3), Is.EqualTo("060"));
            Assert.That(Campo(lote, 18, 1), Is.EqualTo("2"));
            Assert.That(Campo(lote, 19, 15), Is.EqualTo("011222333000181"));
            Assert.That(Campo(lote, 34, 20), Is.EqualTo(new string(' ', 20)));
            Assert.That(Campo(lote, 54, 19), Is.EqualTo(Campo(linhas[0], 53, 19)));  // 054-072 = 053-071 do header
            Assert.That(Campo(lote, 74, 30).TrimEnd(), Is.EqualTo("CEDENTE TESTE LTDA"));
            Assert.That(Campo(lote, 104, 80), Is.EqualTo(new string(' ', 80)));
            Assert.That(Campo(lote, 184, 8), Is.EqualTo("00000007"));             // número remessa = NSA (G079)
            Assert.That(Campo(lote, 192, 8), Is.EqualTo(DateTime.Now.ToString("ddMMyyyy")));
            Assert.That(Campo(lote, 200, 8), Is.EqualTo("00000000"));
        }

        [Test]
        public void Remessa_SegmentoP_TituloComJurosDescontoEspecieSafra()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));
            var p = linhas[2];

            Assert.That(Campo(p, 4, 4), Is.EqualTo("0001"));
            Assert.That(Campo(p, 9, 5), Is.EqualTo("00001"));
            Assert.That(Campo(p, 16, 2), Is.EqualTo("01"));
            Assert.That(Campo(p, 18, 19), Is.EqualTo(Campo(linhas[1], 54, 19)));  // 018-036 = 054-072 do lote
            Assert.That(Campo(p, 38, 9), Is.EqualTo("000004537"));                // nosso número 8 + DV
            Assert.That(Campo(p, 47, 11), Is.EqualTo(new string(' ', 11)));
            Assert.That(Campo(p, 58, 5), Is.EqualTo("11222"));                    // carteira, cadastro, documento, emissão, distribuição
            Assert.That(Campo(p, 63, 15), Is.EqualTo("BB943A".PadRight(15)));
            Assert.That(Campo(p, 78, 8), Is.EqualTo("10112026"));
            Assert.That(Campo(p, 86, 15), Is.EqualTo("000000000014150"));
            Assert.That(Campo(p, 101, 6), Is.EqualTo("00000 "));                  // agência cobradora: zeros (C014)
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

        // Espécie C015 (p.30): 02 DM, 04 DS, 12 NP, 16 NS, 17 RC, 31 CC (33 BDA não tem correspondente no enum).
        [TestCase(TipoEspecieDocumento.DM, "02")]
        [TestCase(TipoEspecieDocumento.DS, "04")]
        [TestCase(TipoEspecieDocumento.NP, "12")]
        [TestCase(TipoEspecieDocumento.NS, "16")]
        [TestCase(TipoEspecieDocumento.RC, "17")]
        [TestCase(TipoEspecieDocumento.CC, "31")]
        public void Remessa_SegmentoP_EspecieDaTabelaSafra(TipoEspecieDocumento especie, string codigoEsperado)
        {
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.EspecieDocumento = especie;

            var p = GerarRemessa(banco, boleto)[2];

            Assert.That(Campo(p, 107, 2), Is.EqualTo(codigoEsperado));
        }

        [TestCase(TipoEspecieDocumento.NaoDefinido)]
        [TestCase(TipoEspecieDocumento.NF)]
        [TestCase(TipoEspecieDocumento.OU)]
        public void Remessa_SegmentoP_EspecieForaDaTabelaSafra_LancaExcecao(TipoEspecieDocumento especie)
        {
            // 23 (NF) e 99 (outros) não existem no Safra: o título voltaria rejeitado com motivo 21 (espécie inválida).
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.EspecieDocumento = especie;

            var ex = Assert.Catch(() => GerarRemessa(banco, boleto));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("Espécie").And.Contain(especie.ToString()));
        }

        [Test]
        public void Remessa_SegmentosQER_PagadorEMultaPercentual()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoComEncargos(banco));
            var q = linhas[3];
            var r = linhas[4];

            Assert.That(Campo(q, 4, 4), Is.EqualTo("0001"));
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

            Assert.That(Campo(r, 4, 4), Is.EqualTo("0001"));
            Assert.That(Campo(r, 9, 5), Is.EqualTo("00003"));
            Assert.That(Campo(r, 16, 2), Is.EqualTo("01"));
            Assert.That(Campo(r, 18, 48), Is.EqualTo(new string('0', 48)));
            Assert.That(Campo(r, 66, 1), Is.EqualTo("2"));
            Assert.That(Campo(r, 67, 8), Is.EqualTo("11112026"));
            Assert.That(Campo(r, 75, 15), Is.EqualTo("000000000000200"));
            Assert.That(Campo(r, 90, 151), Is.EqualTo(new string(' ', 151)));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("00000-000")]
        [TestCase("013101000")]
        [TestCase("ABCDE-FGH")]
        [TestCase("０１３１０１００")]   // dígitos Unicode (fullwidth)
        public void Remessa_SegmentoQ_CepVazioZeradoOuInvalido_LancaExcecao(string cep)
        {
            // Manual p.8 nota 3: CEP em branco ou zerado não é aceito (motivo 48 - CEP inválido).
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.Sacado = Sacado(cep);

            var ex = Assert.Catch(() => GerarRemessa(banco, boleto));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("CEP"));
        }

        [TestCase("01310-100", "01310100")]
        [TestCase("01.310-100", "01310100")]
        [TestCase("01310100", "01310100")]
        [TestCase("1310100", "01310100")]    // zero à esquerda perdido (CEP guardado como número)
        public void Remessa_SegmentoQ_CepValido_SaiCom8Digitos(string cep, string esperado)
        {
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.Sacado = Sacado(cep);

            var q = GerarRemessa(banco, boleto)[3];

            Assert.That(Campo(q, 129, 8), Is.EqualTo(esperado));
        }

        // Pedido de baixa (movimento 02): o Q é opcional para instruções (p.5), então o CEP não é validado, só formatado.
        [TestCase("", "00000000")]
        [TestCase(null, "00000000")]
        [TestCase("00000-000", "00000000")]
        [TestCase("1310100", "01310100")]
        [TestCase("01310-100", "01310100")]
        public void Remessa_PedidoDeBaixa_NaoValidaCepDoPagador(string cep, string esperado)
        {
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.Sacado = Sacado(cep);
            boleto.CodigoOcorrencia = "02";

            var q = GerarRemessa(banco, boleto)[3];

            Assert.That(Campo(q, 16, 2), Is.EqualTo("02"));
            Assert.That(Campo(q, 129, 8), Is.EqualTo(esperado));
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
            Assert.That(Campo(trailerLote, 4, 5), Is.EqualTo("00015"));
            Assert.That(Campo(trailerLote, 18, 6), Is.EqualTo("000007"));        // 5 detalhes + header/trailer do lote
            Assert.That(Campo(trailerLote, 24, 6), Is.EqualTo("000002"));
            Assert.That(Campo(trailerLote, 30, 17), Is.EqualTo("00000000000034150"));
            Assert.That(Campo(trailerLote, 47, 6), Is.EqualTo("000000"));
            Assert.That(Campo(trailerLote, 53, 17), Is.EqualTo(new string('0', 17)));
            Assert.That(Campo(trailerLote, 70, 46), Is.EqualTo(new string('0', 46)));  // caucionada e descontada: numéricos (p.21)
            Assert.That(Campo(trailerLote, 116, 125), Is.EqualTo(new string(' ', 125)));
            Assert.That(Campo(trailerArquivo, 4, 5), Is.EqualTo("99999"));
            Assert.That(Campo(trailerArquivo, 18, 6), Is.EqualTo("000001"));
            Assert.That(Campo(trailerArquivo, 24, 6), Is.EqualTo("000009"));
            Assert.That(Campo(trailerArquivo, 30, 6), Is.EqualTo("000000"));       // contas p/ conciliação: numérico (p.10)
            Assert.That(Campo(trailerArquivo, 36, 205), Is.EqualTo(new string(' ', 205)));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(9999)]
        [TestCase(10000)]
        public void Remessa_Lote_Sempre0001ENumeroRemessaNoHeaderDeLote(int numeroArquivoRemessa)
        {
            // G002 (p.39): 0001 no primeiro lote do arquivo. G079 (p.43): 184-191 = sequência de envio do arquivo (NSA).
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, numeroArquivoRemessa, BoletoComEncargos(banco), BoletoSemEncargos(banco));

            Assert.That(Campo(linhas[0], 4, 4), Is.EqualTo("0000"));
            Assert.That(Campo(linhas[0], 158, 6), Is.EqualTo(numeroArquivoRemessa.ToString("000000")));
            for (var i = 1; i < linhas.Length - 1; i++)
                Assert.That(Campo(linhas[i], 4, 4), Is.EqualTo("0001"), $"Lote da linha {i + 1}");
            Assert.That(Campo(linhas[1], 184, 8), Is.EqualTo(numeroArquivoRemessa.ToString("00000000")));
            Assert.That(Campo(linhas[linhas.Length - 1], 4, 4), Is.EqualTo("9999"));
            foreach (var linha in linhas)
                Assert.That(linha.Length, Is.EqualTo(240));
        }

        [Test]
        public void Remessa_CNAB400_NumeroDoArquivoNaoEhTrocadoPorOutraRemessaGeradaEmOutraThread()
        {
            // BancoSafra é singleton. O CNAB400 repete o número do arquivo em cada detalhe (392-394); um header gerado
            // em outra thread no meio da remessa não pode trocar esse número.
            var banco = BancoSafra();
            var registro = 0;
            banco.GerarHeaderRemessa(TipoArquivo.CNAB400, 7, ref registro);

            var outraThread = new Thread(() =>
            {
                var registroOutra = 0;
                banco.GerarHeaderRemessa(TipoArquivo.CNAB400, 8, ref registroOutra);
            });
            outraThread.Start();
            outraThread.Join();

            var detalhe = banco.GerarDetalheRemessa(TipoArquivo.CNAB400, BoletoSemEncargos(banco), ref registro);

            Assert.That(Campo(detalhe, 392, 3), Is.EqualTo("007"));
        }

        [Test]
        public void Boleto_NossoNumeroFormatado_EspelhaAsPosicoes038a046DaRemessa()
        {
            // O nosso número impresso tem 9 caracteres, igual ao arquivo remessa.
            var banco = BancoSafra();
            var boleto = BoletoComEncargos(banco);
            var p = GerarRemessa(banco, boleto)[2];

            Assert.That(boleto.NossoNumeroFormatado, Is.EqualTo("000004537"));
            Assert.That(boleto.NossoNumeroFormatado, Is.EqualTo(Campo(p, 38, 9)));
        }

        [Test]
        public void Remessa_TituloSemEncargos_JurosIsentoSemDescontoESemSegmentoR()
        {
            var banco = BancoSafra();
            var linhas = GerarRemessa(banco, BoletoSemEncargos(banco));
            var p = linhas[2];

            Assert.That(Estrutura(linhas), Is.EqualTo("0,1,3P,3Q,5,9"));
            Assert.That(Campo(p, 107, 2), Is.EqualTo("02"));                      // DM
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
        public void Remessa_PedidoDeBaixaDeTituloComMulta_NaoGeraSegmentoR()
        {
            // O segmento R (descontos 2/3 e multa) não se aplica ao pedido de baixa (movimento 02).
            var banco = BancoSafra();
            var boleto = BoletoComEncargos(banco);
            boleto.CodigoOcorrencia = "02";

            var linhas = GerarRemessa(banco, boleto);

            Assert.That(Estrutura(linhas), Is.EqualTo("0,1,3P,3Q,5,9"));
            Assert.That(Campo(linhas[linhas.Length - 2], 18, 6), Is.EqualTo("000004"));  // P, Q + header/trailer do lote
            Assert.That(Campo(linhas[linhas.Length - 1], 24, 6), Is.EqualTo("000006"));
        }

        // C026 (p.31): 1 = protestar dias corridos, somente cobrança vinculada; 2 = protestar dias úteis, somente
        // cobrança simples; 3 = não protestar.
        [TestCase("1", TipoCodigoProtesto.ProtestarDiasUteis, "205")]
        [TestCase("2", TipoCodigoProtesto.ProtestarDiasCorridos, "105")]
        [TestCase("1", TipoCodigoProtesto.NaoProtestar, "300")]
        [TestCase("2", TipoCodigoProtesto.NaoProtestar, "300")]
        public void Remessa_SegmentoP_ProtestoConformeACarteira(string carteira, TipoCodigoProtesto codigoProtesto, string esperado)
        {
            var banco = BancoSafra(carteira: carteira);
            var boleto = BoletoSemEncargos(banco);
            boleto.CodigoProtesto = codigoProtesto;
            boleto.DiasProtesto = codigoProtesto == TipoCodigoProtesto.NaoProtestar ? 0 : 5;

            var p = GerarRemessa(banco, boleto)[2];

            Assert.That(Campo(p, 58, 1), Is.EqualTo(carteira));
            Assert.That(Campo(p, 221, 3), Is.EqualTo(esperado));
        }

        [TestCase("1", TipoCodigoProtesto.ProtestarDiasCorridos)]
        [TestCase("2", TipoCodigoProtesto.ProtestarDiasUteis)]
        [TestCase("1", TipoCodigoProtesto.NegativacaoSemProtesto)]
        public void Remessa_SegmentoP_ProtestoNaoAceitoNaCarteira_LancaExcecao(string carteira, TipoCodigoProtesto codigoProtesto)
        {
            var banco = BancoSafra(carteira: carteira);
            var boleto = BoletoSemEncargos(banco);
            boleto.CodigoProtesto = codigoProtesto;
            boleto.DiasProtesto = 5;

            var ex = Assert.Catch(() => GerarRemessa(banco, boleto));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("protesto"));
        }

        [Test]
        public void Remessa_SegmentoP_PrazoDeBaixaMenorQueOPrazoDeProtesto_LancaExcecao()
        {
            // p.13: os campos 37.3P (dias para protesto) e 39.3P (dias para baixa) não podem ser conflitantes.
            var banco = BancoSafra();
            var boleto = BoletoSemEncargos(banco);
            boleto.CodigoProtesto = TipoCodigoProtesto.ProtestarDiasUteis;
            boleto.DiasProtesto = 10;
            boleto.CodigoBaixaDevolucao = TipoCodigoBaixaDevolucao.BaixarDevolver;
            boleto.DiasBaixaDevolucao = 5;

            var ex = Assert.Catch(() => GerarRemessa(banco, boleto));

            Assert.That(MensagensDaCadeia(ex), Does.Contain("baixa"));
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
                Assert.That(Campo(linhas[0], 27, 5), Is.EqualTo("12345"));        // CNAB400 mantém agência (4) + DV
                Assert.That(Campo(linhas[1], 18, 5), Is.EqualTo("12345"));
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
            18, "01234 0000001234567 ", 38, nossoNumero, 58, "1", 59, "BB943A",
            74, "10112026", 82, "000000000014150", 97, "42212345", 106, "CTRL-1",
            131, "09", 133, "1000098765432100", 149, "SACADO TESTE", 189, "0000000000",
            199, "000000000000250", 214, motivo);

        private static string SegmentoU(int sequencial, string movimento, string valorPago, string valorLiquido,
            string dataOcorrencia, string dataCredito) => Registro(
            1, "4220001300", 9, sequencial.ToString("00000"), 14, "U", 16, movimento,
            18, "000000000000010", 33, "000000000000500", 48, "000000000000300", 63, "000000000000040",
            78, valorPago, 93, valorLiquido, 108, "000000000000070", 123, "000000000000080",
            138, dataOcorrencia, 146, dataCredito);

        private static ArquivoRetorno LerRetorno(params string[] detalhes)
        {
            var linhas = new[]
            {
                Registro(1, "42200000", 18, "211222333000181", 53, "01234 0000001234567 ",
                    73, "CEDENTE TESTE LTDA", 103, "BANCO SAFRA S/A", 143, "2", 144, "28102026",
                    152, "000000", 158, "000123", 164, "103"),
                Registro(1, "42200011T01  060", 18, "2011222333000181")
            }.Concat(detalhes).Concat(new[]
            {
                Registro(1, "42200015", 18, (detalhes.Length + 2).ToString("000000")),
                Registro(1, "42299999", 18, "000001" + (detalhes.Length + 4).ToString("000000"))
            });
            var conteudo = string.Join("\r\n", linhas) + "\r\n";

            using (var stream = new MemoryStream(Encoding.GetEncoding("ISO-8859-1").GetBytes(conteudo)))
                return new ArquivoRetorno(stream);
        }

        [Test]
        public void Retorno_LeHeaderESegmentosTEU_DeLiquidacaoEDeRejeicao()
        {
            var retorno = LerRetorno(
                SegmentoT(1, "06", "000004537", "0000000000"),
                SegmentoU(2, "06", "000000000013660", "000000000013410", "27102026", "28102026"),
                SegmentoT(3, "03", "000004545", "2148A4    "),
                SegmentoU(4, "03", "000000000000000", "000000000000000", "27102026", "00000000"));

            Assert.That(retorno.TipoArquivo, Is.EqualTo(TipoArquivo.CNAB240));
            Assert.That(retorno.Banco.Codigo, Is.EqualTo(422));
            Assert.That(retorno.NumeroSequencial, Is.EqualTo(123));
            Assert.That(retorno.DataGeracao, Is.EqualTo(new DateTime(2026, 10, 28)));
            Assert.That(retorno.Banco.Cedente.CPFCNPJ, Is.EqualTo("11222333000181"));
            // 053-057 traz a agência com 5 posições sem DV; 058 (DV da agência) em branco.
            Assert.That(retorno.Banco.Cedente.ContaBancaria.Agencia, Is.EqualTo("01234"));
            Assert.That(retorno.Banco.Cedente.ContaBancaria.DigitoAgencia, Is.EqualTo(""));
            Assert.That(retorno.Banco.Cedente.ContaBancaria.Conta, Is.EqualTo("000000123456"));
            Assert.That(retorno.Banco.Cedente.ContaBancaria.DigitoConta, Is.EqualTo("7"));
            Assert.That(retorno.Boletos.Count, Is.EqualTo(2));

            var liquidado = retorno.Boletos[0];
            Assert.That(liquidado.CodigoOcorrencia, Is.EqualTo("06"));
            Assert.That(liquidado.DescricaoOcorrencia, Is.EqualTo("Liquidação"));
            Assert.That(liquidado.NossoNumero, Is.EqualTo("00000453"));
            Assert.That(liquidado.NossoNumeroDV, Is.EqualTo("7"));
            Assert.That(liquidado.NossoNumeroFormatado, Is.EqualTo("000004537"));   // mesmo formato do boleto emitido
            Assert.That(liquidado.NumeroDocumento, Is.EqualTo("BB943A"));
            Assert.That(liquidado.NumeroControleParticipante, Is.EqualTo("CTRL-1"));
            Assert.That(liquidado.TipoCarteira, Is.EqualTo(TipoCarteira.CarteiraCobrancaSimples));
            Assert.That(liquidado.DataVencimento, Is.EqualTo(Vencimento));
            Assert.That(liquidado.ValorTitulo, Is.EqualTo(141.50M));
            Assert.That(liquidado.ValorTarifas, Is.EqualTo(2.50M));                 // T 199-213
            Assert.That(liquidado.Sacado.CPFCNPJ, Is.EqualTo("98765432100"));
            Assert.That(liquidado.ValorJurosDia, Is.EqualTo(0.10M));                // U 018-032: juros + multa + encargos
            Assert.That(liquidado.ValorDesconto, Is.EqualTo(5M));                   // U 033-047
            Assert.That(liquidado.ValorAbatimento, Is.EqualTo(3M));                 // U 048-062
            Assert.That(liquidado.ValorIOF, Is.EqualTo(0.40M));                     // U 063-077
            Assert.That(liquidado.ValorPago, Is.EqualTo(136.60M));                  // U 078-092: pago pelo pagador
            Assert.That(liquidado.ValorPagoCredito, Is.EqualTo(134.10M));           // U 093-107: líquido a creditar
            Assert.That(liquidado.ValorOutrasDespesas, Is.EqualTo(0.70M));          // U 108-122
            Assert.That(liquidado.ValorOutrosCreditos, Is.EqualTo(0.80M));          // U 123-137
            Assert.That(liquidado.DataProcessamento, Is.EqualTo(new DateTime(2026, 10, 27)));  // U 138-145
            Assert.That(liquidado.DataCredito, Is.EqualTo(new DateTime(2026, 10, 28)));        // U 146-153

            var rejeitado = retorno.Boletos[1];
            Assert.That(rejeitado.CodigoOcorrencia, Is.EqualTo("03"));
            Assert.That(rejeitado.DescricaoOcorrencia, Is.EqualTo("Entrada rejeitada"));
            Assert.That(rejeitado.CodigoOcorrenciaAuxiliar, Is.EqualTo("2148A4    "));  // T 214-223: até 5 motivos de 2 posições
            Assert.That(rejeitado.NossoNumero, Is.EqualTo("00000454"));
            Assert.That(rejeitado.NossoNumeroDV, Is.EqualTo("5"));
            Assert.That(rejeitado.ValorPago, Is.EqualTo(0M));
        }

        // Tabela C044 do Safra (p.33-34). Difere da tabela FEBRABAN genérica em vários códigos (ex.: 44 e 45).
        [TestCase("02", "Entrada confirmada")]
        [TestCase("07", "Confirmação do recebimento da instrução de desconto")]
        [TestCase("08", "Confirmação do recebimento do cancelamento do desconto")]
        [TestCase("09", "Baixa")]
        [TestCase("17", "Liquidação após baixa ou liquidação título não registrado")]
        [TestCase("26", "Instrução rejeitada")]
        [TestCase("28", "Débito de tarifas/custas")]
        [TestCase("44", "Título pago com cheque devolvido")]
        [TestCase("45", "Título pago com cheque compensado")]
        [TestCase("53", "Título DDA recusado pela CIP")]
        [TestCase("94", "Cancelamento de intenção de pagamento")]
        [TestCase("10", "")]
        public void Retorno_DescricaoDaOcorrencia_SegueATabelaSafra(string movimento, string descricao)
        {
            var retorno = LerRetorno(
                SegmentoT(1, movimento, "000004537", "10        "),
                SegmentoU(2, movimento, "000000000000000", "000000000000000", "27102026", "00000000"));

            Assert.That(retorno.Boletos[0].CodigoOcorrencia, Is.EqualTo(movimento));
            Assert.That(retorno.Boletos[0].DescricaoOcorrencia, Is.EqualTo(descricao));
        }
    }
}

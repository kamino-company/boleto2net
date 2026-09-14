using NUnit.Framework;

namespace Boleto2Net.Testes
{
    [TestFixture]
    [Category("Sicoob Convenio Header CNAB240")]
    public class BancoSicoobConvenioHeaderTests
    {
        // N3-7193: algumas cooperativas Sicoob (ex.: 4036) rejeitam a remessa quando o campo
        // Convênio vem preenchido, exigindo-o em branco; outras (ex.: 4355, N3-2384) exigem o
        // oposto. Cedente.SicoobConvenioEmBranco controla o comportamento por conta.
        private static IBanco CriarBanco(bool convenioEmBranco)
        {
            var contaBancaria = new ContaBancaria
            {
                Agencia = "4036",
                DigitoAgencia = "0",
                Conta = "2233363",
                DigitoConta = "0",
                CarteiraPadrao = "1",
                VariacaoCarteiraPadrao = "01",
                TipoCarteiraPadrao = TipoCarteira.CarteiraCobrancaSimples,
                TipoFormaCadastramento = TipoFormaCadastramento.ComRegistro,
                TipoImpressaoBoleto = TipoImpressaoBoleto.Empresa
            };

            var banco = Banco.Instancia(Bancos.Sicoob);
            banco.Cedente = Utils.GerarCedente("165515", "8", "", contaBancaria);
            banco.Cedente.SicoobConvenioEmBranco = convenioEmBranco;
            banco.FormataCedente();
            return banco;
        }

        // Header do Arquivo: Convênio nas posições 033-052 (1-indexado) => índice 32, tamanho 20.
        private static string ConvenioHeaderArquivo(string header)
        {
            var linhaHeaderArquivo = header.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None)[0];
            return linhaHeaderArquivo.Substring(32, 20);
        }

        // Header do Lote: Convênio nas posições 034-053 (1-indexado) => índice 33, tamanho 20.
        private static string ConvenioHeaderLote(string header)
        {
            var linhaHeaderLote = header.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None)[1];
            return linhaHeaderLote.Substring(33, 20);
        }

        [Test]
        public void Sicoob_Convenio_PreenchidoPorPadrao()
        {
            var banco = CriarBanco(convenioEmBranco: false);
            var numeroRegistroGeral = 0;

            var header = banco.GerarHeaderRemessa(TipoArquivo.CNAB240, 1, ref numeroRegistroGeral);

            Assert.That(ConvenioHeaderArquivo(header), Is.EqualTo("              165515"), "Convênio do Header de Arquivo deveria vir preenchido por padrão");
            Assert.That(ConvenioHeaderLote(header), Is.EqualTo("              165515"), "Convênio do Header de Lote deveria vir preenchido por padrão");
        }

        [Test]
        public void Sicoob_Convenio_EmBrancoQuandoConfigurado()
        {
            var banco = CriarBanco(convenioEmBranco: true);
            var numeroRegistroGeral = 0;

            var header = banco.GerarHeaderRemessa(TipoArquivo.CNAB240, 1, ref numeroRegistroGeral);

            Assert.That(ConvenioHeaderArquivo(header), Is.EqualTo(new string(' ', 20)), "Convênio do Header de Arquivo deveria vir em branco quando SicoobConvenioEmBranco = true");
            Assert.That(ConvenioHeaderLote(header), Is.EqualTo(new string(' ', 20)), "Convênio do Header de Lote deveria vir em branco quando SicoobConvenioEmBranco = true");
        }
    }
}

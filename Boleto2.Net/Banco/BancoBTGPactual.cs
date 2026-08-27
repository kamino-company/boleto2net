using System;
using System.Collections.Generic;
using Boleto2Net.Exceptions;

namespace Boleto2Net
{
    /// <summary>
    /// BTG Pactual (208).
    ///
    /// Implementa apenas a composição do boleto - campo livre do código de barras e nosso número. A
    /// cobrança do BTG na plataforma é registrada pela VAN bancária, que devolve o registro mas não a
    /// linha digitável, então o consumidor precisa compor. Geração e leitura de arquivo CNAB não são
    /// usadas neste caminho e lançam exceção explícita em vez de devolver arquivo silenciosamente
    /// inválido: se um dia a remessa por arquivo for necessária, é implementação a fazer, não a herdar.
    ///
    /// O campo livre é idêntico ao do Bradesco (agência + carteira + nosso número + conta + '0'). O DV do
    /// nosso número difere: a base leva um '0' à frente da carteira.
    /// </summary>
    internal sealed class BancoBTGPactual : IBanco
    {
        internal static Lazy<IBanco> Instance { get; } = new Lazy<IBanco>(() => new BancoBTGPactual());

        public Cedente Cedente { get; set; }
        public int Codigo { get; } = 208;
        public string Nome { get; } = "BTG Pactual";
        public string Digito { get; } = "1";
        public List<string> IdsRetornoCnab400RegistroDetalhe { get; } = new List<string> { "1" };
        public bool RemoveAcentosArquivoRemessa { get; } = true;

        private BancoBTGPactual()
        {
        }

        public void FormataCedente()
        {
            var contaBancaria = Cedente.ContaBancaria;

            if (!CarteiraFactory<BancoBTGPactual>.CarteiraEstaImplementada(contaBancaria.CarteiraComVariacaoPadrao))
                throw Boleto2NetException.CarteiraNaoImplementada(contaBancaria.CarteiraComVariacaoPadrao);

            contaBancaria.FormatarDados("PAGÁVEL EM QUALQUER BANCO, CANAIS ELETRÔNICOS OU CORRESPONDENTES.", "", "", 7);

            var codigoCedente = Cedente.Codigo;
            Cedente.Codigo = codigoCedente.Length <= 20 ? codigoCedente.PadLeft(20, '0') : throw Boleto2NetException.CodigoCedenteInvalido(codigoCedente, 20);

            Cedente.CodigoFormatado = $"{contaBancaria.Agencia} / {contaBancaria.Conta}-{contaBancaria.DigitoConta}";
        }

        public void ValidaBoleto(Boleto boleto)
        {
        }

        public void FormataNossoNumero(Boleto boleto)
        {
            var carteira = CarteiraFactory<BancoBTGPactual>.ObterCarteira(boleto.CarteiraComVariacao);
            carteira.FormataNossoNumero(boleto);
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            var carteira = CarteiraFactory<BancoBTGPactual>.ObterCarteira(boleto.CarteiraComVariacao);
            return carteira.FormataCodigoBarraCampoLivre(boleto);
        }

        #region CNAB - não utilizado no registro pela VAN

        private const string CnabNaoSuportado =
            "Geração e leitura de arquivo CNAB não estão implementadas para o BTG Pactual (208): " +
            "a cobrança deste banco é registrada pela VAN bancária, não por remessa de arquivo.";

        public string GerarHeaderRemessa(TipoArquivo tipoArquivo, int numeroArquivoRemessa, ref int numeroRegistro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public string GerarDetalheRemessa(TipoArquivo tipoArquivo, Boleto boleto, ref int numeroRegistro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public string GerarTrailerRemessa(TipoArquivo tipoArquivo, int numeroArquivoRemessa,
                                            ref int numeroRegistroGeral, decimal valorBoletoGeral,
                                            int numeroRegistroCobrancaSimples, decimal valorCobrancaSimples,
                                            int numeroRegistroCobrancaVinculada, decimal valorCobrancaVinculada,
                                            int numeroRegistroCobrancaCaucionada, decimal valorCobrancaCaucionada,
                                            int numeroRegistroCobrancaDescontada, decimal valorCobrancaDescontada)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerHeaderRetornoCNAB240(ArquivoRetorno arquivoRetorno, string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerDetalheRetornoCNAB240SegmentoT(ref Boleto boleto, string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerDetalheRetornoCNAB240SegmentoU(ref Boleto boleto, string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerHeaderRetornoCNAB400(ArquivoRetorno arquivoRetorno, string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerDetalheRetornoCNAB400Segmento1(ref Boleto boleto, string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerDetalheRetornoCNAB400Segmento7(ref Boleto boleto, string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void LerTrailerRetornoCNAB400(string registro)
            => throw new NotImplementedException(CnabNaoSuportado);

        public void SetaNumeroSequencial(int numeroSequencial)
            => throw new NotImplementedException(CnabNaoSuportado);

        public string FormatarNomeArquivoRemessa(int numeroSequencial)
            => throw new NotImplementedException(CnabNaoSuportado);

        #endregion
    }
}

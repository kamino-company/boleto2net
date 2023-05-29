using System;
using System.Collections.Generic;
using System.Web.UI;
using Boleto2Net.Exceptions;
using static System.String;

[assembly: WebResource("BoletoNet.Imagens.077.jpg", "image/jpg")]

namespace Boleto2Net
{
    internal sealed class BancoInter : IBanco
    {
        #region UtilsInter
        public static bool PossuiTransacao2(Boleto boleto)
        {
            bool possui = false;
            if (!string.IsNullOrWhiteSpace(boleto.ComplementoInstrucao2) || !string.IsNullOrWhiteSpace(boleto.ComplementoInstrucao3) || !string.IsNullOrWhiteSpace(boleto.ComplementoInstrucao4) || !string.IsNullOrWhiteSpace(boleto.ComplementoInstrucao5))
                possui = true;
            else if (boleto.ValorDesconto2 > 0 || boleto.ValorDesconto3 > 0 || boleto.PercentualDesconto2 > 0 || boleto.PercentualDesconto3 > 0)
                possui = true;
            return possui;
        }
        #endregion

        internal static Lazy<IBanco> Instance { get; } = new Lazy<IBanco>(() => new BancoInter());

        public Cedente Cedente { get; set; }

        public int Codigo { get; } = 077;

        public string Nome { get; } = "Inter";

        public string Digito { get; } = "9";

        public List<string> IdsRetornoCnab400RegistroDetalhe { get; } = new List<string>() { "1" };

        public bool RemoveAcentosArquivoRemessa { get; } = true;

        /// <summary>
        /// Informar o mesmo número informado no nome do arquivo, é preenchido pelo método FormatarNomeArquivoRemessa
        /// </summary>
        public int NumeroSequencial { get; set; }

        public void SetaNumeroSequencial(int numeroSequencial)
        {
            NumeroSequencial = numeroSequencial;
        }

        public void FormataCedente()
        {
            ContaBancaria contaBancaria = Cedente.ContaBancaria;

            if (!CarteiraFactory<BancoInter>.CarteiraEstaImplementada(contaBancaria.CarteiraComVariacaoPadrao))
                throw Boleto2NetException.CarteiraNaoImplementada(contaBancaria.CarteiraComVariacaoPadrao);

            contaBancaria.FormatarDados("PAGÁVEL EM QUALQUER BANCO ATÉ O VENCIMENTO.", "", "Ouvidoria: 0800 940 7772 / SAC - Deficiente de Fala e Audição 0800 979 70 99", 9);

            Cedente.CodigoFormatado = $"{contaBancaria.Agencia} {contaBancaria.Conta}{contaBancaria.DigitoConta}";
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            ICarteira<BancoInter> carteira = CarteiraFactory<BancoInter>.ObterCarteira(boleto.CarteiraComVariacao);
            return carteira.FormataCodigoBarraCampoLivre(boleto);
        }

        public void FormataNossoNumero(Boleto boleto)
        {
            ICarteira<BancoInter> carteira = CarteiraFactory<BancoInter>.ObterCarteira(boleto.CarteiraComVariacao);
            carteira.FormataNossoNumero(boleto);
        }

        public string FormatarNomeArquivoRemessa(int numeroSequencial)
        {
            return $"CI400_001_{NumeroSequencial.ToString().PadLeft(7, '0')}.rem";
        }

        public string GerarHeaderRemessa(TipoArquivo tipoArquivo, int numeroArquivoRemessa, ref int numeroRegistro)
        {
            try
            {
                string header = Empty;
                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB400:
                        header += GerarHeaderRemessaCNAB400(numeroArquivoRemessa, ref numeroRegistro);
                        break;
                    default:
                        throw new Exception("Tipo de arquivo inexistente.");
                }
                return header;
            }
            catch (Exception ex)
            {
                throw Boleto2NetException.ErroAoGerarRegistroHeaderDoArquivoRemessa(ex);
            }
        }

        public string GerarDetalheRemessa(TipoArquivo tipoArquivo, Boleto boleto, ref int numeroRegistro)
        {
            try
            {
                string transacaoT1 = Empty, transacaoT2 = Empty;
                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB400:
                        transacaoT1 += GerarDetalheRemessaCNAB400Transacao1(boleto, ref numeroRegistro);
                        transacaoT2 = GerarDetalheRemessaCNAB400Transacao2(boleto, ref numeroRegistro);
                        if (!IsNullOrWhiteSpace(transacaoT2))
                        {
                            transacaoT1 += Environment.NewLine;
                            transacaoT1 += transacaoT2;
                        }
                        break;
                    default:
                        throw new Exception("Tipo de arquivo inexistente.");
                }
                return transacaoT1;
            }
            catch (Exception ex)
            {
                throw Boleto2NetException.ErroAoGerarRegistroDetalheDoArquivoRemessa(ex);
            }
        }


        public string GerarTrailerRemessa(TipoArquivo tipoArquivo, int numeroBoletoRemessa, ref int numeroRegistroGeral, decimal valorBoletoGeral, 
            int numeroRegistroCobrancaSimples, decimal valorCobrancaSimples, int numeroRegistroCobrancaVinculada, decimal valorCobrancaVinculada, 
            int numeroRegistroCobrancaCaucionada, decimal valorCobrancaCaucionada, int numeroRegistroCobrancaDescontada, decimal valorCobrancaDescontada)
        {
            try
            {
                string trailer = Empty;
                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB400:
                        trailer = GerarTrailerRemessaCNAB400(numeroBoletoRemessa, ref numeroRegistroGeral);
                        break;
                    default:
                        throw new Exception("Tipo de arquivo inexistente.");
                }
                return trailer;
            }
            catch (Exception ex)
            {
                throw Boleto2NetException.ErroAoGerrarRegistroTrailerDoArquivoRemessa(ex);
            }
        }
        
        public void LerHeaderRetornoCNAB400(ArquivoRetorno arquivoRetorno, string registro)
        {
            try
            {
                if (registro.Substring(0, 9) != "02RETORNO")
                    throw new Exception("O arquivo não é do tipo \"02RETORNO\"");

                //DATA DE GERAÇÃO DATA DE GERAÇÃO DO ARQUIVO 095 100
                arquivoRetorno.DataGeracao = Utils.ToDateTime(Utils.ToInt32(registro.Substring(94, 6)).ToString("##-##-##"));

            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler HEADER do arquivo de RETORNO / CNAB 400.", ex);
            }
        }

        public void LerDetalheRetornoCNAB400Segmento1(ref Boleto boleto, string registro)
        {
            try
            {   
                //Nº Controle do Participante
                boleto.NumeroControleParticipante = registro.Substring(37, 25);

                //Carteira
                boleto.Carteira = registro.Substring(86, 3);
                boleto.TipoCarteira = TipoCarteira.CarteiraCobrancaSimples;

                //Seu Número
                boleto.SeuNumero = registro.Substring(97, 10);

                //Identificação do Título no Banco
                boleto.NossoNumero = registro.Substring(107, 10);
                boleto.NossoNumeroDV = registro.Substring(117, 1); //DV
                boleto.NossoNumeroFormatado = $"{boleto.Carteira}/{boleto.NossoNumero}-{boleto.NossoNumeroDV}";
                boleto.NossoNumero = registro.Substring(107, 11);

                //Identificação de Ocorrência
                boleto.CodigoOcorrencia = registro.Substring(89, 2);
                boleto.DescricaoOcorrencia = DescricaoOcorrenciaCnab400(boleto.CodigoOcorrencia);
                if (int.Parse(boleto.CodigoOcorrencia) == 3)
                    boleto.DescricaoOcorrencia += $" | {registro.Substring(240, 140).Trim()}";

                //Especie
                boleto.EspecieDocumento = TipoEspecieDocumento.OU;

                //Valores do Título
                if(!IsNullOrWhiteSpace(registro.Substring(124, 13)))
                    boleto.ValorTitulo = Convert.ToDecimal(registro.Substring(124, 13)) / 100;
                decimal valorPago = 0;
                if (!IsNullOrWhiteSpace(registro.Substring(159, 13)))
                    valorPago = Convert.ToDecimal(registro.Substring(159, 13));
                boleto.ValorPagoCredito = valorPago / 100;

                //Data Vencimento do Título
                boleto.DataVencimento = Utils.ToDateTime(Utils.ToInt32(registro.Substring(118, 6)).ToString("##-##-##"));

                // Data do Crédito
                boleto.DataCredito = Utils.ToDateTime(Utils.ToInt32(registro.Substring(172, 6)).ToString("##-##-##"));

                // Banco Cobrador
                boleto.BancoCobradorRecebedor = registro.Substring(137, 3);
                boleto.AgenciaCobradoraRecebedora = registro.Substring(140, 4);

                // Registro Retorno
                boleto.RegistroArquivoRetorno = boleto.RegistroArquivoRetorno + registro + Environment.NewLine;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 400.", ex);
            }
        }
        
        public void LerTrailerRetornoCNAB400(string registro) { }

        public void ValidaBoleto(Boleto boleto) { }

        #region Remessa - CNAB400
        private string GerarHeaderRemessaCNAB400(int numeroArquivoRemessa, ref int numeroRegistroGeral)
        {
            try
            {
                numeroRegistroGeral++;
                TRegistroEDI reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0002, 001, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0003, 007, 0, "REMESSA", ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0010, 002, 0, "01", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0012, 015, 0, "COBRANCA", ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0027, 020, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0047, 030, 0, Cedente.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0077, 003, 0, "077", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0080, 015, 0, "INTER", ' ');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0095, 006, 0, DateTime.Now, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0101, 010, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0111, 007, 0, NumeroSequencial, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0118, 277, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do arquivo de remessa do CNAB400.", ex);
            }
        }

        private string GerarDetalheRemessaCNAB400Transacao1(Boleto boleto, ref int numeroRegistroGeral)
        {
            try
            {
                numeroRegistroGeral++;
                TRegistroEDI reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0002, 019, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0021, 003, 0, "112", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0024, 004, 0, "0001", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0028, 010, 0, boleto.Banco.Cedente.ContaBancaria.Conta.PadLeft(10, '0'), '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0038, 025, 0, Empty, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0063, 003, 0, Empty, ' ');
                //Valor da multa informado em percentual
                if (boleto.PercentualMulta > 0) {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0066, 001, 0, "2", '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0067, 013, 0, Empty, '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0080, 004, 2, boleto.PercentualMulta, '0');
                    DateTime dataMulta = boleto.DataMulta >= boleto.DataVencimento ? boleto.DataMulta : boleto.DataVencimento.AddDays(1);
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0084, 006, 0, dataMulta, '0');
                }
                //Valor da multa informado em valor
                else if (boleto.ValorMulta > 0) {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0066, 001, 0, "1", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0067, 013, 2, boleto.ValorMulta, '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0080, 004, 0, Empty, '0');
                    DateTime dataMulta = boleto.DataMulta >= boleto.DataVencimento ? boleto.DataMulta : boleto.DataVencimento.AddDays(1);
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0084, 006, 0, dataMulta, '0');
                }
                //Sem Multa
                else {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0066, 001, 0, "0", '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0067, 013, 0, Empty, '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0080, 004, 0, Empty, '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0084, 006, 0, Empty, '0');
                }
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0090, 011, 0, boleto.NossoNumero, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0101, 008, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0109, 002, 0, "01", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0111, 010, 0, boleto.SeuNumero.PadLeft(10, '0'), '0');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0121, 006, 0, boleto.DataVencimento, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0127, 013, 2, boleto.ValorTitulo, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0140, 002, 0, boleto.DiasBaixaDevolucao, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0142, 006, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0148, 002, 0, "99", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0150, 001, 0, "N", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0151, 006, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0157, 003, 0, Empty, ' ');
                //Valor da juros/mora informado em percentual
                if (boleto.PercentualJurosDia > 0)
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0160, 001, 0, "2", '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0161, 013, 0, Empty, '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0174, 004, 2, boleto.PercentualJurosDia, '0');
                    DateTime dataJuros = boleto.DataJuros >= boleto.DataVencimento ? boleto.DataJuros : boleto.DataVencimento.AddDays(1);
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0178, 006, 0, dataJuros, '0');
                }
                //Valor da juros/mora informado em valor
                else if (boleto.ValorJurosDia > 0)
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0160, 001, 0, "1", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0161, 013, 2, boleto.ValorJurosDia, '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0174, 004, 0, Empty, '0');
                    DateTime dataJuros = boleto.DataMulta >= boleto.DataVencimento ? boleto.DataJuros : boleto.DataVencimento.AddDays(1);
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0178, 006, 0, dataJuros, '0');
                }
                //Sem juros/mora
                else
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0160, 001, 0, "0", '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0161, 013, 0, Empty, '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0174, 004, 0, Empty, '0');
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0178, 006, 0, Empty, '0');
                }
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0184, 001, 0, (int)boleto.CodigoTipoDesconto, '0');
                switch (boleto.CodigoTipoDesconto)
                {
                    case TipoDesconto.SemDesconto:
                        reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0185, 013, 0, Empty, '0');
                        reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0198, 004, 0, Empty, '0');
                        reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0202, 006, 0, Empty, '0');
                        break;
                    case TipoDesconto.ValorDataFixa:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0185, 013, 2, boleto.ValorDesconto, '0');
                        reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0198, 004, 0, Empty, '0');
                        DateTime dataDescontoValorFixa = boleto.DataDesconto < boleto.DataVencimento ? boleto.DataDesconto : boleto.DataVencimento;
                        reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0202, 006, 0, dataDescontoValorFixa, '0');
                        break;
                    case TipoDesconto.ValorDiaCorrido:
                    case TipoDesconto.ValorDiaUtil:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0185, 013, 2, boleto.ValorDesconto, '0');
                        reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0198, 004, 0, Empty, '0');
                        DateTime dataDescontoValor = boleto.DataDesconto < boleto.DataVencimento ? boleto.DataDesconto : boleto.DataVencimento.AddDays(-1);
                        reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0202, 006, 0, dataDescontoValor, '0');
                        break;
                    case TipoDesconto.PercDataFixa:
                    case TipoDesconto.PercDiaCorrido:
                    case TipoDesconto.PercDiaUtil:
                        reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0185, 013, 0, Empty, '0');
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0198, 004, 2, boleto.PercentualDesconto, '0');
                        DateTime dataDescontoPerc = boleto.DataDesconto < boleto.DataVencimento ? boleto.DataDesconto : boleto.DataVencimento.AddDays(-1);
                        reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0202, 006, 0, dataDescontoPerc, '0');
                        break;
                    default:
                        break;
                }
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0208, 013, 0, boleto.ValorAbatimento, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0221, 002, 0, boleto.Sacado.TipoCPFCNPJ("00"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0223, 014, 0, boleto.Sacado.CPFCNPJ.PadLeft(14, '0'), '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0237, 040, 0, boleto.Sacado.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0277, 040, 0, boleto.Sacado.Endereco.LogradouroEndereco, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0317, 008, 0, boleto.Sacado.Endereco.CEP, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0325, 070, 0, boleto.ComplementoInstrucao1, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do arquivo CNAB400 - Registro 1.", ex);
            }
        }

        private string GerarDetalheRemessaCNAB400Transacao2(Boleto boleto, ref int numeroRegistroGeral)
        {
            try
            {
                if (!PossuiTransacao2(boleto))
                    return "";

                numeroRegistroGeral++;
                TRegistroEDI reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, '2', '2');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0002, 078, 0, boleto.ComplementoInstrucao2.TrimOrNull(), ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0080, 078, 0, boleto.ComplementoInstrucao3.TrimOrNull(), ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0158, 078, 0, boleto.ComplementoInstrucao4.TrimOrNull(), ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0236, 078, 0, boleto.ComplementoInstrucao5.TrimOrNull(), ' ');
                DateTime dataDesconto = boleto.DataLimiteDesconto2 < boleto.DataVencimento ? boleto.DataLimiteDesconto2 : boleto.DataVencimento.AddDays(-1);
                if (dataDesconto.Year == 1)
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliDireita______, 0314, 006, 0, Empty, '0');
                else
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0314, 006, 0, dataDesconto, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0320, 013, 0, boleto.ValorDesconto2, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0333, 004, 0, boleto.PercentualDesconto2, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliDireita______, 0337, 010, 0, Empty, ' ');
                dataDesconto = boleto.DataLimiteDesconto3 < boleto.DataVencimento ? boleto.DataLimiteDesconto3 : boleto.DataVencimento.AddDays(-1);
                if(dataDesconto.Year == 1)
                    reg.Adicionar(TTiposDadoEDI.ediAlphaAliDireita______, 0347, 006, 0, Empty, '0');
                else
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0347, 006, 0, dataDesconto, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0353, 013, 0, boleto.ValorDesconto3, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0366, 004, 0, boleto.PercentualDesconto3, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliDireita______, 0370, 010, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0380, 011, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliDireita______, 0391, 004, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do arquivo CNAB400 - Registro 2.", ex);
            }
        }

        private string GerarTrailerRemessaCNAB400(int numeroBoletoRemessa, ref int numeroRegistroGeral)
        {
            try
            {
                numeroRegistroGeral++;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, "9", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0002, 006, 0, numeroBoletoRemessa, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0008, 387, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do registro TRAILER do arquivo de REMESSA.", ex);
            }
        }
        #endregion

        #region Retorno - CNAB400
        private string DescricaoOcorrenciaCnab400(string codigo)
        {
            switch (codigo)
            {
                case "02":
                    return "Em aberto | Boleto registrado";
                case "03":
                    return "Erro";
                case "06":
                    return "Pago";
                case "07":
                    return "Baixado";                
                default:
                    return "";
            }
        }
        #endregion

        #region Not Implemented Methods
        public void LerHeaderRetornoCNAB240(ArquivoRetorno arquivoRetorno, string registro)
        {
            throw new NotImplementedException();
        }
        public void LerDetalheRetornoCNAB240SegmentoT(ref Boleto boleto, string registro)
        {
            throw new NotImplementedException();
        }
        public void LerDetalheRetornoCNAB240SegmentoU(ref Boleto boleto, string registro)
        {
            throw new NotImplementedException();
        }
        public void LerDetalheRetornoCNAB400Segmento7(ref Boleto boleto, string registro)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Web.UI;
using Boleto2Net.Exceptions;
using static System.String;

[assembly: WebResource("BoletoNet.Imagens.422.jpg", "image/jpg")]

namespace Boleto2Net
{
    internal sealed class BancoSafra : IBanco
    {
        internal static Lazy<IBanco> Instance { get; } = new Lazy<IBanco>(() => new BancoSafra());

        public Cedente Cedente { get; set; }
        public int Codigo { get; } = 422;
        public string Nome { get; } = "Safra";
        public string Digito { get; } = "7";
        public List<string> IdsRetornoCnab400RegistroDetalhe { get; } = new List<string> { "1" };
        public bool RemoveAcentosArquivoRemessa { get; } = true;
        private int NumeroArquivoRemessaCNAB400 { get; set; } // Safra repete o número do arquivo remessa na linha de detalhe (CNAB 400)

        public void FormataCedente()
        {
            var contaBancaria = Cedente.ContaBancaria;

            if (!CarteiraFactory<BancoSafra>.CarteiraEstaImplementada(contaBancaria.CarteiraComVariacaoPadrao))
                throw Boleto2NetException.CarteiraNaoImplementada(contaBancaria.CarteiraComVariacaoPadrao);

            contaBancaria.FormatarDados("Pagável em qualquer Banco do Sistema de Compensação","", "", 6);

            Cedente.CodigoFormatado = $"{contaBancaria.Agencia}-{contaBancaria.DigitoAgencia} / {contaBancaria.Conta}-{contaBancaria.DigitoConta}";
        }

        public void ValidaBoleto(Boleto boleto)
        {
        }

        public void FormataNossoNumero(Boleto boleto)
        {
            var carteira = CarteiraFactory<BancoSafra>.ObterCarteira(boleto.CarteiraComVariacao);
            carteira.FormataNossoNumero(boleto);
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            var carteira = CarteiraFactory<BancoSafra>.ObterCarteira(boleto.CarteiraComVariacao);
            return carteira.FormataCodigoBarraCampoLivre(boleto);
        }

        public string FormatarNomeArquivoRemessa(int numeroSequencial)
        {
            return "";
        }
        public string GerarHeaderRemessa(TipoArquivo tipoArquivo, int numeroArquivoRemessa, ref int numeroRegistroGeral)
        {
            this.NumeroArquivoRemessaCNAB400 = numeroArquivoRemessa;
            try
            {
                var header = Empty;
                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB240:
                        // Cabeçalho do Arquivo
                        header += GerarHeaderRemessaCNAB240(numeroArquivoRemessa);
                        // Cabeçalho do Lote
                        header += Environment.NewLine;
                        header += GerarHeaderLoteRemessaCNAB240();
                        break;
                    case TipoArquivo.CNAB400:
                        header += GerarHeaderRemessaCNAB400(numeroArquivoRemessa, ref numeroRegistroGeral);
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
                string detalhe = Empty, strline = Empty;
                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB240:
                        // Segmento P (Obrigatório)
                        detalhe += GerarDetalheSegmentoPRemessaCNAB240(boleto, ref numeroRegistro);
                        // Segmento Q (Obrigatório)
                        detalhe += Environment.NewLine;
                        detalhe += GerarDetalheSegmentoQRemessaCNAB240(boleto, ref numeroRegistro);
                        // Segmento R (Opcional: só quando há multa)
                        strline = GerarDetalheSegmentoRRemessaCNAB240(boleto, ref numeroRegistro);
                        if (!IsNullOrWhiteSpace(strline))
                        {
                            detalhe += Environment.NewLine;
                            detalhe += strline;
                        }
                        break;
                    case TipoArquivo.CNAB400:
                        detalhe += GerarDetalheRemessaCNAB400Registro1(boleto, ref numeroRegistro);
                        break;
                    default:
                        throw new Exception("Tipo de arquivo inexistente.");
                }
                return detalhe;
            }
            catch (Exception ex)
            {
                throw Boleto2NetException.ErroAoGerarRegistroDetalheDoArquivoRemessa(ex);
            }
        }

        public string GerarTrailerRemessa(TipoArquivo tipoArquivo, int numeroArquivoRemessa,
            ref int numeroRegistroGeral, decimal valorBoletoGeral,
            int numeroRegistroCobrancaSimples, decimal valorCobrancaSimples,
            int numeroRegistroCobrancaVinculada, decimal valorCobrancaVinculada,
            int numeroRegistroCobrancaCaucionada, decimal valorCobrancaCaucionada,
            int numeroRegistroCobrancaDescontada, decimal valorCobrancaDescontada)
        {
            try
            {
                var trailer = Empty;
                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB240:
                        // Trailler do Lote
                        trailer += GerarTrailerLoteRemessaCNAB240(numeroRegistroGeral,
                            numeroRegistroCobrancaSimples, valorCobrancaSimples,
                            numeroRegistroCobrancaVinculada, valorCobrancaVinculada);
                        // Trailler do Arquivo
                        trailer += Environment.NewLine;
                        trailer += GerarTrailerRemessaCNAB240(numeroRegistroGeral);
                        break;
                    case TipoArquivo.CNAB400:
                        trailer = GerarTrailerRemessaCNAB400(ref numeroRegistroGeral, ref valorBoletoGeral);
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

        public void SetaNumeroSequencial(int numeroSequencial)
        {
            throw new NotImplementedException();
        }

        #region Retorno - CNAB240
        // Guia Safra CNAB 240: o retorno segue o padrão FEBRABAN 240 (lote v.060). O guia destaca o código de
        // movimento no Segmento T (016-017) e o motivo da ocorrência (214-223); o nosso número Safra ocupa 038-046.
        public void LerHeaderRetornoCNAB240(ArquivoRetorno arquivoRetorno, string registro)
        {
            try
            {
                if (registro.Substring(0, 3) != "422")
                    throw new Exception("O arquivo não é do Banco Safra (422).");

                arquivoRetorno.Banco.Cedente = new Cedente();
                //018 Tipo de inscrição / 019-032 Número de inscrição da empresa
                arquivoRetorno.Banco.Cedente.CPFCNPJ = registro.Substring(17, 1) == "1" ? registro.Substring(21, 11) : registro.Substring(18, 14);
                //073-102 Nome da empresa
                arquivoRetorno.Banco.Cedente.Nome = registro.Substring(72, 30).Trim();

                arquivoRetorno.Banco.Cedente.ContaBancaria = new ContaBancaria();
                //053-057 Agência (no Safra, 4 posições da agência + DV, como a remessa grava) / 058 em branco
                //059-070 Conta / 071 DV da conta
                arquivoRetorno.Banco.Cedente.ContaBancaria.Agencia = registro.Substring(52, 4);
                arquivoRetorno.Banco.Cedente.ContaBancaria.DigitoAgencia = registro.Substring(56, 1);
                arquivoRetorno.Banco.Cedente.ContaBancaria.Conta = registro.Substring(58, 12);
                arquivoRetorno.Banco.Cedente.ContaBancaria.DigitoConta = registro.Substring(70, 1);

                //144-151 Data de geração do arquivo (DDMMAAAA)
                arquivoRetorno.DataGeracao = Utils.ToDateTime(Utils.ToInt32(registro.Substring(143, 8)).ToString("##-##-####"));
                //158-163 Número sequencial do arquivo (NSA)
                arquivoRetorno.NumeroSequencial = Utils.ToInt32(registro.Substring(157, 6));
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler HEADER do arquivo de RETORNO / CNAB 240.", ex);
            }
        }

        public void LerDetalheRetornoCNAB240SegmentoT(ref Boleto boleto, string registro)
        {
            try
            {
                //016-017 Código de movimento retorno
                boleto.CodigoOcorrencia = registro.Substring(15, 2);
                boleto.DescricaoOcorrencia = Cnab.OcorrenciaCnab240(boleto.CodigoOcorrencia);
                //214-223 Motivo da ocorrência (rejeições)
                boleto.CodigoOcorrenciaAuxiliar = registro.Substring(213, 10);

                //038-046 Nosso número Safra: 8 dígitos + DV (047-057 em branco)
                boleto.NossoNumero = registro.Substring(37, 8);
                boleto.NossoNumeroDV = registro.Substring(45, 1);
                boleto.NossoNumeroFormatado = $"{boleto.NossoNumero}-{boleto.NossoNumeroDV}";

                //058 Carteira: 1 = Cobrança Simples / 2 = Cobrança Vinculada
                boleto.Carteira = registro.Substring(57, 1);
                boleto.TipoCarteira = boleto.Carteira == "2"
                    ? TipoCarteira.CarteiraCobrancaVinculada
                    : TipoCarteira.CarteiraCobrancaSimples;

                //059-073 Número do documento (seu número)
                boleto.NumeroDocumento = registro.Substring(58, 15).Trim();
                boleto.EspecieDocumento = TipoEspecieDocumento.NaoDefinido;

                //074-081 Vencimento / 082-096 Valor do título
                boleto.DataVencimento = Utils.ToDateTime(Utils.ToInt32(registro.Substring(73, 8)).ToString("##-##-####"));
                boleto.ValorTitulo = Convert.ToDecimal(registro.Substring(81, 15)) / 100;

                //097-099 Banco cobrador / 100-104 Agência cobradora
                boleto.BancoCobradorRecebedor = registro.Substring(96, 3);
                boleto.AgenciaCobradoraRecebedora = registro.Substring(99, 5);

                //106-130 Identificação do título na empresa
                boleto.NumeroControleParticipante = registro.Substring(105, 25).Trim();

                //133 Tipo de inscrição / 134-148 Número de inscrição / 149-188 Nome do pagador
                boleto.Sacado = new Sacado();
                var inscricaoPagador = registro.Substring(133, 15);
                if (registro.Substring(132, 1) == "1")
                    boleto.Sacado.CPFCNPJ = inscricaoPagador.Substring(4, 11);
                else if (registro.Substring(132, 1) == "2")
                    boleto.Sacado.CPFCNPJ = inscricaoPagador.Substring(1, 14);
                boleto.Sacado.Nome = registro.Substring(148, 40).Trim();

                //199-213 Valor da tarifa
                boleto.ValorTarifas = Convert.ToDecimal(registro.Substring(198, 15)) / 100;

                // Registro Retorno
                boleto.RegistroArquivoRetorno = boleto.RegistroArquivoRetorno + registro + Environment.NewLine;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 240 / T.", ex);
            }
        }

        public void LerDetalheRetornoCNAB240SegmentoU(ref Boleto boleto, string registro)
        {
            try
            {
                //018-032 Juros/multa/encargos / 033-047 Desconto / 048-062 Abatimento / 063-077 IOF
                boleto.ValorJurosDia = Convert.ToDecimal(registro.Substring(17, 15)) / 100;
                boleto.ValorDesconto = Convert.ToDecimal(registro.Substring(32, 15)) / 100;
                boleto.ValorAbatimento = Convert.ToDecimal(registro.Substring(47, 15)) / 100;
                boleto.ValorIOF = Convert.ToDecimal(registro.Substring(62, 15)) / 100;
                //078-092 Valor pago / 093-107 Valor líquido creditado
                boleto.ValorPago = Convert.ToDecimal(registro.Substring(77, 15)) / 100;
                boleto.ValorPagoCredito = Convert.ToDecimal(registro.Substring(92, 15)) / 100;
                //108-122 Outras despesas / 123-137 Outros créditos
                boleto.ValorOutrasDespesas = Convert.ToDecimal(registro.Substring(107, 15)) / 100;
                boleto.ValorOutrosCreditos = Convert.ToDecimal(registro.Substring(122, 15)) / 100;

                //138-145 Data da ocorrência / 146-153 Data do crédito
                boleto.DataProcessamento = Utils.ToDateTime(Utils.ToInt32(registro.Substring(137, 8)).ToString("##-##-####"));
                boleto.DataCredito = Utils.ToDateTime(Utils.ToInt32(registro.Substring(145, 8)).ToString("##-##-####"));

                // Registro Retorno
                boleto.RegistroArquivoRetorno = boleto.RegistroArquivoRetorno + registro + Environment.NewLine;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 240 / U.", ex);
            }
        }
        #endregion

        public void LerHeaderRetornoCNAB400(ArquivoRetorno arquivoRetorno, string registro)
        {
            try
            {
                if (registro.Substring(0, 9) != "02RETORNO")
                    throw new Exception("O arquivo não é do tipo \"02RETORNO\"");

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
                boleto.Carteira = registro.Substring(107, 1);
                if (boleto.Carteira == "2")
                    boleto.TipoCarteira = TipoCarteira.CarteiraCobrancaVinculada;
                else
                    boleto.TipoCarteira = TipoCarteira.CarteiraCobrancaSimples;

                //Identificação do Título no Banco
                boleto.NossoNumero = registro.Substring(62, 8);
                boleto.NossoNumeroDV = registro.Substring(70, 1); //DV
                boleto.NossoNumeroFormatado = $"{boleto.NossoNumero}-{boleto.NossoNumeroDV}";

                //Identificação de Ocorrência
                boleto.CodigoOcorrencia = registro.Substring(108, 2);
                boleto.DescricaoOcorrencia = DescricaoOcorrenciaCnab400(boleto.CodigoOcorrencia);
                boleto.CodigoOcorrenciaAuxiliar = registro.Substring(104, 3);

                //Número do Documento
                boleto.NumeroDocumento = registro.Substring(116, 10);
                boleto.EspecieDocumento = AjustaEspecieCnab400(registro.Substring(173, 2));

                //Valores do Título
                boleto.ValorTitulo = Convert.ToDecimal(registro.Substring(152, 13)) / 100;
                boleto.ValorTarifas = Convert.ToDecimal(registro.Substring(175, 13)) / 100;
                boleto.ValorOutrasDespesas = Convert.ToDecimal(registro.Substring(188, 13)) / 100;
                boleto.ValorIOF = Convert.ToDecimal(registro.Substring(214, 13)) / 100;
                boleto.ValorAbatimento = Convert.ToDecimal(registro.Substring(227, 13)) / 100;
                boleto.ValorDesconto = Convert.ToDecimal(registro.Substring(240, 13)) / 100;
                boleto.ValorPagoCredito = Convert.ToDecimal(registro.Substring(253, 13)) / 100;
                boleto.ValorJurosDia = Convert.ToDecimal(registro.Substring(266, 13)) / 100;
                boleto.ValorOutrosCreditos = Convert.ToDecimal(registro.Substring(279, 13)) / 100;

                //Data Ocorrência no Banco
                boleto.DataProcessamento = Utils.ToDateTime(Utils.ToInt32(registro.Substring(110, 6)).ToString("##-##-##"));

                //Data Vencimento do Título
                boleto.DataVencimento = Utils.ToDateTime(Utils.ToInt32(registro.Substring(146, 6)).ToString("##-##-##"));

                // Data do Crédito
                boleto.DataCredito = Utils.ToDateTime(Utils.ToInt32(registro.Substring(295, 6)).ToString("##-##-##"));

                // Registro Retorno
                boleto.RegistroArquivoRetorno = boleto.RegistroArquivoRetorno + registro + Environment.NewLine;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 400.", ex);
            }
        }

        public void LerDetalheRetornoCNAB400Segmento7(ref Boleto boleto, string registro)
        {
            throw new NotImplementedException();
        }

        public void LerTrailerRetornoCNAB400(string registro)
        {
        }

        #region Remessa - CNAB240
        // Layout padrão Safra CNAB 240 ("Guia para implantação de transmissão e troca de arquivos", Mesa de Implantação
        // Cash Management). O Safra não trata convênio, e a agência mantenedora é informada com 5 dígitos (agência + DV),
        // a mesma composição do campo livre do código de barras; a posição do DV da agência fica em branco.
        private string AgenciaCNAB240 => $"{Cedente.ContaBancaria.Agencia}{Cedente.ContaBancaria.DigitoAgencia}";

        private string GerarHeaderRemessaCNAB240(int numeroArquivoRemessa)
        {
            try
            {
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "0000", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0009, 009, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 001, 0, Cedente.TipoCPFCNPJ("0"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0019, 014, 0, Cedente.CPFCNPJ, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0033, 020, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0053, 005, 0, AgenciaCNAB240, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0058, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0059, 012, 0, Cedente.ContaBancaria.Conta, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0071, 001, 0, Cedente.ContaBancaria.DigitoConta, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0072, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0073, 030, 0, Cedente.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0103, 030, 0, "BANCO SAFRA S/A", ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0133, 010, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0143, 001, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0144, 008, 0, DateTime.Now, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0152, 006, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0158, 006, 0, numeroArquivoRemessa, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0164, 003, 0, "087", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0167, 005, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0172, 020, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0192, 020, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0212, 029, 0, Empty, ' ');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do arquivo de remessa do CNAB240.", ex);
            }
        }

        private string GerarHeaderLoteRemessaCNAB240()
        {
            try
            {
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "0001", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0009, 001, 0, "R", ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0010, 002, 0, "01", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0012, 002, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0014, 003, 0, "060", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0017, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 001, 0, Cedente.TipoCPFCNPJ("0"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0019, 015, 0, Cedente.CPFCNPJ, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0034, 020, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0054, 005, 0, AgenciaCNAB240, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0059, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0060, 012, 0, Cedente.ContaBancaria.Conta, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0072, 001, 0, Cedente.ContaBancaria.DigitoConta, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0073, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0074, 030, 0, Cedente.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0104, 040, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0144, 040, 0, Empty, ' ');
                // O guia manda repetir aqui o sequencial do lote (004-007).
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0184, 008, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0192, 008, 0, DateTime.Now, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0200, 008, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0208, 033, 0, Empty, ' ');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do lote no arquivo de remessa do CNAB240.", ex);
            }
        }

        private string GerarDetalheSegmentoPRemessaCNAB240(Boleto boleto, ref int numeroRegistroGeral)
        {
            try
            {
                numeroRegistroGeral++;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "0001", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "3", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0009, 005, 0, numeroRegistroGeral, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0014, 001, 0, "P", ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0015, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0016, 002, 0, boleto.CodigoOcorrencia, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 005, 0, AgenciaCNAB240, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0023, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0024, 012, 0, Cedente.ContaBancaria.Conta, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0036, 001, 0, Cedente.ContaBancaria.DigitoConta, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0037, 001, 0, Empty, ' ');
                // Cobrança direta: o nosso número Safra tem 9 posições (8 dígitos + DV) em 038-046; 047-057 em branco.
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0038, 009, 0, $"{boleto.NossoNumero}{boleto.NossoNumeroDV}", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0047, 011, 0, Empty, ' ');
                switch (boleto.TipoCarteira)
                {
                    case TipoCarteira.CarteiraCobrancaSimples:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0058, 001, 0, "1", '0');
                        break;
                    case TipoCarteira.CarteiraCobrancaVinculada:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0058, 001, 0, "2", '0');
                        break;
                    default:
                        throw new Exception($"Tipo de carteira não aceito pelo Safra no CNAB240: {boleto.TipoCarteira}.");
                }
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0059, 001, 0, "1", '0'); // Cobrança registrada
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0060, 001, 0, "2", '0'); // Escritural
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0061, 001, 0, "2", '0'); // Cliente emite o boleto
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0062, 001, 0, "2", '0'); // Cliente distribui o boleto
                // O Safra trata só 10 caracteres do seu número (063-072).
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0063, 010, 0, boleto.NumeroDocumento, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0073, 005, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0078, 008, 0, boleto.DataVencimento, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0086, 015, 2, boleto.ValorTitulo, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0101, 005, 0, AgenciaCNAB240, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0106, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0107, 002, 0, EspecieCNAB240(boleto.EspecieDocumento), '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0109, 001, 0, "N", ' ');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0110, 008, 0, boleto.DataEmissao, '0');

                var jurosDia = JurosDiaCNAB240(boleto);
                if (jurosDia > 0)
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0118, 001, 0, "1", '0'); // Valor por dia
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0119, 008, 0, boleto.DataJuros == DateTime.MinValue ? boleto.DataVencimento.AddDays(1) : boleto.DataJuros, '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0127, 015, 2, jurosDia, '0');
                }
                else
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0118, 001, 0, "3", '0'); // Isento
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0119, 008, 0, "0", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0127, 015, 0, "0", '0');
                }

                if (boleto.ValorDesconto > 0)
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0142, 001, 0, "1", '0'); // Valor fixo até a data informada
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0143, 008, 0, boleto.DataDesconto == DateTime.MinValue ? boleto.DataVencimento : boleto.DataDesconto, '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0151, 015, 2, boleto.ValorDesconto, '0');
                }
                else
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0142, 001, 0, "0", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0143, 008, 0, "0", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0151, 015, 0, "0", '0');
                }

                // IOF só para seguradoras; o Safra não aceita abatimento na entrada do título.
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0166, 015, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0181, 015, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0196, 025, 0, boleto.NumeroControleParticipante, ' ');

                switch (boleto.CodigoProtesto)
                {
                    case TipoCodigoProtesto.ProtestarDiasCorridos:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0221, 001, 0, "1", '0');
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0222, 002, 0, boleto.DiasProtesto, '0');
                        break;
                    case TipoCodigoProtesto.ProtestarDiasUteis:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0221, 001, 0, "2", '0');
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0222, 002, 0, boleto.DiasProtesto, '0');
                        break;
                    default:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0221, 001, 0, "3", '0'); // Não protestar
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0222, 002, 0, "0", '0');
                        break;
                }

                if (boleto.CodigoBaixaDevolucao == TipoCodigoBaixaDevolucao.BaixarDevolver)
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0224, 001, 0, "1", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0225, 003, 0, boleto.DiasBaixaDevolucao, '0');
                }
                else
                {
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0224, 001, 0, "2", '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0225, 003, 0, "0", '0');
                }

                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0228, 002, 0, "09", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0230, 010, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0240, 001, 0, "1", '0'); // Não autoriza pagamento parcial
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do Segmento P no arquivo de remessa do CNAB240.", ex);
            }
        }

        private string GerarDetalheSegmentoQRemessaCNAB240(Boleto boleto, ref int numeroRegistroGeral)
        {
            try
            {
                numeroRegistroGeral++;
                var cep = (boleto.Sacado.Endereco.CEP ?? Empty).Replace("-", "").Replace(".", "").PadLeft(8, '0');
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "0001", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "3", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0009, 005, 0, numeroRegistroGeral, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0014, 001, 0, "Q", ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0015, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0016, 002, 0, boleto.CodigoOcorrencia, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 001, 0, boleto.Sacado.TipoCPFCNPJ("0"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0019, 015, 0, boleto.Sacado.CPFCNPJ, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0034, 040, 0, boleto.Sacado.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0074, 040, 0, boleto.Sacado.Endereco.FormataLogradouro(40), ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0114, 015, 0, boleto.Sacado.Endereco.Bairro, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0129, 005, 0, cep.Substring(0, 5), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0134, 003, 0, cep.Substring(5, 3), '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0137, 015, 0, boleto.Sacado.Endereco.Cidade, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0152, 002, 0, boleto.Sacado.Endereco.UF, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0154, 001, 0, boleto.Avalista.TipoCPFCNPJ("0"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0155, 015, 0, boleto.Avalista.CPFCNPJ, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0170, 040, 0, boleto.Avalista.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0210, 003, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0213, 020, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0233, 008, 0, Empty, ' ');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do Segmento Q no arquivo de remessa do CNAB240.", ex);
            }
        }

        private string GerarDetalheSegmentoRRemessaCNAB240(Boleto boleto, ref int numeroRegistroGeral)
        {
            try
            {
                // O Safra só aceita multa em percentual (código 2); sem multa o segmento R não é gerado.
                var percentualMulta = PercentualMultaCNAB240(boleto);
                if (percentualMulta <= 0)
                    return Empty;

                numeroRegistroGeral++;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "0001", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "3", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0009, 005, 0, numeroRegistroGeral, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0014, 001, 0, "R", ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0015, 001, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0016, 002, 0, boleto.CodigoOcorrencia, '0');
                // Descontos 2 e 3: o Safra não trata, informar zeros.
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 048, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0066, 001, 0, "2", '0');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAAAA_________, 0067, 008, 0, boleto.DataMulta == DateTime.MinValue ? boleto.DataVencimento.AddDays(1) : boleto.DataMulta, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0075, 015, 2, percentualMulta, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0090, 151, 0, Empty, ' ');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do Segmento R no arquivo de remessa do CNAB240.", ex);
            }
        }

        private string GerarTrailerLoteRemessaCNAB240(int numeroRegistroGeral,
            int numeroRegistroCobrancaSimples, decimal valorCobrancaSimples,
            int numeroRegistroCobrancaVinculada, decimal valorCobrancaVinculada)
        {
            try
            {
                // Registros do lote = segmentos de detalhe + header e trailer do lote (padrão FEBRABAN).
                var numeroRegistrosNoLote = numeroRegistroGeral + 2;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "0001", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "5", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0009, 009, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 006, 0, numeroRegistrosNoLote, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0024, 006, 0, numeroRegistroCobrancaSimples, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0030, 017, 2, valorCobrancaSimples, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0047, 006, 0, numeroRegistroCobrancaVinculada, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0053, 017, 2, valorCobrancaVinculada, '0');
                // Caucionada e descontada: o Safra não trata, informar zeros.
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0070, 029, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0099, 142, 0, Empty, ' ');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar TRAILER do lote no arquivo de remessa do CNAB240.", ex);
            }
        }

        private string GerarTrailerRemessaCNAB240(int numeroRegistroGeral)
        {
            try
            {
                // Registros do arquivo = segmentos de detalhe + header/trailer do lote + header/trailer do arquivo.
                var numeroRegistrosNoArquivo = numeroRegistroGeral + 4;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 004, 0, "9999", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0008, 001, 0, "9", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0009, 009, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 006, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0024, 006, 0, numeroRegistrosNoArquivo, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0030, 211, 0, Empty, ' ');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar TRAILER do arquivo de remessa do CNAB240.", ex);
            }
        }

        private static decimal JurosDiaCNAB240(Boleto boleto)
        {
            if (boleto.ValorJurosDia > 0)
                return boleto.ValorJurosDia;
            if (boleto.PercentualJurosDia > 0)
                return Math.Round(boleto.ValorTitulo * boleto.PercentualJurosDia / 100, 2);
            return 0;
        }

        private static decimal PercentualMultaCNAB240(Boleto boleto)
        {
            if (boleto.PercentualMulta > 0)
                return boleto.PercentualMulta;
            if (boleto.ValorMulta > 0 && boleto.ValorTitulo > 0)
                return Math.Round(boleto.ValorMulta * 100 / boleto.ValorTitulo, 2);
            return 0;
        }

        // Espécies aceitas pelo Safra no CNAB240; qualquer outra (inclusive não definida) vai como 99 - Outros.
        private static string EspecieCNAB240(TipoEspecieDocumento especieDocumento)
        {
            switch (especieDocumento)
            {
                case TipoEspecieDocumento.DM:
                    return "02";
                case TipoEspecieDocumento.DS:
                    return "04";
                case TipoEspecieDocumento.NP:
                    return "12";
                case TipoEspecieDocumento.RC:
                    return "17";
                case TipoEspecieDocumento.NF:
                    return "23";
                default:
                    return "99";
            }
        }
        #endregion

        private string GerarHeaderRemessaCNAB400(int numeroArquivoRemessa, ref int numeroRegistroGeral)
        {
            try
            {
                var numeroArquivoRemessa3Digitos = numeroArquivoRemessa.ToString().PadLeft(3, '0');
                numeroArquivoRemessa3Digitos = numeroArquivoRemessa3Digitos.Substring(numeroArquivoRemessa3Digitos.Length - 3);
                numeroRegistroGeral++;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0002, 001, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0003, 007, 0, "REMESSA", ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0010, 002, 0, "01", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0012, 015, 0, "COBRANCA", ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0027, 004, 0, Cedente.ContaBancaria.Agencia, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0031, 001, 0, Cedente.ContaBancaria.DigitoAgencia, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0032, 002, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0034, 006, 0, Cedente.ContaBancaria.Conta, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0040, 001, 0, Cedente.ContaBancaria.DigitoConta, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0041, 006, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0047, 030, 0, Cedente.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0077, 003, 0, "422", ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0080, 015, 0, "BANCO SAFRA", ' ');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0095, 006, 0, DateTime.Now, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0101, 291, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0392, 003, 0, numeroArquivoRemessa3Digitos, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do arquivo de remessa do CNAB400.", ex);
            }
        }

        private string GerarDetalheRemessaCNAB400Registro1(Boleto boleto, ref int numeroRegistroGeral)
        {
            try
            {
                var numeroArquivoRemessa3Digitos = this.NumeroArquivoRemessaCNAB400.ToString().PadLeft(3, '0');
                numeroArquivoRemessa3Digitos = numeroArquivoRemessa3Digitos.Substring(numeroArquivoRemessa3Digitos.Length - 3);
                numeroRegistroGeral++;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, "1", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0002, 002, 0, boleto.Banco.Cedente.TipoCPFCNPJ("00"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0004, 014, 0, boleto.Banco.Cedente.CPFCNPJ, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0018, 004, 0, Cedente.ContaBancaria.Agencia, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0022, 001, 0, Cedente.ContaBancaria.DigitoAgencia, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0023, 002, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0025, 006, 0, Cedente.ContaBancaria.Conta, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0031, 001, 0, Cedente.ContaBancaria.DigitoConta, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0032, 006, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0038, 025, 0, boleto.NumeroControleParticipante, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0063, 008, 0, boleto.NossoNumero, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0071, 001, 0, boleto.NossoNumeroDV, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0072, 030, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0102, 001, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0103, 002, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0105, 001, 0, Empty, ' ');

                if (boleto.CodigoInstrucao2 == "10")
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0107, 002, 0, boleto.DiasProtesto, '0');
                else
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0107, 002, 0, "0", '0');

                switch (boleto.TipoCarteira)
                {
                    case TipoCarteira.CarteiraCobrancaSimples:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0108, 001, 0, "1", '0');
                        break;
                    case TipoCarteira.CarteiraCobrancaVinculada:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0108, 001, 0, "2", '0');
                        break;
                    default:
                        reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0108, 001, 0, "?", '0');
                        break;
                }

                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0109, 002, 0, boleto.CodigoOcorrencia, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0111, 010, 0, boleto.NumeroDocumento, ' ');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0121, 006, 0, boleto.DataVencimento, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0127, 013, 2, boleto.ValorTitulo, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0140, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0143, 005, 0, "0", '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0148, 002, 0, AjustaEspecieCnab400(boleto.EspecieDocumento), '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0150, 001, 0, boleto.Aceite, ' ');
                reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0151, 006, 0, boleto.DataEmissao, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0157, 002, 0, boleto.CodigoInstrucao1, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0159, 002, 0, boleto.CodigoInstrucao2, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0161, 013, 2, boleto.ValorJurosDia, '0');

                if (boleto.ValorDesconto == 0)
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0174, 006, 0, "0", '0'); // Sem Desconto
                else
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0174, 006, 0, boleto.DataDesconto, '0'); // Com Desconto

                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0180, 013, 2, boleto.ValorDesconto, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0193, 013, 2, boleto.ValorIOF, '0');

                if (boleto.CodigoOcorrencia == "01" && boleto.CodigoInstrucao1 == "16")
                {
                    // Conforme manual, item 6.1.8, quando operação for "entrada de título" e a primeira ocorrência for "16", utiliza campo para informar Multa
                    reg.Adicionar(TTiposDadoEDI.ediDataDDMMAA___________, 0206, 006, 0, boleto.DataMulta, ' ');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0212, 004, 2, boleto.PercentualMulta, '0');
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0216, 003, 0, "000", '0');
                }
                else
                    reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0206, 013, 2, boleto.ValorAbatimento, '0');

                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0219, 002, 0, boleto.Sacado.TipoCPFCNPJ("00"), '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0221, 014, 0, boleto.Sacado.CPFCNPJ, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0235, 040, 0, boleto.Sacado.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0275, 040, 0, boleto.Sacado.Endereco.FormataLogradouro(40), ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0315, 010, 0, boleto.Sacado.Endereco.Bairro, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0325, 002, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0327, 008, 0, boleto.Sacado.Endereco.CEP.Replace("-", ""), '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0335, 015, 0, boleto.Sacado.Endereco.Cidade, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0350, 002, 0, boleto.Sacado.Endereco.UF, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0352, 030, 0, boleto.Avalista.Nome, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0382, 007, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0389, 003, 0, "422", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0392, 003, 0, numeroArquivoRemessa3Digitos, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do arquivo CNAB400 - Registro 1.", ex);
            }
        }

        private string GerarTrailerRemessaCNAB400(ref int numeroRegistroGeral, ref decimal valorBoletoGeral)
        {
            try
            {
                var numeroArquivoRemessa3Digitos = this.NumeroArquivoRemessaCNAB400.ToString().PadLeft(3, '0');
                numeroArquivoRemessa3Digitos = numeroArquivoRemessa3Digitos.Substring(numeroArquivoRemessa3Digitos.Length-3);
                numeroRegistroGeral++;
                var reg = new TRegistroEDI();
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0001, 001, 0, "9", '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0002, 367, 0, Empty, ' ');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0369, 008, 0, numeroRegistroGeral - 2, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0377, 015, 2, valorBoletoGeral, '0');
                reg.Adicionar(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0392, 003, 0, numeroArquivoRemessa3Digitos, '0');
                reg.Adicionar(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistroGeral, '0');
                reg.CodificarLinha();
                return reg.LinhaRegistro;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do registro TRAILER do arquivo de REMESSA.", ex);
            }
        }

        private string DescricaoOcorrenciaCnab400(string codigo)
        {
            switch (codigo)
            {
                case "02":
                    return "Entrada Confirmada";
                case "03":
                    return "Entrada Rejeitada";
                case "04":
                    return "Transferência de Carteira (Entrada)";
                case "05":
                    return "Transferência de Carteira (Baixa)";
                case "06":
                    return "Liquidação normal";
                case "09":
                    return "Baixado Automaticamente";
                case "10":
                    return "Baixado conforme instruções";
                case "11":
                    return "Títulos em Ser (Para Arquivo Mensal)";
                case "12":
                    return "Abatimento Concedido";
                case "13":
                    return "Abatimento Cancelado";
                case "14":
                    return "Vencimento Alterado";
                case "15":
                    return "Liquidação em Cartório";
                case "19":
                    return "Confirmação de instrução de protesto";
                case "20":
                    return "Confirmação de sustar protesto";
                case "21":
                    return "Transferência de beneficiário";
                case "23":
                    return "Título enviado a cartório";
                case "40":
                    return "Baixa de Título Protestado";
                case "41":
                    return "Liquidação de Título Baixado";
                case "42":
                    return "Título retirado do cartório";
                case "43":
                    return "Despesa de cartório";
                case "44":
                    return "Aceite do título DDA pelo pagador";
                case "45":
                    return "Não aceite do título DDA pelo pagador";
                case "51":
                    return "Valor do título alterado";
                case "52":
                    return "Acerto de Data de emissão";
                case "53":
                    return "Acerto de código de espécie de documento";
                case "54":
                    return "Alteração de seu número";
                case "56":
                    return "Instrução de negativação aceita";
                case "57":
                    return "Instrução de baixa de negativação aceita";
                case "58":
                    return "Instrução não negativar aceita";
                default:
                    return "";
            }
        }

        private TipoEspecieDocumento AjustaEspecieCnab400(string codigoEspecie)
        {
            switch (codigoEspecie)
            {
                case "01":
                    return TipoEspecieDocumento.DM;
                case "02":
                    return TipoEspecieDocumento.NP;
                case "03":
                    return TipoEspecieDocumento.NS;
                case "05":
                    return TipoEspecieDocumento.RC;
                case "09":
                    return TipoEspecieDocumento.DS;
                default:
                    return TipoEspecieDocumento.OU;
            }
        }

        private string AjustaEspecieCnab400(TipoEspecieDocumento especieDocumento)
        {
            switch (especieDocumento)
            {
                case TipoEspecieDocumento.DM:
                    return "01";
                case TipoEspecieDocumento.NP:
                    return "02";
                case TipoEspecieDocumento.NS:
                    return "03";
                case TipoEspecieDocumento.RC:
                    return "05";
                case TipoEspecieDocumento.DS:
                    return "09";
                default:
                    return "??";
            }
        }

        }
}



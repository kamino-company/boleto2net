using System;
using System.ComponentModel;
using Boleto2Net.Util;

namespace Boleto2Net
{
    [Serializable, Browsable(false)]
    public class Cedente
    {
        private string _cpfcnpj;
        public string Codigo { get; set; } = string.Empty;
        public string CodigoDV { get; set; } = string.Empty;
        public string CodigoFormatado { get; set; } = string.Empty;
        public string CodigoTransmissao { get; set; } = string.Empty;
        public string CPFCNPJ
        {
            get
            {
                return _cpfcnpj;
            }
            set
            {
                _cpfcnpj = CpfCnpjValidator.NormalizeAndValidate(CpfCnpjValidator.SanitizeSeparators(value));
            }
        }
        public string TipoCPFCNPJ(string formatoRetorno)
        {
            if (CPFCNPJ == string.Empty)
                return "0";
            switch (formatoRetorno)
            {
                case "A":
                    return CPFCNPJ.Length <= 11 ? "F" : "J";
                case "0":
                    return CPFCNPJ.Length <= 11 ? "1" : "2";
                case "00":
                    return CPFCNPJ.Length <= 11 ? "01" : "02";
            }
            throw new Exception("TipoCPFCNPJ: Formato do retorno inv�lido.");
        }
        public string Nome { get; set; }
        public string Observacoes { get; set; } = string.Empty;
        public ContaBancaria ContaBancaria { get; set; } = new ContaBancaria();
        public Endereco Endereco { get; set; } = new Endereco();
        public bool MostrarCNPJnoBoleto { get; set; } = true;

        /// <summary>
        /// Algumas cooperativas Sicoob rejeitam a remessa CNAB240 quando o campo Convênio
        /// (posição 033-052 do Header de Arquivo e 034-053 do Header de Lote) vem preenchido
        /// com o código do cedente, exigindo-o em branco. O padrão em outras cooperativas é
        /// o oposto. Não há regra única conhecida; habilitar por conta conforme confirmado com o banco.
        /// </summary>
        public bool SicoobConvenioEmBranco { get; set; } = false;
    }
}

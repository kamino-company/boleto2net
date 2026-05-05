using System;
using System.ComponentModel;
using System.Collections.Generic;
using Boleto2Net.Util;

namespace Boleto2Net
{
    [Serializable(), Browsable(false)]
    public class Sacado
    {
        private string _cpfcnpj = string.Empty;

        public string Nome { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public Endereco Endereco { get; set; } = new Endereco();
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
    }
}


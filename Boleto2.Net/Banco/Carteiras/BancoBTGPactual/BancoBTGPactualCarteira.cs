using System;
using Boleto2Net.Extensions;
using static System.String;

namespace Boleto2Net
{
    /// <summary>
    /// Composição comum às carteiras do BTG Pactual. As modalidades (simples, caucionada) diferem no
    /// contrato com o banco, não na montagem: o código da carteira entra no campo livre e na base do DV,
    /// e o resto é idêntico. Fica em um lugar só para as duas não divergirem por descuido.
    /// </summary>
    internal static class BancoBTGPactualCarteira
    {
        private const int TamanhoNossoNumero = 11;

        public static void FormataNossoNumero(Boleto boleto)
        {
            if (IsNullOrWhiteSpace(boleto.NossoNumero) || boleto.NossoNumero.TrimStart('0').Length == 0)
            {
                // Sem nosso número informado, o banco gera o dele e devolve no retorno. Zerar aqui é o que
                // o Bradesco faz no mesmo caso, e mantém o campo livre com 25 posições.
                boleto.NossoNumero = new string('0', TamanhoNossoNumero);
                boleto.NossoNumeroDV = "0";
                boleto.NossoNumeroFormatado = "000/00000000000-0";
                return;
            }

            if (boleto.NossoNumero.Length > TamanhoNossoNumero)
                throw new Exception($"Nosso Número ({boleto.NossoNumero}) deve conter {TamanhoNossoNumero} dígitos.");

            boleto.NossoNumero = boleto.NossoNumero.PadLeft(TamanhoNossoNumero, '0');

            // Base do DV com '0' à frente da carteira. É o único ponto em que o BTG difere do Bradesco,
            // que calcula sobre carteira + nosso número. Mesmo algoritmo (módulo 11, pesos 2 a 7, resto 1
            // vira 'P'), base diferente - trocar uma pela outra produz um dígito plausível e errado.
            boleto.NossoNumeroDV = ("0" + CarteiraComDoisDigitos(boleto) + boleto.NossoNumero).CalcularDVBradesco();

            boleto.NossoNumeroFormatado = $"{CarteiraComDoisDigitos(boleto).PadLeft(3, '0')}/{boleto.NossoNumero}-{boleto.NossoNumeroDV}";
        }

        public static string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            var contaBancaria = boleto.Banco.Cedente.ContaBancaria;

            // Agência(4) + Carteira(2) + Nosso Número(11) + Conta(7) + '0' = 25 posições.
            // O nosso número entra SEM o DV - ele existe só para exibição no campo "nosso número" do
            // boleto. Incluí-lo aqui empurraria a conta para fora da posição e o banco não localizaria
            // o título.
            return $"{contaBancaria.Agencia}{CarteiraComDoisDigitos(boleto)}{boleto.NossoNumero}{contaBancaria.Conta}0";
        }

        /// <summary>
        /// O cadastro aceita a carteira como "1" ou "01"; o campo livre e a base do DV exigem dois dígitos.
        /// </summary>
        private static string CarteiraComDoisDigitos(Boleto boleto)
            => boleto.Carteira.PadLeft(2, '0');
    }
}

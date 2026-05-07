# ADR-PAY-2324: Suporte a CNPJ Alfanumerico em Cedente/Sacado/Avalista

## Status

Aceita

## Contexto

A IN RFB 2.229/2024 institui o **CNPJ alfanumerico** a partir de **julho/2026**: as 12 primeiras posicoes passam a aceitar A-Z; os 2 digitos verificadores continuam numericos. Formato mantem 14 caracteres (ex: `AA345678000A29`).

Boleto2Net e submodulo do Plataforma-Hub-API responsavel por gerar boletos. Os setters de `Cedente.CPFCNPJ`, `Sacado.CPFCNPJ` e `Avalista.CPFCNPJ` rodavam validacao numerica via `Common.RemoveStringIdentation` + `Length == 11/14` antes de aceitar o valor. Com CNPJ alfanumerico, a validacao falhava silenciosamente — boletos com Cedente/Sacado alfa nao eram emitidos corretamente.

Esta story (PAY-2324) e correlata ao epico PAY-2096 do Hub-API. Depende dos helpers PAY-2097 disponibilizados via `[InternalsVisibleTo]`.

## Decisoes

### AD-1 — Setters Cedente/Sacado/Avalista alfa-aware via `CnabSettings`

Centralizar a logica de aceitacao em um helper `CnabSettings.IsCpfCnpjValido(string)` que delega para `CpfCnpjValidator`. Setters chamam o validator antes de gravar — comportamento alfa-aware por construcao.

### AD-2 — `CpfCnpjValidator` implementa algoritmo Serpro DV

Algoritmo oficial (charmap ASCII-48 + pesos `[5,4,3,2,9,8,7,6,5,4,3,2]` / `[6,5,4,3,2,9,8,7,6,5,4,3,2]`, modulo 11). Espelha a implementacao em `Common.ValidaCNPJAlphanumeric` do Hub-API (PAY-2097) — mesmo contrato, sem cross-assembly dependency.

### AD-3 — `Utils.FormataCPFCPPJ` renomeado para `Utils.FormataCPFCNPJ`

Typo pre-existente em master (`CPPJ` em vez de `CNPJ`). Aproveitamos o PR PAY-2324 para corrigir o typo apos feedback de reviewer. Metodo e `internal static` — apenas chamadores em `Boleto2.Net.Testes` foram atualizados.

### AD-4 — Helper `FormataCPFCNPJ` permanece position-based (sem validacao de DV)

`FormataCPFCNPJ` aplica mascara via `Substring` baseado no comprimento (11 → CPF, 14 → CNPJ, outros → `Exception`). Nao valida o DV nem rejeita letras nas posicoes de DV. Responsabilidade de validacao fica em `CpfCnpjValidator.ValidaCnpjAlfanumerico`.

Documentado via teste `FormataCPFCNPJ_DvComLetras_FormataPositionalmente` que prova o contrato (helper aceita `AB12CD34EFGHXY` mesmo com letras nas posicoes de DV).

## Alternativas Avaliadas

### Reusar `Common.CleanCpfCnpj` do Hub-API via referencia direta — rejeitada

Boleto2Net e submodulo independente. Adicionar dependencia direta ao Hub-API quebraria o isolamento. Solucao: implementar `CpfCnpjValidator` proprio com mesmo contrato Serpro.

### Validar DV em `FormataCPFCNPJ` — rejeitada

Helper de formatacao nao deve fazer validacao. Separacao de responsabilidades: formatador formata, validador valida. Documentado via teste.

## Consequencias

**Positivas:**
- Boleto2Net aceita CNPJ alfanumerico em Cedente, Sacado e Avalista.
- Algoritmo Serpro espelha PAY-2097 (Hub-API) — comportamento consistente entre as duas camadas.
- Typo pre-existente `CPPJ` corrigido como side-effect.

**Negativas:**
- Algoritmo Serpro duplicado entre Boleto2Net e Hub-API — manutencao em dois lugares se especificacao mudar.

**Neutras:**
- Sem dependencia nova do submodulo no Hub-API.
- Sem mudanca em geracao de arquivo de boleto (formato do CPF/CNPJ no boleto continua o mesmo).

## Riscos

### Drift entre `CpfCnpjValidator` (Boleto2Net) e `Common.ValidaCNPJAlphanumeric` (Hub-API)

Implementacoes paralelas do mesmo algoritmo Serpro podem divergir se a especificacao for atualizada. Mitigacao: vetor de teste comum (`AB12CD34EFGH83` — exemplo oficial Serpro com DV1=8, DV2=3) presente em ambos os repos.

## Tests

- `CnpjAlfanumericoFormattingTests` — 5 cenarios: CPF numerico, CNPJ numerico, CNPJ alfa, length invalido (lanca excecao), DV com letras (formata positionalmente).
- `CnpjAlfanumericoSettersTests` — paridade Cedente/Sacado/Avalista, flag ON/OFF, vetores numericos e alfa.

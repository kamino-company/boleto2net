# ADR-N3-6120: Tratamento de campos decimais em branco no retorno CNAB 400 Itau

## Status

Aceita

## Contexto

Ao importar arquivos de retorno CNAB 400 do Itau (tela "Importar Retorno" / Dashboard de Cobranca),
a operacao falhava com HTTP 500 e o front exibia um toast generico (`[object Object]`).

O parser `BancoItau.LerDetalheRetornoCNAB400Segmento1` (submodulo Boleto2Net) lia os 9 campos de
valor do registro de detalhe (tipo 1) via `Convert.ToDecimal(registro.Substring(...))`. O Itau
preenche campos de valor opcionais com **espacos em branco** em vez de zeros quando nao ha valor.
`Convert.ToDecimal("             ")` lanca `FormatException`, que e re-empacotada em
`LerDetalheRetornoCNAB400Segmento1` e propaga sem tratamento por linha em `ArquivoRetorno`
ate o controller — abortando a importacao do arquivo inteiro.

Evidencia (arquivo do cliente `CN02066A.RET`, anexo do ticket): 180 linhas (1 header, 178 detalhes,
1 trailer), todas com 400 chars. O campo `ValorOutrasDespesas` (offset `Substring(188, 13)`,
posicoes 189-201) esta em branco em **178/178** registros de detalhe; os outros 8 campos de valor
vem preenchidos. Confirma que o gatilho e o campo em branco, nao corrupcao do arquivo.

## Decisoes

### AD-1 — Helper `SafeToDecimal` local em `BancoItau`, sem tocar `Utils.cs`

Adicionado `private static decimal SafeToDecimal(string)` em `BancoItau.cs` e substituidas as 9
chamadas `Convert.ToDecimal(registro.Substring(...))` do segmento 1.

O helper foi mantido **local** (e nao promovido a `Utils.ToDecimal`) de proposito: `Utils.cs` esta
em encoding ISO-8859-1 (Latin-1) e seu encoding e propriedade da PAY-2324 (PR #19,
"utils-encoding-rot", que restaurou os literais Latin-1). Editar `Utils.cs` com ferramentas que
reescrevem o arquivo re-converteria o encoding e inflaria o diff com mudancas nao relacionadas ao
bug. `BancoItau.cs` ja esta em UTF-8, entao a alteracao fica contida em um unico arquivo de codigo.

### AD-2 — `blank -> 0`, demais entradas mantem `Convert.ToDecimal`

```csharp
private static decimal SafeToDecimal(string value)
{
    return string.IsNullOrWhiteSpace(value) ? 0m : Convert.ToDecimal(value);
}
```

Delta comportamental minimo: apenas strings nulas/vazias/somente-espaco passam a retornar `0m`.
Para qualquer valor nao-branco o comportamento e identico ao anterior (`Convert.ToDecimal`). Tratar
"outras despesas" em branco como zero e a interpretacao correta do layout Itau. Entradas
genuinamente malformadas (nao numericas) continuam lancando — falha alta para corrupcao real, em vez
de zerar silenciosamente um valor financeiro.

### AD-3 — Testes pelo caminho publico real

Os testes exercitam `LerDetalheRetornoCNAB400Segmento1` (o caminho real de parsing que faltava
cobertura), construindo registros de 400 chars com os 9 campos nos offsets exatos. Cobrem o bug,
todos os 9 campos individualmente, zeros e valores reais com divisao por 100.

## Alternativas Avaliadas

### Promover para `Utils.ToDecimal` compartilhado (com `NumberStyles.None` / `InvariantCulture`) — rejeitada

Tecnicamente superior (parsing estrito, culture-invariant) e reusavel pelos outros bancos. Rejeitada
neste ticket porque: (a) tocaria `Utils.cs` (Latin-1), conflitando com a PAY-2324 e introduzindo
risco de encoding; (b) os outros bancos estao fora do escopo do N3-6120. Fica registrada como
melhoria de DT (ver Riscos).

### `try/catch` por linha em `ArquivoRetorno` — rejeitada

Pular linhas com erro mascararia campos genuinamente malformados e produziria importacao parcial
silenciosa de dados financeiros.

## Consequencias

**Positivas**
- Importacao de retorno CNAB 400 Itau deixa de quebrar com campos de valor em branco.
- Diff cirurgico: 1 arquivo de codigo (`BancoItau.cs`) + 1 arquivo de teste + registro no `.csproj`.
- `Utils.cs` permanece byte-identico ao master (sem regressao de encoding).

**Negativas**
- O helper e duplicavel: os demais bancos com o mesmo padrao nao sao corrigidos aqui.

## Riscos

### Mesmo padrao em outros bancos (follow-up DT)

`Convert.ToDecimal(registro.Substring(...))` sem guarda existe tambem em `BancoBradesco` (19),
`BancoBrasil` (19), `BancoCaixa` (19), `BancoBanrisul` (8) e `BancoInter` (2). Qualquer um pode
quebrar com o mesmo cenario de campo em branco. Sugerido card de DT para padronizar via um helper
compartilhado (`Utils.ToDecimal`).

### Suite de testes do Boleto2Net quebrada por PAY-2324 (pre-existente)

`Boleto2.Net.Testes` nao compila no master atual por dois motivos introduzidos pela PAY-2324 (PR #19),
sem relacao com o N3-6120:
- `InternalsVisibleTo("Boleto2.Net.Testes")` nao casa com o assembly real `Boleto2Net.Testes`
  (CS0122 em `CnpjAlfanumericoFormattingTests`).
- `CnpjAlfanumericoSettersTests.cs:256` usa `new Boleto()`, mas `Boleto` nao tem construtor sem
  parametros (CS1729).

A biblioteca principal (`Boleto2.Net`) compila normalmente com esta correcao. A execucao da suite de
testes depende da resolucao dessa quebra pre-existente.

## Tests

`Boleto2.Net.Testes/BancoItauRetornoCnab400Tests.cs`:
- `LerDetalhe_ComValorOutrasDespesasEmBranco_NaoLancaEAssumeZero` (reproduz N3-6120)
- `LerDetalhe_ComTodosOsCamposDecimaisEmBranco_NaoLancaEAssumeZero`
- `LerDetalhe_ComCamposZerados_RetornaZero`
- `LerDetalhe_ComValoresPreenchidos_DividePorCemEMapeiaCadaCampo`
- `LerDetalhe_ComUmCampoDecimalEmBranco_NaoLanca` (9 casos, um por campo)

## Referencias

- Ticket: N3-6120 (Recebimento - Erro ao importar CNAB de retorno - Itau)
- PR Boleto2Net: kamino-company/boleto2net#20
- PR Plataforma-Hub-API (update submodule): kamino-company/Plataforma-Hub-API#7662
- Arquivo de evidencia: anexo `CN02066A.RET`

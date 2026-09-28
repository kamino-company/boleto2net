# ADR-N3-7193: Convênio em branco nos headers da remessa CNAB240 do Sicoob

## Status

Aceita

## Contexto

A remessa CNAB240 de cobrança do Sicoob gerada pela Kamino para uma conta de cliente na cooperativa 4036 foi rejeitada inteira pelo banco. O validador do Sicoob apontou os dois campos Convênio:

| Registro | Posições | Valor gerado | Mensagem do validador |
|---|---|---|---|
| Header do Arquivo | 033-052 | código do cedente (convênio sem o DV), alinhado à direita | "Código de contrato de cobrança informado não pertence ao beneficiário" |
| Header do Lote | 034-053 | código do cedente | "Valor informado diverge do valor default definido para o campo: espaços em branco" |

O campo já tinha mudado duas vezes, sempre para preenchido:

| Quando | PR | O que fez | Evidência que motivou |
|---|---|---|---|
| nov/2024 | #7 (`669d521`, N3-2384) | preencheu o Header do Arquivo com `Cedente.Codigo` | a cooperativa 4355 recusou o campo em branco |
| mar/2026 | #17 (`d5ae635`, N3-4985) | preencheu o Header do Lote com `Cedente.Codigo` | só um print "ARQUIVO TOTALMENTE REJEITADO", sem campo apontado. O erro detalhado do chamado anterior da mesma conta (N3-4648) era no Segmento Q ("nome do sacado não informado"), sem relação com convênio |

A análise automatizada do N3-7193 propôs o PR #25: uma flag por conta (`Cedente.SicoobConvenioEmBranco`, padrão preenchido) partindo da hipótese de que a exigência variava por cooperativa. O PR foi fechado sem merge: a premissa não se sustentou e o padrão "preenchido" faria toda conta nova nascer com o mesmo erro.

## Evidências

### Leiaute oficial

Planilha oficial do Sicoob "Instruções para montagem e validação de boletos de cobrança" (atualização de 03/09/2018), aba "Remessa - Opção CNAB240", layout `081` do arquivo e `040` do lote, que são as versões que esta biblioteca gera:

- 07.0, posições 033-052, Convênio: "Código do Convênio no Sicoob: Preencher com espaços em branco".
- 11.1, posições 034-053, Convênio: "Código do Convênio no Banco: Preencher com espaços em branco".

O leiaute é um só para a rede: a contracapa lista todas as centrais e cooperativas, sem ressalva por cooperativa. O texto é idêntico ao que o banco mandou no chamado. A biblioteca `laravel-boleto` (`Cnab/Remessa/Cnab240/Banco/Bancoob.php`) também gera `add(33, 52, '')` e `add(34, 53, '')`, e este repositório gerava `Empty` nos dois campos antes dos PRs #7 e #17.

### Validador público do Sicoob

Validador: `https://www.sicoob.com.br/web/sicoob/validador-cnab` (CNPJ do beneficiário + cooperativa + arquivo). Todos os testes são de 25/09/2026. Usaram arquivos `.rem` reais gerados pela Kamino, com só o campo Convênio alterado e o resto do arquivo preservado byte a byte (latin1, CRLF, 240 posições por linha).

| Coop | Origem do arquivo | Ambos em branco | Código no Header do Arquivo | Código no Header do Lote |
|---|---|---|---|---|
| 4036 | N3-7193 (cliente A) | APROVADO | reprovado com o código sem DV, com DV e com zeros à esquerda | reprovado |
| 3219 | N3-4648 (cliente B) | headers sem erro | reprovado com o código sem DV | reprovado |
| 4355 | N3-2384 (cliente C) | APROVADO, o mesmo arquivo recusado em 2024 | reprovado com o código com DV (o formato aceito em 2024) e sem DV | reprovado |
| 4598 | N3-4830 (cliente D), com o DV da conta corrigido | headers sem erro | reprovado com o código antigo e com o atual do cadastro | reprovado |
| 3246 | montado com dados reais da conta Sicoob do cliente E (3 títulos) | APROVADO | reprovado com o código sem DV | reprovado |

Mensagens em todos os casos reprovados:

- Header do Arquivo: "Código de contrato de cobrança informado não pertence ao beneficiário".
- Header do Lote: "Valor informado diverge do valor default definido para o campo: espaços em branco".

"Headers sem erro" (3219 e 4598): os erros restantes estão no Segmento Q, por dados de sacado vazios nesses arquivos antigos de jan e fev/2026, sem relação com o convênio. O arquivo original da 4598 também tinha o DV da conta em branco (o dígito da conta foi gravado junto do número e a posição do DV ficou vazia), o que parava o validador antes do convênio. Por isso foi retestado com o DV corrigido.

### Aceitar não é exigir

As duas contas Sicoob com fluxo ativo na base Kamino (coops 3219 e 3246) liquidam cobrança todo mês com o campo preenchido, e o validador reprova esse formato. O portal dessas cooperativas é mais tolerante que o validador. Na 4036, o portal rejeitou exatamente o que o validador aponta. O arquivo em branco passa no validador nas duas cooperativas ativas, então a mudança não quebra quem funciona hoje e tira a dependência dessa tolerância.

Censo de uso: em toda a base de clientes, só 4 contas Sicoob geraram remessa depois de 06/03/2026 (data do PR #17).

## Decisões

### AD-1 — Espaços em branco nos dois campos, para todas as contas e sem configuração

`GerarHeaderRemessaCNAB240` (posições 033-052) e `GerarHeaderLoteRemessaCNAB240` (posições 034-053) passam a gravar `Empty`. Isso reverte os PRs #7 e #17 e restaura o comportamento original da biblioteca.

Decidimos **não** tornar o campo configurável: o validador mostrou a mesma regra em 5 cooperativas, e o leiaute é único para a rede. Uma configuração só serviria para reproduzir um formato que o banco reprova.

### AD-2 — `Cedente.Codigo` continua obrigatório

O código do cedente continua sendo usado no campo livre do código de barras e no cálculo do DV do nosso número (`BancoSincoobCarteira1`). A decisão muda só o que vai para os headers da remessa, e não o cadastro nem o boleto.

### AD-3 — Testes no Hub-API, não neste repositório

O projeto `Boleto2.Net.Testes` não compila hoje (`TargetFrameworkVersion v4.0` contra a lib em `v4.8`, dívida registrada no PR #21), e a biblioteca só compila dentro do Hub-API (o `HintPath` dos pacotes é `..\..\packages`). O teste de regressão das posições 033-052 e 034-053 fica no `NimblyTests` do Hub-API e gera a remessa pelo mesmo caminho da produção. Ele entra junto com a atualização do ponteiro do submódulo, depois do merge deste PR.

Evidência já executada, com este diff aplicado localmente ao submódulo do Hub-API (sem commit do ponteiro):

- Teste `BankSlipRemittanceSicoobHeaderTests`: 3 casos. Os brancos no Header do Arquivo e no Header do Lote verificam as posições reais da remessa gerada por `MontarBoletoBancarioTradicional` e `ArquivoRemessa`. O terceiro é um controle positivo: o código do convênio continua no campo livre do código de barras.
- Vermelho com o ponteiro de produção (`da5e240`): 2 falhas (os dois headers) e 1 aprovado (o controle).
- Verde com este diff: 3 de 3.
- Suíte `NimblyTests` inteira (categoria `UnitTest`) com este diff: 7574 testes, 7572 aprovados, 0 falhas, 2 ignorados por `[Ignore]` anterior e sem relação com esta mudança.
- Build do `TaticoAPI.sln` em Release e Debug com este diff: sem erros.

## Consequências

- As remessas Sicoob de todas as contas passam a sair com o campo Convênio em branco nos dois headers.
- Não há mudança de schema, de cadastro ou de boleto.
- Rollback: reverter este commit e o ponteiro do submódulo no Hub-API.

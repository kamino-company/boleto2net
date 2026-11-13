# ADR-N3-7635: Remessa e retorno CNAB240 do Safra

## Status

Proposto

## Contexto

O cliente `canibalinc` está implantando a cobrança no Banco Safra (422), carteira 1 (cobrança simples), com agência `0160-7` e conta `580195-7`. O chamado pede as adequações necessárias no arquivo de remessa para atender ao Safra. O cliente ainda não gerou boleto nem remessa.

A primeira versão desta mudança seguiu o guia resumido que o banco entregou ao cliente ("Cobrança Safra — Guia para implantação de transmissão e troca de arquivos — Layout padrão Safra CNAB 240", anexo `500.pdf` do chamado). A fonte agora é o manual oficial completo: "Manual de Transferência de Arquivos — Cobrança (Produto 500) — Layout Padrão Safra 240 — Versão Agosto 2026" (58 páginas). Onde o guia e o manual divergem, vale o manual. A extração campo a campo do manual, com as páginas e notas citadas neste ADR, está em `docs/n3-support/investigations/N3-7635/safra-cnab240-manual-spec.md` (workspace).

O que o manual define e o guia não tinha, ou tinha diferente:

| Assunto | Guia | Manual |
|---|---|---|
| Agência | 5 posições, sem dizer se inclui o DV | 5 posições **sem dígito**; conta com 9 posições **com dígito** (p.8, nota 1) |
| Versão do header de arquivo (164-166) | `084`, `087` ou `103` | `103` (p.9) |
| Lote de serviço (004-007) | +1 a cada arquivo | `0001` no primeiro lote do arquivo, +1 por lote **dentro** do arquivo (G002, p.39) |
| Espécie (P 107-108) | incluía `23` NF e `99` outros | `02`, `04`, `12`, `16`, `17`, `31`, `33`; não existem `23` nem `99` (C015, p.30) |
| Protesto (P 221) | "3 somente simples" | `1` só vinculada, `2` só simples, `3` não protestar (C026, p.31) |
| Registros do trailer de lote (018-023) | "registros detalhe P, Q e R" | soma dos tipos 1, 2, 3, 4 e 5 (G057, p.41) |
| CEP do pagador | — | CEP em branco ou zerado não é aceito (p.8, nota 3); motivo `48` |
| Retorno | só posições do Segmento T | layout completo de T e U (p.23 e p.25) e tabela de movimentos própria (C044, p.33-34) |

O Hub-API já roteia a remessa do Safra para o CNAB240: em `Boleto.CNAB.GeraRemessaPendentes` (`ChatExterno/Models/Financeiro/Recebimento/Boleto.cs`), só Bradesco, Itaú, Sicredi e Inter usam CNAB400. Esta biblioteca, porém, só implementava o Safra em CNAB400:

| Método de `BancoSafra` | Antes, com `TipoArquivo.CNAB240` |
|---|---|
| `GerarHeaderRemessa`, `GerarDetalheRemessa`, `GerarTrailerRemessa` | lançam `Tipo de arquivo inexistente.` |
| `LerHeaderRetornoCNAB240`, `LerDetalheRetornoCNAB240SegmentoT`, `LerDetalheRetornoCNAB240SegmentoU` | lançam `NotImplementedException` |

Assim, a primeira remessa Safra gerada pela plataforma falharia no header, e nenhum retorno 240 do Safra poderia ser lido.

## Causa raiz

O layout CNAB240 do Safra nunca foi implementado nesta biblioteca, mas o Hub-API sempre roteou o Safra para o CNAB240. Além disso, o cadastro do cedente seguia o formato genérico (agência com 4 posições + DV, conta com 6 posições), que não corresponde ao "5 posições sem dígito / 9 posições com dígito" do manual, e o campo livre do código de barras montava agência + DV da agência + `00` + conta + DV.

## Decisões

### AD-1 — CNAB240 implementado no `BancoSafra`, com o CNAB400 mantido

`BancoSafra.GerarHeaderRemessa`, `GerarDetalheRemessa` e `GerarTrailerRemessa` ganham o `case TipoArquivo.CNAB240`, no mesmo formato de `BancoSicoob`:

- o header devolve o header de arquivo (`GerarHeaderRemessaCNAB240`) e o header de lote (`GerarHeaderLoteRemessaCNAB240`);
- o detalhe devolve os segmentos P e Q e, quando cabe, o R (AD-10);
- o trailer devolve o trailer de lote (`GerarTrailerLoteRemessaCNAB240`) e o trailer de arquivo (`GerarTrailerRemessaCNAB240`).

A remessa tem um lote só. Os segmentos S e Y (opcionais, p.7) não são gerados.

### AD-2 — Agência com 5 posições, sem DV, lida do cadastro

O manual pede a agência com "5 posições para a agência (sem dígito)" (p.8, nota 1); o DV da agência é um campo Alfa separado (G009). `BancoSafra.FormataCedente`:

- recusa agência vazia ou com caractere que não seja dígito ASCII (`SoDigitosAscii`), com `Boleto2NetException.AgenciaInvalida(agencia, 5)`. Uma agência cadastrada como `0160-7` é recusada;
- completa com zeros à esquerda até 5 posições pela nova sobrecarga `ContaBancaria.FormatarDados(..., digitosConta, digitosAgencia)`, que lança exceção acima de 5 dígitos. A sobrecarga antiga delega com 4 posições, então os demais bancos não mudam;
- não usa `DigitoAgencia` em nenhum campo do CNAB240 nem do código de barras.

O valor vem do cadastro, sem conversão: `160` e `0160` viram `00160`; `1600` e `01600` viram `01600`. Ele é gravado (propriedade `AgenciaCNAB240`) em:

| Onde | Posição | DV da agência |
|---|---|---|
| Header de arquivo | 053-057 | 058 em branco |
| Header de lote | 054-058 | 059 em branco |
| Segmento P | 018-022 | 023 em branco |
| Campo livre do código de barras (`BancoSafraCarteira1/2.FormataCodigoBarraCampoLivre`) | posições 21-25 do código (p.44) | — |
| Agência / código do beneficiário (`Cedente.CodigoFormatado`) | `00160 / 005801957` (modelos p.56-57) | — |

No retorno, `LerHeaderRetornoCNAB240` lê a agência em 053-057 e o DV (058) com `Trim`, que vem em branco.

A agência cobradora do Segmento P (101-105) vai zerada, com 106 em branco: o campo é opcional e o banco a atribui pelo CEP do pagador (C014, p.29). Assim, o código de 5 posições da agência só aparece nos campos da agência mantenedora.

### AD-3 — Conta de 1 a 8 dígitos, com DV obrigatório

O manual pede a conta com 9 posições "com dígito" no código de barras (p.8 e p.51) e 12 posições + DV (Alfa) no arquivo (p.9, 11 e 12). `BancoSafra.FormataCedente`:

- exige `DigitoConta` com exatamente 1 dígito ASCII; senão lança `Boleto2NetException.DigitoContaObrigatorio`;
- formata a conta com até 8 dígitos (`FormatarDados(..., 8, 5)`), lançando exceção acima disso.

No arquivo, a conta vai com 12 posições numéricas e o DV logo depois: header de arquivo 059-070 + 071, header de lote 060-071 + 072, Segmento P 024-035 + 036. O DV ag/conta (072, 073, 037) fica em branco, porque só se usa em banco com DV de conta de 2 posições (G012, p.40). No código de barras e no código do beneficiário, `BancoSafra.ContaCodigoBeneficiario` monta conta + DV com 9 posições, completando com zeros à esquerda.

O campo livre fica `7` + agência (5) + conta com DV (9) + nosso número com DV (9) + `2` (cobrança registrada, p.44 e 8.1.6, p.49).

### AD-4 — CNAB400 do Safra mantido

O caminho CNAB400 continua gerando o mesmo layout: agência com 4 posições + DV da agência, conta com 6 posições + DV. Como o cadastro agora guarda a agência com 5 posições e a conta com até 8, as propriedades `AgenciaCNAB400` e `ContaCNAB400` tiram os zeros à esquerda e completam de novo até 4 e 6 posições. Agência com mais de 4 dígitos significativos (ex.: `12400`) ou conta com mais de 6 (ex.: `1580195`) lança exceção, porque não cabe no layout 400. Para agências e contas que já cabiam, a saída não muda.

O retorno CNAB400 (`LerDetalheRetornoCNAB400Segmento1`) passa a formatar o nosso número sem hífen, como o boleto (AD-12).

### AD-5 — Lote `0001` e número da remessa (NSA)

- **Lote de serviço (004-007).** Constante `LoteCNAB240 = "0001"` no header de lote, nos segmentos P, Q e R e no trailer de lote: o arquivo tem um lote só, e G002 (p.39) manda `0001` no primeiro lote do arquivo. O header de arquivo usa `0000` e o trailer de arquivo `9999`. A versão anterior gravava o número do arquivo no lote, lendo o guia; o manual corrige.
- **Número da remessa.** O número do arquivo que `GerarHeaderRemessa` recebe vai no NSA do header de arquivo (158-163) e no "número remessa" do header de lote (184-191), que G079 (p.43) define como a sequência de envio do arquivo.

O número do arquivo fica no campo `[ThreadStatic] _numeroArquivoRemessa` (propriedade `NumeroArquivoRemessa`). `BancoSafra` é singleton (`Lazy<IBanco>`) e o CNAB400 repete esse número nos detalhes e no trailer (392-394); o `[ThreadStatic]` impede que um header gerado em outra thread troque o número no meio de uma remessa. `ArquivoRemessa` gera header, detalhes e trailer na mesma thread.

### AD-6 — Header de arquivo e header de lote

| Registro | Regra aplicada |
|---|---|
| Header de arquivo (`GerarHeaderRemessaCNAB240`) | banco `422`; lote `0000`; tipo `0`; inscrição do cedente com 14 posições (019-032); convênio (033-052) em branco; agência e conta (AD-2, AD-3); nome da empresa; `BANCO SAFRA S/A` em 103-132 (G014, p.40); código `1` (143); data de geração; hora `000000`; NSA (158-163); **versão `103`** (164-166, p.9); densidade `00000`; demais posições em branco |
| Header de lote (`GerarHeaderLoteRemessaCNAB240`) | lote `0001`; tipo `1`; operação `R` (G028, p.41); serviço `01`; 012-013 em branco; **versão do lote `060`** (G030, p.11); inscrição com 15 posições; convênio em branco; agência e conta em 054-072, iguais às de 053-071 do header de arquivo; mensagens 1 e 2 (104-183) em branco; número da remessa (184-191) = NSA; data de gravação (192-199); data do crédito `00000000` (200-207, preenchida só no retorno, C003, p.28) |

### AD-7 — Segmento P

`GerarDetalheSegmentoPRemessaCNAB240`:

| Posição | Regra aplicada |
|---|---|
| 016-017 | movimento = `CodigoOcorrencia` do boleto (`01` entrada, `02` pedido de baixa, C004, p.28) |
| 038-046 / 047-057 | nosso número com 9 dígitos (8 + DV) e 047-057 em branco (G069, p.42; AD-12) |
| 058 | carteira: `1` simples, `2` vinculada (C006); outra carteira lança exceção |
| 059-062 | `1` com cadastramento, `2` escritural, `2` cliente emite, `2` cliente distribui (C007-C010, p.29) |
| 063-077 | número do documento com 10 caracteres (C011, p.29) e 073-077 em branco |
| 078-100 | vencimento e valor nominal |
| 101-106 | agência cobradora zerada e DV em branco (AD-2) |
| 107-108 | espécie (AD-8) |
| 109 / 110-117 | aceite `N`; data de emissão |
| 118-141 | juros: com `ValorJurosDia`, ou `ValorTitulo × PercentualJurosDia / 100` arredondado em 2 casas (`JurosDiaCNAB240`), vai código `1` (valor por dia), data `DataJuros` (sem ela, vencimento + 1, já que C019, p.30, pede data maior que o vencimento) e valor. Sem juros, código `3` (isento) com data e valor zerados |
| 142-165 | desconto: com `ValorDesconto > 0`, código `1` (valor fixo até a data), `DataDesconto` (sem ela, o vencimento) e valor. Sem desconto, `0` e zeros (ver Pendências) |
| 166-195 | IOF e abatimento zerados |
| 196-220 | `NumeroControleParticipante` (identificação do título na empresa) |
| 221-227 | protesto e baixa (AD-9) |
| 228-240 | moeda `09`; contrato zerado; `1` (não autoriza pagamento parcial, C077, p.38) |

O Segmento P do pedido de baixa (movimento `02`) é gerado com as mesmas regras; a espécie e o protesto também são validados nele.

### AD-8 — Espécies aceitas

`EspecieCNAB240` aceita só as espécies da tabela C015 (p.30) que têm correspondente no enum `TipoEspecieDocumento`: `DM` → `02`, `DS` → `04`, `NP` → `12`, `NS` → `16`, `RC` → `17`, `CC` → `31`. O `33` (BDA) não tem correspondente no enum. Qualquer outra espécie, inclusive `NaoDefinido`, `NF` e `OU`, lança `Boleto2NetException.EspecieDocumentoNaoAceita`, em vez de gravar um código que o Safra rejeitaria com o motivo `21` (espécie inválida). Os códigos `23` e `99` da versão anterior foram removidos: eles não existem no manual.

### AD-9 — Protesto por carteira e prazo de baixa

`CodigoProtestoCNAB240` segue C026 (p.31):

| `CodigoProtesto` do boleto | Carteira | P 221-223 |
|---|---|---|
| `NaoProtestar` (default do boleto) | qualquer | `3` e `00` dias |
| `ProtestarDiasCorridos` | só vinculada | `1` + dias |
| `ProtestarDiasUteis` | só simples | `2` + dias |
| combinação incompatível ou outro código | — | `Boleto2NetException.InstrucaoDeProtestoInvalida` |

Quando há protesto e a baixa é `BaixarDevolver`, `DiasBaixaDevolucao` menor que `DiasProtesto` lança exceção, porque os campos 37.3P e 39.3P não podem ser conflitantes (p.13).

A baixa/devolução (224-227) usa `CodigoBaixaDevolucao` e `DiasBaixaDevolucao` do boleto: `1` + dias para `BaixarDevolver` (o default do boleto é 60 dias), e `2` + `000` nos demais casos (C028, p.31).

### AD-10 — Segmentos Q e R

**Segmento Q** (`GerarDetalheSegmentoQRemessaCNAB240`): movimento; inscrição, nome, endereço, bairro, CEP (5 + 3), cidade e UF do pagador; sacador/avalista com tipo `0` e zeros quando ausente; banco correspondente `000`; nosso número correspondente e 233-240 em branco.

O CEP passa por `CepPagadorCNAB240`: tira `-` e `.`, aceita de 1 a 8 dígitos ASCII e completa com zeros à esquerda até 8 (CEP guardado como número perde o zero inicial). CEP vazio, nulo, todo em zeros, com mais de 8 dígitos ou com caractere não numérico lança `Boleto2NetException.CepPagadorInvalido`, porque o Safra não aceita CEP em branco ou zerado (p.8, nota 3) e rejeitaria o título com o motivo `48`. No movimento `02` o CEP não é validado: o Segmento Q é opcional para instruções (p.5); o CEP só é formatado, e o inválido vai como `00000000`.

**Segmento R** (`GerarDetalheSegmentoRRemessaCNAB240`): só é gerado quando há multa e o movimento não é `02`. Descontos 2 e 3 (018-065) vão zerados; multa com código `2` (percentual, G073, p.42), data `DataMulta` (sem ela, vencimento + 1) e percentual com 2 decimais (075-089). O percentual é `PercentualMulta` ou, sem ele, o equivalente a `ValorMulta` sobre o valor do título (`PercentualMultaCNAB240`). 090-240 vão em branco.

### AD-11 — Trailers e carteira do cedente

| Registro | Regra aplicada |
|---|---|
| Trailer de lote | lote `0001`; quantidade de registros do lote = detalhes + header e trailer do lote (G057, p.41); quantidade e valor da cobrança simples e da vinculada; caucionada e descontada (070-115) com zeros; 116-240 em branco |
| Trailer de arquivo | lote `9999`; `000001` lote; total de registros do arquivo = detalhes + 4; contas para conciliação (030-035) com zeros; 036-240 em branco |

As quantidades e valores por carteira vêm dos totalizadores de `ArquivoRemessa`, que classificam cada boleto por `boleto.TipoCarteira`. O boleto herda esse tipo de `ContaBancaria.TipoCarteiraPadrao`. Para que P 058 e os totais do trailer acompanhem a carteira do código de barras, `BancoSafra.FormataCedente` deriva `TipoCarteiraPadrao` de `CarteiraPadrao`: `2` → vinculada, qualquer outra → simples. Um `TipoCarteiraPadrao` incoerente informado pelo chamador é sobrescrito.

### AD-12 — Nosso número com 9 caracteres

`BancoSafraCarteira1/2.FormataNossoNumero` completa o nosso número até 8 dígitos e calcula o DV (`CalcularDVSafra`). O `NossoNumeroFormatado` passa de `00001142-8` para `000011428`: 9 caracteres sem hífen, iguais às posições 038-046 da remessa (G069, p.42, e modelos de boleto das p.56-57). A leitura do retorno 240 (T 038-045 + 046) e 400 usa o mesmo formato, porque a plataforma confirma o registro procurando o boleto pelo nosso número formatado.

### AD-13 — Retorno CNAB240

A leitura segue as posições do manual (header p.9, Segmento T p.23, Segmento U p.25):

- **Header** (`LerHeaderRetornoCNAB240`): confere o banco `422`; lê inscrição, nome, agência (053-057, AD-2), DV da agência, conta (059-070), DV da conta (071), data de geração (144-151) e NSA (158-163).
- **Segmento T** (`LerDetalheRetornoCNAB240SegmentoT`): movimento (016-017) em `CodigoOcorrencia`; motivos da ocorrência (214-223, até 5 códigos de 2 posições, C047, p.34) em `CodigoOcorrenciaAuxiliar`, sem decodificar; nosso número 038-045 + DV 046; carteira (058); número do documento (059-073); vencimento; valor nominal; banco e agência cobradora (097-104); identificação do título na empresa (106-130); pagador; tarifa (199-213) em `ValorTarifas`.
- **Segmento U** (`LerDetalheRetornoCNAB240SegmentoU`): acréscimos (juros + multa + encargos, 018-032) em `ValorJurosDia`; desconto (033-047); abatimento (048-062); IOF (063-077); **valor pago pelo pagador (078-092) em `ValorPago`**; valor líquido a creditar (093-107) em `ValorPagoCredito`; outras despesas (108-122); outros créditos (123-137); data da ocorrência (138-145) em `DataProcessamento`; data do crédito (146-153) em `DataCredito`.

`ValorJurosDia` recebe o total de acréscimos porque o Safra não separa juros e multa no U.

### AD-14 — Tabela de ocorrências própria do Safra

A descrição do movimento de retorno vem de `BancoSafra.DescricaoOcorrenciaCnab240`, com a tabela C044 do Safra (p.33-34), e não de `Cnab.OcorrenciaCnab240`. A tabela genérica diverge do Safra em vários códigos (ex.: `44` e `45`, que no Safra são título pago com cheque devolvido e compensado) e não tem códigos como `11`, `15`, `29`, `46` a `65`, `93` e `94`. Código fora do domínio Safra (ex.: `10`) fica com descrição vazia. `Util/Cnab.cs` não foi alterado, então os demais bancos não mudam.

## Consequências

- O cliente passa a gerar pela plataforma a remessa CNAB240 do Safra no layout do manual, e a plataforma passa a ler o retorno 240 do Safra.
- Boletos cancelados do Safra, que o Hub-API inclui na remessa como baixa (ADR-N3-7293 do Hub-API), saem com movimento `02` nos segmentos P e Q, sem Segmento R e sem validação de CEP.
- Erros de cadastro que o Safra rejeitaria na homologação passam a falhar na geração, com mensagem em português: agência fora de 1 a 5 dígitos, conta acima de 8 dígitos, DV da conta ausente ou inválido, espécie fora da tabela, CEP do pagador inválido, protesto incompatível com a carteira e prazo de baixa menor que o de protesto.
- O código de barras e o código do beneficiário do Safra mudam para todos os cedentes Safra: a agência sai com 5 posições sem DV e a conta com 9 posições com DV. Antes, o campo livre levava agência (4) + DV da agência + `00` + conta (6) + DV. Antes desta mudança, nenhum boleto Safra podia ser registrado por remessa pela plataforma, porque o CNAB240 lançava exceção na geração.
- A remessa CNAB400 do Safra mantém o layout (AD-4). Nenhum outro banco teve o código de geração alterado; a sobrecarga nova de `ContaBancaria.FormatarDados` mantém 4 posições de agência para os demais.
- **Riscos e limites:**
  - `BancoSafra` é singleton e a propriedade `Cedente` é compartilhada entre threads, como nos outros 10 bancos da biblioteca que usam `Lazy<IBanco>`. O `[ThreadStatic]` protege só o número do arquivo (AD-5); duas remessas de cedentes Safra diferentes geradas em paralelo poderiam trocar o cedente uma da outra. É um risco pré-existente da biblioteca, sem falha no fluxo atual, que gera cada remessa de forma síncrona (os testes reconfiguram o cedente em `[SetUp]` pelo mesmo motivo). Follow-up sugerido: tirar o estado por cedente do singleton.
  - Os totais por carteira do trailer de lote contam todos os boletos do arquivo, inclusive pedidos de baixa. O manual diz que esses campos só são usados no arquivo de retorno (C070/C071, p.38), então a diferença não afeta a remessa.
  - O Segmento R grava em branco 090-240, inclusive campos que o manual tipa como numéricos (200-215, 217-228 e 231). O guia mandava brancos e esses campos só se aplicam a débito automático e movimento `30`.
  - Sem sacador/avalista, Q 154 vai `0`; G005 só lista `1` e `2`, mas o guia aceitava `0`.
  - A interpretação do retorno (o que fazer com `03`, `09`, `17`, `26`, `28` etc.) é do Hub-API (`Boleto.InterpretaRetorno`), fora desta biblioteca. A análise está na seção 18 do documento de investigação.
- Rollback: reverter o commit e o ponteiro do submódulo no Hub-API.

## Testes

Arquivos `Boleto2.Net.Testes/BancoSafraCarteira1Tests.cs` (classes `BancoSafraCarteira1` e `BancoSafraCnab240Tests`, categoria "Safra CNAB240") e `Boleto2.Net.Testes/BancoSafraCarteira2Tests.cs` (classe `BancoSafraCarteira2`). Os dois arquivos já eram compilados pelo projeto; o `.csproj` não mudou. As fixtures `BancoSafraCarteira1` e `BancoSafraCarteira2` passaram a reconfigurar o cedente em `[SetUp]`, porque `BancoSafra` é singleton e outros testes trocam o cedente dele.

### `BancoSafraCnab240Tests`

| Teste | Casos | O que garante |
|---|---|---|
| `Remessa_GeraHeaderLoteSegmentosPQRETrailers_Com240Posicoes` | 1 | sequência `0,1,3P,3Q,3R,5,9`, 240 posições, banco `422` em todas as linhas |
| `Remessa_HeaderDeArquivo_SegueOManualSafra` | 1 | header de arquivo campo a campo: agência `01234` com 058 em branco, convênio em branco, NSA, versão `103` |
| `Remessa_Agencia_5PosicoesSemDigitoEmHeadersESegmentoP` | 5 | agência de 5 posições sem DV nos headers e no P (`160`/`0160` → `00160`, `1600`/`01600` → `01600`); P 101-106 = `00000 ` |
| `Cedente_AgenciaInvalidaParaOSafra_LancaExcecao` | 4 | `123456`, `0160-7`, vazio e dígitos Unicode são recusados |
| `Cedente_DigitoDaContaInvalido_LancaExcecao` | 4 | DV da conta vazio, com 2 dígitos, letra ou dígito Unicode é recusado |
| `Remessa_Conta_De6a8DigitosNoHeaderENoSegmentoP` | 3 | conta de 6, 7 e 8 dígitos com 12 posições + DV nos headers e no P |
| `Cedente_ContaComMaisDe8Digitos_LancaExcecao` | 1 | conta de 9 dígitos é recusada |
| `Remessa_CNAB400_ContaComMaisDe6Digitos_LancaExcecao` | 1 | conta de 7 dígitos não cabe no CNAB400 |
| `Remessa_CNAB400_AgenciaCom5Digitos_LancaExcecao` | 1 | agência `12400` não cabe no CNAB400 |
| `Remessa_TipoCarteira_SegueACarteiraDoCedente` | 2 | P 058 e totais do trailer seguem `CarteiraPadrao`, mesmo com `TipoCarteiraPadrao` incoerente |
| `Remessa_HeaderDeLote_RepeteAgenciaEContaDoHeaderDeArquivo` | 1 | lote `0001`, `R`, `01`, `060`, 054-072 = 053-071 do header de arquivo, 184-191 = NSA, data do crédito zerada |
| `Remessa_SegmentoP_TituloComJurosDescontoEspecieSafra` | 1 | P campo a campo: nosso número de 9 posições, `11222`, agência cobradora zerada, espécie `04`, juros `1`, desconto `1`, protesto `300`, baixa `1060`, moeda e parcial |
| `Remessa_SegmentoP_EspecieDaTabelaSafra` | 6 | `DM`, `DS`, `NP`, `NS`, `RC`, `CC` → `02`, `04`, `12`, `16`, `17`, `31` |
| `Remessa_SegmentoP_EspecieForaDaTabelaSafra_LancaExcecao` | 3 | `NaoDefinido`, `NF` e `OU` lançam exceção |
| `Remessa_SegmentosQER_PagadorEMultaPercentual` | 1 | pagador, CEP 5 + 3, avalista zerado, multa código `2` com data e percentual |
| `Remessa_SegmentoQ_CepVazioZeradoOuInvalido_LancaExcecao` | 6 | CEP vazio, nulo, zerado, com 9 dígitos, com letras ou dígitos Unicode é recusado |
| `Remessa_SegmentoQ_CepValido_SaiCom8Digitos` | 4 | CEP com hífen, ponto ou sem o zero inicial sai com 8 dígitos |
| `Remessa_PedidoDeBaixa_NaoValidaCepDoPagador` | 5 | no movimento `02` o CEP só é formatado; inválido vira `00000000` |
| `Remessa_Trailers_TotalizamLoteEArquivo` | 1 | 2 títulos: 7 registros no lote, 9 no arquivo, totais da cobrança simples, zeros em 070-115 e 030-035 |
| `Remessa_Lote_Sempre0001ENumeroRemessaNoHeaderDeLote` | 4 | lote `0001` em todos os registros do lote e NSA em 158-163 e 184-191 (arquivos 1, 2, 9999 e 10000) |
| `Remessa_CNAB400_NumeroDoArquivoNaoEhTrocadoPorOutraRemessaGeradaEmOutraThread` | 1 | header gerado em outra thread não troca o número do arquivo no detalhe CNAB400 |
| `Boleto_NossoNumeroFormatado_EspelhaAsPosicoes038a046DaRemessa` | 1 | nosso número do boleto = P 038-046 |
| `Remessa_TituloSemEncargos_JurosIsentoSemDescontoESemSegmentoR` | 1 | espécie `02`, juros `3`, desconto zerado, sem Segmento R |
| `Remessa_JurosPorPercentual_ViramValorPorDia` | 1 | percentual de juros convertido em valor por dia |
| `Remessa_Baixa_UsaMovimento02` | 1 | movimento `02` em P e Q |
| `Remessa_PedidoDeBaixaDeTituloComMulta_NaoGeraSegmentoR` | 1 | sem Segmento R no movimento `02`; contagens dos trailers acompanham |
| `Remessa_SegmentoP_ProtestoConformeACarteira` | 4 | protesto `2` na simples, `1` na vinculada, `3` sem protesto |
| `Remessa_SegmentoP_ProtestoNaoAceitoNaCarteira_LancaExcecao` | 3 | protesto incompatível com a carteira ou código não suportado lança exceção |
| `Remessa_SegmentoP_PrazoDeBaixaMenorQueOPrazoDeProtesto_LancaExcecao` | 1 | baixa em 5 dias com protesto em 10 é recusada |
| `Remessa_CNAB400_ContinuaComHeaderDe400Posicoes` | 1 | controle: o CNAB400 continua com 400 posições e agência 4 + DV |
| `Retorno_LeHeaderESegmentosTEU_DeLiquidacaoEDeRejeicao` | 1 | header (agência de 5 posições, DV em branco), liquidação `06` com todos os valores e datas do U e tarifa do T, rejeição `03` com os motivos de 214-223; nosso número sem hífen |
| `Retorno_DescricaoDaOcorrencia_SegueATabelaSafra` | 12 | descrições da tabela C044, inclusive `44` e `45`; `10` sem descrição |

### `BancoSafraCarteira1` e `BancoSafraCarteira2`

| Teste | Casos | O que garante |
|---|---|---|
| `Safra_1_REM400`, `Safra_2_REM400` | 1 + 1 | homologação do CNAB400 existente |
| `Safra_1_BoletoOK`, `Safra_2_BoletoOK` | 9 + 9 | código de barras, linha digitável e nosso número com os valores esperados atualizados para o novo campo livre e o nosso número sem hífen |
| `Safra_1_CodigoDeBarras_UsaAgenciaDe5PosicoesSemDigito`, `Safra_2_CodigoDeBarras_UsaAgenciaDe5PosicoesSemDigito` | 3 + 2 | campo livre e linha digitável com a agência de 5 posições sem DV (`160`, `0160`, `01600`), conta `580195-7` e código do beneficiário `XXXXX / 005801957` |
| `Safra_1_CodigoDeBarras_ContaDe6a8DigitosComDV` | 3 | conta de 6, 7 e 8 dígitos + DV com 9 posições no campo livre |

Os valores esperados de código de barras e linha digitável foram calculados à parte pelo algoritmo do manual (DAC módulo 11 e DV dos campos módulo 10, p.47-49).

### Resultado

O Boleto2Net não tem CI. Os testes foram compilados com `csc` e executados com `vstest` localmente:

- testes do Safra: 111 casos, 93 aprovados e 18 falhas;
- as 18 falhas são os 9 + 9 casos de `Safra_1_BoletoOK` e `Safra_2_BoletoOK`, pré-existentes: os vencimentos de 2016 e 2017 ficam fora do range de fator de vencimento proposto pela CENEGESC (`DateTimeExtensions`), e a exceção ocorre no cálculo do código de barras, antes das asserções. A cobertura do campo livre novo vem dos testes `CodigoDeBarras_*`, com vencimento em 2026;
- suíte completa do projeto: nenhuma falha nova em relação ao `HEAD` do submódulo.

O teste de regressão do lado do Hub-API (`NimblyTests/Services/Recebimento/BankSlipRemittanceSafraCnab240Tests.cs`) entra junto com a atualização do ponteiro do submódulo.

## Pendências

- **Código de 5 posições da agência.** O código grava o que estiver no cadastro, completado com zeros à esquerda (AD-2): o cadastro `160` ou `0160` gera `00160`, e `0160-7` é recusado. Para o cliente `canibalinc` (agência `0160-7`), a evidência disponível aponta para `01600`, mas o manual não diz se o 5º dígito é o DV e todos os exemplos dele terminam em `0`. É preciso confirmar com a mesa de implantação do Safra e, se for o caso, cadastrar a agência com as 5 posições confirmadas. Agência errada volta rejeitada com o motivo `07` e gera código de barras inválido.
- **P 142 sem desconto.** Sem desconto, o código vai `0`, mas a tabela C021 (p.30) não tem `0` (lista `1`, `2`, `3`, `5` e `7`). Confirmar com a mesa se `0` é aceito; o risco é rejeição com o motivo `28` (código do desconto inválido).
- **Retorno validado só contra o manual.** A leitura do header e dos segmentos T e U foi conferida contra as p.9, 23 e 25 do manual, sem arquivo de retorno real do Safra. Também falta confirmar com a mesa se a tarifa vem no T da própria linha `02`/`06`, numa linha `28` separada ou nas duas.
- **Testes sem CI.** Os testes do Boleto2Net só rodam localmente (`csc` + `vstest`). As 18 falhas de `Safra_x_BoletoOK` são pré-existentes e continuam até alguém atualizar os vencimentos desses casos.
- A homologação final depende do arquivo de teste que o cliente envia à mesa de implantação do Safra (p.7-8). Este ADR não substitui essa validação.

## Relacionados

- ADR-N3-7193 — Convênio em branco nos headers CNAB240 do Sicoob: mesma estrutura de geração CNAB240.
- ADR-PAY-2324 — CNPJ alfanumérico: inscrições de cedente, sacado e avalista gravadas com o formatador já usado pelos demais bancos.
- Hub-API: ADR-N3-7635. Cobre a seleção do CNAB240 para o Safra, espécie e seu número na remessa, o NSA, o pré-filtro de CEP, o boleto impresso e a interpretação do retorno CNAB240 do Safra por código de movimento.
- Hub-API: ADR-N3-7293 (baixa de cancelados por remessa, que inclui o Safra).
- `docs/n3-support/investigations/N3-7635/safra-cnab240-manual-spec.md` (workspace) — extração do manual Safra 240 e conferência contra o código.

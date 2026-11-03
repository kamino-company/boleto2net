# ADR-N3-7635: Remessa e retorno CNAB240 do Safra

## Status

Proposto

## Contexto

O cliente `canibalinc` está implantando a cobrança no Banco Safra (422), carteira 1 (cobrança simples), e encaminhou o guia que o banco entregou: "Cobrança Safra — Guia para implantação de transmissão e troca de arquivos — Layout padrão Safra CNAB 240" (anexo `500.pdf` do N3-7635). O chamado pede as adequações necessárias no arquivo de remessa para atender ao Safra. O cliente ainda não gerou boleto nem remessa.

O que o guia descreve:

- Páginas 4 a 19: o layout da remessa, com header de arquivo, header de lote, segmentos P, Q e R, trailer de lote e trailer de arquivo.
- Páginas 20 a 24: boleto, código de barras, nosso número e fator de vencimento.
- Página 25: o retorno, só pelas posições do Segmento T.
- Página 26: só a capa do guia CNAB 400, sem conteúdo.

O Hub-API já gera a remessa do Safra como CNAB240: `Boleto.CNAB.GeraRemessaPendentes` usa CNAB400 só para Bradesco, Itaú, Sicredi e Inter. Esta biblioteca, porém, só implementava o Safra em CNAB400:

| Método de `BancoSafra` | Antes, com `TipoArquivo.CNAB240` |
|---|---|
| `GerarHeaderRemessa`, `GerarDetalheRemessa`, `GerarTrailerRemessa` | lançam `Tipo de arquivo inexistente.` |
| `LerHeaderRetornoCNAB240`, `LerDetalheRetornoCNAB240SegmentoT`, `LerDetalheRetornoCNAB240SegmentoU` | lançam `NotImplementedException` |

Assim, a primeira remessa Safra gerada pela plataforma falharia no header, e nenhum retorno 240 do Safra poderia ser lido.

O código de barras do boleto já atendia ao guia e tem teste em `BancoSafraCarteira1Tests`. O nosso número impresso, porém, saía com hífen (`00001142-8`); a revisão de 2026-11-03 (AD-5) o alinha aos 9 caracteres da remessa.

## Causa raiz

O layout CNAB240 do Safra nunca foi implementado nesta biblioteca, mas o Hub-API sempre roteou o Safra para o CNAB240. O CNAB400 existente também não serviria como alternativa: ele não grava a espécie do título (`AjustaEspecieCnab400` devolve `??` para espécie não definida) e só grava a multa com a instrução 16. Além disso, o banco entregou o guia do 240.

## Decisões

### AD-1 — CNAB240 implementado no `BancoSafra`, com o CNAB400 intacto

Os três geradores ganham o `case TipoArquivo.CNAB240`, no mesmo formato de `BancoSicoob`:

- o header devolve o header de arquivo e o header de lote;
- o detalhe devolve os segmentos P e Q, e o R quando há multa;
- o trailer devolve o trailer de lote e o trailer de arquivo.

O caminho CNAB400 não foi alterado. A remessa 400 gerada antes e depois da mudança é idêntica byte a byte (ver Testes).

### AD-2 — Campos fixos conforme o guia Safra

| Registro | Regra do guia aplicada |
|---|---|
| Header de arquivo | lote `0000`; convênio (033-052) em branco; agência com 5 dígitos em 053-057 = agência + DV do cadastro (a mesma composição do campo livre do código de barras), com o DV da agência (058) em branco; conta com 12 dígitos (059-070); DV da conta (071); `BANCO SAFRA S/A` (103-132); código `1` (143); hora `000000`; sequencial do arquivo (158-163); versão `087` |
| Header de lote | lote = número do arquivo remessa (AD-5), `R`, serviço `01`, versão `060`; agência e conta em 054-072 iguais às de 053-071 do header de arquivo; mensagens em branco; número da remessa (184-191) = sequencial do lote, como manda o guia; data do crédito `00000000` |
| Segmento P | movimento = `CodigoOcorrencia` (`01` entrada, `02` baixa); nosso número com 9 posições (038-046) e 047-057 em branco; carteira `1` simples ou `2` vinculada; cadastro `1`, documento `2`, emissão e distribuição `2` (cliente); seu número com 10 caracteres (063-072); agência cobradora igual à mantenedora; aceite `N`; IOF e abatimento zerados (o Safra não aceita abatimento na entrada); moeda `09`; contrato zerado; `1` (não autoriza pagamento parcial) |
| Segmento Q | pagador; sacador/avalista `0` e zeros quando ausente; CEP em 5 + 3; banco correspondente `000` |
| Segmento R | só com multa; descontos 2 e 3 zerados; código de multa `2` (percentual), data e percentual |
| Trailer de lote | quantidade de registros do lote = detalhes + header e trailer do lote; quantidade e valor de cobrança simples e vinculada; caucionada e descontada zeradas, demais campos em branco |
| Trailer de arquivo | lote `9999`, `000001` lote, total de linhas do arquivo |

Para a contagem do trailer de lote, o guia diz "registros detalhe P, Q e R". No entanto, os exemplos dele (20 linhas no lote, 22 no arquivo) só fecham na contagem FEBRABAN, que inclui o header e o trailer do lote. Por isso adotamos a contagem FEBRABAN.

### AD-3 — Regras derivadas para dados que a plataforma envia de outro jeito

- **Espécie:** o guia aceita `02` DM, `04` DS, `12` NP, `17` RC, `23` NF e `99` outros. O Hub-API não inclui o Safra na lista de espécie obrigatória e manda `NaoDefinido`. Qualquer espécie fora da lista, inclusive `NaoDefinido`, vai como `99`.
- **Juros:** o guia aceita código `1` (valor por dia) ou `3` (isento). O valor usado é `ValorJurosDia`. Sem ele, usa-se `ValorTitulo × PercentualJurosDia / 100`, arredondado em 2 casas. Sem juros, vai o código `3`, com data e valor zerados.
- **Multa:** o Safra só aceita percentual. Vale `PercentualMulta`; sem ele, usa-se o percentual equivalente a `ValorMulta`. Sem multa, o segmento R não é gerado.
- **Desconto:** código `1`, valor fixo até a data, quando `ValorDesconto > 0`. Sem desconto, os campos vão zerados.
- **Protesto e baixa:** sem instrução de protesto, vai `3` (não protestar) com `00` dias. A baixa/devolução usa `CodigoBaixaDevolucao` e `DiasBaixaDevolucao` do boleto. O padrão é `1` (baixar e devolver) em 60 dias.
- **Carteira caucionada ou descontada:** o guia não prevê essas carteiras, e a geração lança erro explícito em vez de gravar um código inválido.

### AD-4 — Retorno CNAB240 pelo padrão FEBRABAN 240 (lote v.060)

O guia descreve o retorno só pelo Segmento T: o código de movimento fica em 016-017 e o motivo da ocorrência em 214-223. Para o resto do registro, ele remete ao manual FEBRABAN 240 do site do Safra. A leitura segue as posições FEBRABAN, as mesmas que `BancoSicoob` já usa:

- **Header:** inscrição, nome, agência e conta, data de geração e NSA.
- **Segmento T:** movimento, motivo (`CodigoOcorrenciaAuxiliar`), nosso número com 8 dígitos + DV em 038-046, carteira, seu número, vencimento, valor, banco e agência cobradora, identificação do título na empresa, pagador e tarifa.
- **Segmento U:** juros/multa, desconto, abatimento, IOF, valor pago, valor líquido, outras despesas, outros créditos, data da ocorrência e data do crédito.

A descrição da ocorrência vem de `Cnab.OcorrenciaCnab240`.

## Consequências

- O cliente passa a gerar pela plataforma a remessa CNAB240 do Safra no layout do guia, e a plataforma passa a ler o retorno 240 do Safra.
- Boletos cancelados do Safra, que o Hub-API inclui na remessa como baixa (ADR-N3-7293 do Hub-API), saem com movimento `02` nos segmentos P e Q.
- A remessa CNAB400 do Safra não muda. Nenhum outro banco foi tocado.
- **Riscos e limites:**
  - O guia condiciona as quantidades do trailer de lote ao movimento `01`. A biblioteca usa os totalizadores de `ArquivoRemessa`, que contam todos os boletos do arquivo, inclusive baixas. Num arquivo que só tem entradas, os dois critérios coincidem.
  - A homologação final depende do arquivo de teste que o cliente envia à mesa de implantação do Safra (etapas 3-4 do guia). Esta decisão não substitui essa validação.
  - A leitura do retorno fora das posições citadas no guia segue o padrão FEBRABAN e não foi conferida contra um retorno real do Safra.
  - `BancoSafra` é singleton (`Lazy<IBanco>`). O CNAB240 não acrescenta estado de instância: reaproveita a propriedade que o CNAB400 já gravava no header, renomeada para `NumeroArquivoRemessa` (AD-5).
- Rollback: reverter o commit e o ponteiro do submódulo no Hub-API.

### AD-5 — Revisão de 2026-11-03: lote por arquivo e nosso número do boleto

A reanálise contra o guia (p. 7-8, 20-21) achou duas divergências na primeira versão:

- **Lote.** O guia pede o lote (004-007) a partir de `0001`, somando 1 a cada novo arquivo, e repetido em 184-191 do header de lote. A primeira versão gravava sempre `0001`. Agora o lote acompanha o número do arquivo remessa que `GerarHeaderRemessa` recebe, em header de lote, P, Q, R e trailer de lote, e em 184-191. Com 4 posições, depois de `9999` o lote volta para `0001`.
- **Nosso número no boleto.** O guia pede que o boleto seja espelho da remessa, com 9 caracteres. `BancoSafraCarteira1/2.FormataNossoNumero` passa a gerar `000011428` em vez de `00001142-8`. A leitura do retorno (240 e 400) usa o mesmo formato, porque a plataforma confirma o registro procurando o boleto pelo nosso número formatado.

Continua pendente, à espera da mesa do Safra: a quantidade de registros do trailer de lote (detalhes + 2, adotada aqui, ou só os detalhes, como diz o texto do guia).

## Testes

`Boleto2.Net.Testes/BancoSafraCarteira1Tests.cs`, classe `BancoSafraCnab240Tests` (NUnit, categoria "Safra CNAB240"; no arquivo já compilado pelo projeto, sem alterar o `.csproj`):

| Teste | O que garante |
|---|---|
| `Remessa_GeraHeaderLoteSegmentosPQRETrailers_Com240Posicoes` | sequência `0,1,3P,3Q,3R,5,9`, 240 posições, banco `422` em todas as linhas |
| `Remessa_HeaderDeArquivo_SegueOGuiaSafra` | campos do header de arquivo, posição a posição |
| `Remessa_HeaderDeLote_RepeteAgenciaEContaDoHeaderDeArquivo` | campos do lote e igualdade 054-072 = 053-071 |
| `Remessa_SegmentoP_TituloComJurosDescontoEspecieSafra` | nosso número de 9 posições, espécie `04`, juros código `1`, desconto, protesto `300`, baixa `1060`, moeda e parcial |
| `Remessa_SegmentosQER_PagadorEMultaPercentual` | pagador, CEP 5+3, avalista zerado, multa código `2` com data e percentual |
| `Remessa_Trailers_TotalizamLoteEArquivo` | 2 títulos: 7 registros no lote, 9 no arquivo, totais da cobrança simples |
| `Remessa_TituloSemEncargos_JurosIsentoSemDescontoEspecieOutrosESemSegmentoR` | espécie `99`, juros `3`, desconto zerado, sem segmento R |
| `Remessa_JurosPorPercentual_ViramValorPorDia` | conversão do percentual em valor por dia |
| `Remessa_Baixa_UsaMovimento02` | movimento `02` em P e Q |
| `Remessa_CNAB400_ContinuaComHeaderDe400Posicoes` | controle: o CNAB400 continua sendo gerado |
| `Retorno_LeHeaderESegmentosTEU_DeLiquidacaoEDeRejeicao` | header, liquidação `06` (valores pagos e datas) e rejeição `03` com o motivo de 214-223; nosso número formatado sem hífen |
| `Remessa_Lote_AcompanhaONumeroDoArquivoEmTodosOsRegistrosDoLote` | lote = número do arquivo (1, 2, 9999 e 10000 → `0001`) em todos os registros do lote e em 184-191 (AD-5) |
| `Boleto_NossoNumeroFormatado_EspelhaAsPosicoes038a046DaRemessa` | nosso número do boleto igual às posições 038-046 da remessa (AD-5) |

O projeto `Boleto2.Net.Testes` não compila sozinho (ver AD-3 de ADR-N3-7193). Por isso, os testes foram executados num harness local temporário em .NET 10. Esse harness compila o código-fonte desta biblioteca e o arquivo de teste acima, com um shim mínimo de NUnit sobre MSTest. Nada dele foi versionado. Resultados:

- vermelho com o código original (`d306e3d`): 10 de 11 falham ("Tipo de arquivo inexistente." na remessa, `NotImplementedException` no retorno), e o controle CNAB400 passa;
- verde com esta mudança: 11 de 11;
- regressão do CNAB400: a mesma remessa 400 (entrada e baixa) gerada com o `BancoSafra.cs` original e com o novo é idêntica byte a byte.

O teste de regressão do lado do Hub-API (`NimblyTests/Services/Recebimento/BankSlipRemittanceSafraCnab240Tests.cs`) entra junto com a atualização do ponteiro do submódulo.

## Relacionados

- ADR-N3-7193 — Convênio em branco nos headers CNAB240 do Sicoob: mesma estrutura de geração CNAB240 e a mesma limitação do projeto de testes.
- ADR-PAY-2324 — CNPJ alfanumérico: inscrições de cedente, sacado e avalista gravadas com o formatador já usado pelos demais bancos.
- Hub-API: ADR-N3-7635 (seleção CNAB240 para o Safra e teste de regressão), ADR-N3-7293 (baixa de cancelados por remessa, que inclui o Safra).

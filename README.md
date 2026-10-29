# Valores Receitas API

API ASP.NET Core 10 para o Conta de Chef. Os dados são persistidos no SQL Server usando EF Core.

## Configuração e execução

1. Instale o .NET 10 SDK e deixe o SQL Server acessível em `localhost`.
2. Configure `ConnectionStrings__DefaultConnection` como variável de ambiente; para desenvolvimento local, use um `appsettings.json` não versionado.
3. Configure `Email__Host`, `Email__Port`, `Email__UserName`, `Email__Password` e `Email__FromAddress` com os dados do seu provedor SMTP. O link de confirmação retorna para `Frontend:BaseUrl`.
4. Execute `dotnet run` nesta pasta. Em desenvolvimento, as migrations do EF Core são aplicadas automaticamente e a API fica disponível em `http://localhost:5232`.
5. Em outro terminal, inicie o front com `npm run dev` dentro de `valores-receitas-front`. O Vite fica em `http://localhost:5174`; o Axios acessa a API diretamente, e o CORS da API permite essa origem.

O arquivo `appsettings.example.json` contém o formato de configuração. Configure segredos SMTP e de banco por variáveis de ambiente em produção.

Em produção, a API não aplica migrations durante o startup por padrão. Antes de publicar uma versão com alterações de banco, execute `dotnet ef database update` com a connection string de produção configurada, e então implante/escale a API. Configure `Database__ApplyMigrationsOnStartup=true` somente se a implantação garantir uma única instância migradora.

## Organizacao

O fluxo da aplicacao segue `Controllers -> Services -> Repositories -> AppDbContext`.

- `Controllers/`: rotas, parametros HTTP, chamada da service e traducao do retorno para HTTP. Nao acessam o banco nem executam regras de negocio.
- `Services/`: regras por modulo, validacao de dados, montagem de models e coordenacao dos fluxos. `DataService` coordena a sincronizacao completa; `UserService` coordena cadastro, confirmacao e sessao.
- `Repositories/`: consultas e alteracoes por entidade. As leituras usam projecoes para DTOs, sem devolver entidades completas ou expor `IQueryable`. `UserRepository` utiliza os stores do Identity por meio dos managers, preservando hashing de senha, tokens e bloqueio de conta.
- `Models/`: uma entidade por arquivo, contendo apenas os campos e relacionamentos persistidos. Os nomes dos tipos foram mantidos para preservar o mapeamento e as migrations existentes.
- `DTOs/`: contratos de entrada e saida, separados das entidades. Cada arquivo contem apenas um record, com nome igual ao arquivo e sufixo `Dto`. Nao incluem campos internos de propriedade nem navegacoes desnecessarias. Datas de producao e venda continuam como `DateOnly`, pois representam dias, nao instantes.
- `Middleware/`: conversao centralizada de falhas das services em respostas `ProblemDetails`, mantendo os controllers sem tratamento de regras de negocio.

Cada controller, service e repository possui um contrato proprio, em um arquivo por interface nas pastas `Controllers/interfaces`, `Services/interfaces` e `Repositories/interfaces`. Os namespaces terminam em `.Interfaces`. Implementacoes declaram seus contratos e recebem dependencias das camadas por interface, incluindo usuario atual, validacao e unidade de trabalho.

`ServiceRegistration` centraliza os registros interface/implementacao: escopo por requisicao, exceto email, que permanece transiente. `IEmailService` estende o contrato `IEmailSender<AccountUser>` do Identity, registrado como adaptacao para manter os fluxos de autenticacao e substituicoes nos testes. As interfaces de controllers descrevem as actions; rotas, autorizacao e atributos HTTP permanecem nas classes concretas, preservando a descoberta padrao do MVC.

`ServiceRegistration` e um helper estatico de configuracao, e `ServiceException` representa erros; nao sao services de negocio e nao possuem interfaces.

Os modulos de insumos, estoque, receitas, ingredientes, producoes e vendas possuem controllers e services. O nome interno `MarketItem` e a rota `api/market` foram preservados para compatibilidade. O modulo de usuario conserva as rotas `api/auth`.

`UnitOfWork` compartilha a transacao entre repositories, com isolamento serializavel e rollback em caso de falha. `WorkspaceRepository` reconcilia os cadastros sem apagar e recriar todo o workspace. A revisao por usuario serializa alteracoes de estoque e rejeita gravacoes obsoletas com HTTP 409. `DatabaseRepository` concentra conectividade e migrations. Login, cadastro, confirmação e reenvio de email têm limites de requisição por IP em cada instância da API; atrás de proxy, configure encaminhamento de IP confiável antes de expor a aplicação. Todos os repositories e services de negocio sao registrados com escopo por requisicao.

## Endpoints

- `GET /api/health`: verifica a disponibilidade da API e do banco.
- `POST /api/auth/register`: cria uma conta e envia o link de confirmação.
- `POST /api/auth/confirm-email`: confirma a conta usando o token recebido.
- `POST /api/auth/resend-confirmation`: reenvia o email de confirmação.
- `POST /api/auth/login`, `POST /api/auth/logout` e `GET /api/auth/session`: controlam a sessão protegida por cookie HttpOnly.
- `GET /api/data`: snapshot completo do usuário autenticado, reservado à exportação/importação explícita; não deve ser usado para abrir telas ou paginar.
- `PATCH /api/data`: salva somente registros alterados e IDs explicitamente excluídos. Recebe `market`, `recipes`, `productions`, `sales`, `deleted: {market, recipes, productions, sales}` (todos arrays, mesmo vazios) e `revision`. Retorna apenas os registros alterados canônicos e a nova revisão. Registros não carregados permanecem intactos; validação, estoque FIFO e revisão continuam atômicos. Patches vazios e alterações isoladas de insumos, receitas, produções ou vendas usam leituras e gravações limitadas aos dados afetados; patches que combinam categorias ou incluem exclusões ainda usam a reconciliação completa por compatibilidade.
- `PUT /api/data`: reconcilia os dados e retorna o snapshot canonico (HTTP 200), incluindo `revision`, custos calculados pelo servidor e saldos de venda.
- `GET /api/market`: consulta nome e unidade dos insumos; `hasStockHistory` impede exclusao e mudanca de unidade de itens com historico.
- `GET /api/stock`: página de lotes ativos, ordenados por ordem de entrada decrescente e ID.
- `GET /api/stock?includeInactive=true`: inclui pacotes esgotados, mantendo o filtro por proprietario.
- `GET /api/stock/{id}`: consulta um lote do usuario, inclusive inativo.
- `POST /api/stock`: registra um pacote comprado com `marketItemId`, `entryDate`, `packageQty` e `packagePrice`. A quantidade e positiva, com ate 4 casas decimais, na unidade do insumo; o preco e nao negativo, com ate 2 casas. Retorna HTTP 201 com `Location`.
- `packageCount` permite registrar varios pacotes em uma chamada (inteiro positivo; padrao 1 quando omitido). `packageQty` e `packagePrice` sao por pacote. A resposta inclui `lots` com todos os lotes, preservando `lot` e `Location` para o primeiro. Todos os lotes sao criados na mesma transacao e a `revision` avanca uma unica vez; falhas desfazem o lote inteiro de cadastro.
- `PATCH /api/stock/{id}`: baixa manual, informando `remainingQty`, `reason` e `revision`; somente diminui o saldo.
- `PUT /api/stock`: corrige de 1 a 1.000 pacotes na mesma transacao. Recebe `lotIds` (sem duplicatas), `reason` (obrigatorio, ate 500 caracteres), `revision` e pelo menos um campo opcional: `marketItemId`, `entryDate`, `packageQty` ou `packagePrice`. Campos omitidos ou nulos sao preservados. Retorna HTTP 200 com `lot`, `lots` e a nova revisao; dados invalidos retornam 400, revisao obsoleta retorna 409.
- A correcao permite um pacote individual ou todos os pacotes restantes de uma unica compra (`EntryOrder`), desde que sejam do mesmo insumo. Multiplas compras, insumos diferentes e subconjuntos com mais de um pacote sao rejeitados com HTTP 400, sem alterar dados nem revisao.
- `DELETE /api/stock/{id}?revision={revision}`: exclui logicamente um pacote sem consumo de produção e retorna HTTP 200 com `{revision}`. A revisão avança uma unidade na mesma transação; não é necessário recarregar o workspace completo. Revisão obsoleta retorna 409; IDs inexistentes ou de outro usuário retornam 404. Não altera outros pacotes da compra.
- `GET /api/productions/{id}/consumptions`: lista lotes usados, quantidades e custos historicos.
- `GET /api/query/productions/{id}/report`: inclui `topBuyers` com até cinco compradores (`customer`, `packages`, `spent`) de todas as vendas da produção, sem depender da página diária. Exclui doações, inclui vendas não pagas e agrupa nomes sem distinguir caixa ou espaços nas pontas. Ordena por pacotes e gasto decrescentes, com desempate pelo nome.
- `GET /api/query/sales-report?from=2026-10-01&to=2026-10-31&productionId=...`: indicadores completos das vendas do usuário no período inclusivo e na produção opcional. Retorna `totals` (vendas, pacotes vendidos/doações, clientes identificados, faturamento, recebido, pendente e custos separados), `daily` (página de dias com vendas em ordem cronológica), `topProducts` e `topBuyers` (até cinco, por faturamento/valor comprado decrescente). Totais e rankings independem da página diária. Doações não entram em vendas, ticket, faturamento ou rankings; seu custo é informado à parte. Clientes são agrupados ignorando caixa e espaços nas pontas; nomes vazios não entram na contagem de identificados. `search` e `payment` são ignorados neste relatório, pois são filtros exclusivos do histórico.
- `GET /api/query/sales` aceita também `payment=paid|pending|donated`; recebidos e pendentes excluem doações. Esse filtro é aplicado antes da paginação e da ordenação.
- `GET /api/recipes`: consulta receitas com seus ingredientes.
- `GET /api/recipes/{recipeId}/ingredients`: consulta os ingredientes de uma receita do usuario autenticado.
- `GET /api/productions`: consulta o historico de producoes do usuario autenticado.
- `GET /api/sales`: consulta as vendas do usuario autenticado.
- `GET /openapi/v1.json`: documento OpenAPI em ambiente de desenvolvimento.

Cadastros, produções e vendas usam `PATCH /api/data` nas telas; `PUT /api/data` permanece para substituição completa explícita. Entradas e baixas manuais usam `api/stock`. As alterações enviam a última `revision` recebida. Categorias, clientes e observações podem continuar vazios.

### Paginação e consultas enxutas

Todas as rotas de listagem retornam `{items, page, pageSize, totalCount, totalPages}`. O padrão é `page=1&pageSize=10`; somente **10, 25, 50 ou 100** são aceitos. Valores inválidos retornam HTTP 400, sem truncamento silencioso. Páginas além do fim retornam `items: []` com os totais preservados.

`GET /api/query/{market,recipes,productions,sales,stock,balances,stock-totals}` aceita busca `search`, ordenação `sortBy`/`descending` e paginação. As rotas originais `/api/market`, `/api/recipes`, `/api/productions`, `/api/sales`, `/api/stock`, ingredientes e consumos também seguem o contrato paginado (mudança de contrato em relação aos antigos arrays).

- Filtros e ordenação são aplicados no banco **antes** de `Skip/Take`, com desempate por ID e isolamento por usuário. Consultas de leitura não rastreiam entidades.
- `market`/`recipes`/`productions` aceitam `ids` separados por vírgula (até 100) para resolver somente os registros usados na tela. Receitas na listagem não incluem os ingredientes: retornam `ingredientCount` e `ingredients: []`.
- `productions`, `sales` e `balances` aceitam `from`/`to` inclusivos. Vendas aceitam `productionId`; receitas aceitam `marketItemId`; estoque aceita `marketItemId`, `entryOrder` e `includeInactive`.
- `balances?available=true` limita o seletor às produções com saldo. Cada linha contém `production`, `packages`, `soldPackages`, `availablePackages` e `periodSales`. O saldo desconta **todas** as vendas vinculadas, mesmo fora da página/período; `periodSales` considera o período solicitado.
- `stock` inclui `purchaseLotCount` e `purchaseMixedItems` calculados sobre a compra inteira, não só a página; a edição conjunta pode consultar as páginas de uma compra específica. `stock-totals` agrega os saldos completos e pagina os insumos.
- `POST /api/stock?compact=true` e `PUT /api/stock?compact=true` omitem o array de pacotes na resposta, mantendo o primeiro lote e a revisão. As telas podem invalidar somente a página afetada, sem receber os até 1.000 pacotes da compra.
- `GET /api/query/{market,recipes,productions,sales,balances}/{id}` consulta um registro específico; IDs alheios ou inexistentes retornam 404. O detalhe completo de receita permanece disponível para compatibilidade; a edição normal usa os metadados e ingredientes paginados abaixo.
- `GET /api/query/recipes/{id}/metadata` retorna apenas a ficha da receita e `ingredientCount`, sem carregar ingredientes. O editor usa essa rota com `/ingredients` paginado (`sortBy=id|name|unit|quantity` e busca por nome/unidade).
- Para edição paginada de ingredientes, `PATCH /api/data` recebe `partialRecipeIngredients: true` e `deletedIngredients: []`. Nesse modo, `recipes[].ingredients` contém apenas inclusões/alterações; ingredientes não carregados são preservados. Exclusões precisam ser explícitas e pertencer às receitas enviadas. A resposta contém somente esses ingredientes alterados e a contagem completa.
- `GET /api/query/recipes/{id}/ingredients` e `/api/query/productions/{id}/consumptions` retornam páginas das listagens internas.
- `GET /api/query/summary?from=2026-10-01&to=2026-10-31` retorna revisão, contagens globais e agregados financeiros/de produção calculados no banco, independentes das páginas. As datas filtram vendas pela data de venda e produções pela data de produção.
- `GET /api/query/productions/{id}/report?page=1&pageSize=10` retorna totais completos da produção e `daily` como uma página de dias com vendas, ordenada por data crescente.
- As linhas diárias incluem `cumulativeRevenue` calculado desde o primeiro dia da produção, inclusive nas páginas intermediárias. Vendas filtradas por `productionId` incluem o acumulado até cada venda (desempate por data/ID), sem carregar o histórico inteiro no navegador.
- Ordenações calculadas também são globais: produções aceitam `packages`, `cost`, `profit`; saldos aceitam `packages`, `sold`, `available`, `cost`, `lostPackages`, `revenue` e `profit` (faturamento/resultado das vendas no período); vendas aceitam `total`.
- Consumos de produção aceitam ordenação por `itemName`, `entryDate`, `unit`, `quantity`, `cost`, `packageQty` e `packagePrice`, sempre antes de recortar a página.
- Na visão geral, os saldos aceitam `periodSold` (pacotes vendidos no período), `periodPrice` (preço médio realizado, ou preço previsto sem vendas), `periodRevenue` e `periodResult` (valores realizados quando há vendas no período, ou previstos sem vendas). Essas ordenações usam os mesmos filtros de datas e fórmulas exibidos na tela. `price` ordena apenas pelo preço previsto.

`QueryController` traduz HTTP; `QueryService` aplica o usuário atual; `QueryRepository` concentra filtros, projeções, agregações e limites SQL. A API nunca recebe um nome arbitrário de coluna para interpolar em SQL.

A migração `AddPaginationIndexes` adiciona índices filtrados para usuário/nome/ID, usuário/data/ID, vendas por produção, estoque por compra e referências de insumos. Em desenvolvimento, ela é aplicada no início da API; em produção, aplique-a como etapa única anterior ao rollout.

Os testes de paginação usam SQLite relacional por padrão. A integração SQL Server cria um banco temporário exclusivo, semeia registros de teste e o remove ao terminar. Configure uma connection string de uma instância descartável e execute em PowerShell:

```powershell
$env:VALORES_TEST_SQLSERVER = "Server=localhost;Database=master;User Id=sa;Password=<senha-de-teste>;Encrypt=True;TrustServerCertificate=True"
dotnet test Tests\ValoresReceitas.Api.Tests.csproj --filter FullyQualifiedName~SqlServerQueryTests
Remove-Item Env:\VALORES_TEST_SQLSERVER
```

Esse teste é opt-in e exige permissão para criar e remover apenas o banco temporário gerado para a execução. Nunca aponte a connection string a um banco de produção. O workflow de CI executa essa validação contra um SQL Server isolado.

Para validar a interface sem alterar o banco da aplicação, `BrowserFixtureTests` oferece uma API temporária real em `127.0.0.1:5232`, com SQLite em memória e 113 registros por módulo, incluindo uma receita com 32 ingredientes e uma produção com 32 vendas:

```powershell
$env:VALORES_BROWSER_FIXTURE = "1"
dotnet test Tests\ValoresReceitas.Api.Tests.csproj --filter FullyQualifiedName~ValoresReceitas.Api.Tests.BrowserFixtureTests --logger "console;verbosity=detailed"
```

Inicie o front em `http://127.0.0.1:5187`, com `VITE_API_URL=http://127.0.0.1:5232/api`, e use a conta de teste `pagination@example.test` / `Test-only.123`. O servidor dura no máximo 10 minutos; encerre-o ao terminar. Os dados são exclusivos dessa execução e desaparecem ao encerrá-la. Os testes normais não iniciam esse servidor.

Para validar especificamente o painel de vendas, use `VALORES_BROWSER_FIXTURE=1` com o filtro `FullyQualifiedName~SalesBrowserFixtureTests`. Essa fixture usa a porta **5238**, sem interromper a API principal, e semeia três produtos e 90 dias de vendas, pagamentos pendentes e doações. No front temporário em `127.0.0.1:5187`, defina `VITE_API_URL=http://127.0.0.1:5238/api`. A conta é `sales-dashboard@example.test` / `Test-only.123`. Também dura até 10 minutos e descarta os dados ao encerrar. Se a API principal estiver aberta no depurador, compile os testes com um `BaseOutputPath` separado dentro de `bin` para não disputar os assemblies em uso.

## Estoque e custo FIFO

### Correcao de compras

- Os pacotes de uma compra compartilham `EntryOrder`, definido pela revisao de entrada e preservado nas correcoes. O frontend agrupa por esse identificador, nunca por data, tamanho ou preco iguais.
- A correcao nao recria lotes nem apaga saidas. `novo saldo = nova quantidade comprada - (quantidade anterior - saldo anterior)`. Nao permite quantidade menor que o total retirado; `IsActive` acompanha o novo saldo.
- Trocar insumo e permitido antes de qualquer consumo por producao. Se houve somente baixas manuais, a nova unidade deve ser a mesma. A data nao pode ser posterior a uma producao que consumiu o pacote. Nenhuma falha salva parcialmente uma selecao.
- Precos/quantidades corrigidos valem para os proximos consumos. `StockConsumptions` preserva data, tamanho e preco do pacote no momento do consumo, alem da quantidade e custo historicos. `TotalCost` das producoes e custos de vendas nao sao recalculados.
- `StockAdjustments.PreviousData` e `CorrectedData` guardam snapshots JSON de cada pacote corrigido, com motivo, instante e saldos; ficam nulos nas baixas manuais e nos registros antigos.
- A migration `AddStockCorrections` adiciona esses campos e preenche os snapshots de consumos legados a partir dos lotes, antes de permitir novas correcoes. E aplicada automaticamente na inicializacao normal da API. Reinicie a API ao atualizar frontend e backend juntos.
- A quantidade de pacotes criados nao e editavel pelo endpoint; compras faltantes devem ser registradas. Pacotes extras sem movimentacoes podem ser excluidos individualmente; os que possuem historico podem receber baixa.

- Insumos definem apenas nome e unidade. As antigas colunas `PackageQty` e `PackagePrice` do cadastro permanecem no banco para preservar dados legados, mas nao fazem parte do contrato de insumos nem precificam novas producoes.
- Cada entrada registra um pacote, sua data, quantidade e preco pago. Um mesmo insumo pode ter pacotes de tamanhos diferentes, cada um com seu custo por unidade. Compras posteriores nao alteram lotes anteriores.
- Uma producao com `quantity: 0` e rascunho e nao consome estoque nem permite vendas. Ao salvar com quantidade positiva, o servidor multiplica os insumos da receita pela quantidade de receitas, consome os lotes mais antigos e registra `StockConsumptions` na mesma transacao.
- FIFO usa `EntryDate`, `EntryOrder` (revisao de entrada) e ID como desempate deterministico.
- Falta de qualquer insumo retorna HTTP 409 e desfaz todas as baixas, insercoes e a alteracao da revisao.
- `TotalCost` e a soma dos custos dos lotes usados (precisao de 8 casas), usada como fonte autoritativa. `IngredientCostPerBatch` e informativo; nao se deve recalcular o total multiplicando um valor por lote arredondado.
- Receita e quantidade de producoes salvas sao imutaveis. Rendimento, perdas e planejamento de venda podem ser ajustados sem novas baixas, respeitando vendas existentes.
- Producoes avulsas (`isStandalone: true`) nao consomem estoque nem exigem insumos na receita. Exigem quantidade positiva e `totalCost` >= 0 com ate 2 casas; o servidor deriva `ingredientCostPerBatch`. O custo manual pode ser corrigido depois (vendas existentes preservam seu custo historico), mas o tipo (normal/avulsa) e imutavel apos salvar. Migration: `AddStandaloneProductions`.
- Lotes com saldo zero ficam inativos automaticamente. Nao ha DELETE de estoque. Baixas manuais registram motivo, saldo anterior e instante em `StockAdjustments`.
- Remover uma producao a marca como `IsDeleted`; nao devolve consumo real e nao apaga seus vinculos. IDs removidos nao podem ser reutilizados.
- Insumos com qualquer historico de estoque nao podem ser excluidos nem mudar de unidade.
- A migration `AddInventoryFifo` preserva os dados existentes, sem inventar saldos nem realizar baixas retroativas. Producoes historicas continuam com seus custos antigos e `TotalCost: null`.

Exemplos autenticados estao em `ValoresReceitas.Api.http`.

As services validam identificadores unicos, referencias entre entidades, datas e saldo de pacotes antes de iniciar a transacao. A verificacao de conflitos com outras contas ocorre dentro da mesma transacao da substituicao.
Cada tabela de negocio possui proprietario. As consultas filtram o usuario autenticado, e a sincronizacao valida referencias e conflitos antes de gravar. Dados existentes sem proprietario sao atribuidos ao primeiro usuario que entrar com email confirmado.

Sem SMTP configurado, o cadastro cria a conta, mas retorna erro de envio; configure o provedor e use `POST /api/auth/resend-confirmation` para reenviar o link.

## Testes

Na raiz do workspace:

```powershell
dotnet test .\valores-receitas-api\Tests\ValoresReceitas.Api.Tests.csproj
```

O workflow `.github/workflows/ci.yml` executa testes e validações do frontend e da API; o job da API usa um SQL Server isolado para verificar as consultas específicas do provedor. A senha fixa desse container é apenas descartável para o serviço de CI, nunca deve ser reutilizada fora dele.

A suite usa xUnit/VSTest, TestServer, SQLite em memoria e envio de email simulado. Executa os controllers, services, repositories e autenticacao por cookie reais sem acessar o SQL Server ou SMTP locais. Cobre sincronizacao, consultas por modulo, DTOs, categoria vazia, validacoes, isolamento entre contas, conflitos de identificadores, rollback, cadastro, confirmacao, sessao, bloqueio e falhas de email. Tambem verifica os limites das camadas e compara o modelo SQL Server com as migrations sem abrir conexao.

SQLite nao substitui uma verificacao de integracao com SQL Server para aspectos especificos do provedor, como concorrencia, collation e precisao decimal.

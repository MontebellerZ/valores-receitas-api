# Valores Receitas API

API ASP.NET Core 10 para o Conta de Chef. Os dados são persistidos no SQL Server usando EF Core.

## Configuração e execução

1. Instale o .NET 10 SDK e deixe o SQL Server acessível em `localhost`.
2. Configure a conexão em `appsettings.json`. O arquivo local já está preenchido para o ambiente informado e é ignorado pelo Git. Para outros ambientes, use `ConnectionStrings__DefaultConnection` como variável de ambiente.
3. Configure `Email:Host`, `Email:Port`, `Email:UserName`, `Email:Password` e `Email:FromAddress` com os dados do seu provedor SMTP. O link de confirmação retorna para `Frontend:BaseUrl`.
4. Execute `dotnet run` nesta pasta. As migrations do EF Core são aplicadas automaticamente e a API fica disponível em `http://localhost:5232`.
5. Em outro terminal, inicie o front com `npm run dev` dentro de `valores-receitas-front`. O Vite encaminha `/api` para esta API.

O arquivo `appsettings.example.json` contém o formato de configuração sem a senha local.

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

`UnitOfWork` compartilha a transacao entre repositories, com isolamento serializavel e rollback em caso de falha. `WorkspaceRepository` reconcilia os cadastros sem apagar e recriar todo o workspace. A revisao por usuario serializa alteracoes de estoque e rejeita gravacoes obsoletas com HTTP 409. `DatabaseRepository` concentra conectividade e migrations. Todos os repositories e services de negocio sao registrados com escopo por requisicao.

## Endpoints

- `GET /api/health`: verifica a disponibilidade da API e do banco.
- `POST /api/auth/register`: cria uma conta e envia o link de confirmação.
- `POST /api/auth/confirm-email`: confirma a conta usando o token recebido.
- `POST /api/auth/resend-confirmation`: reenvia o email de confirmação.
- `POST /api/auth/login`, `POST /api/auth/logout` e `GET /api/auth/session`: controlam a sessão protegida por cookie HttpOnly.
- `GET /api/data`: retorna somente os dados do usuário autenticado.
- `PUT /api/data`: reconcilia os dados e retorna o snapshot canonico (HTTP 200), incluindo `revision`, custos calculados pelo servidor e saldos de venda.
- `GET /api/market`: consulta nome e unidade dos insumos; `hasStockHistory` impede exclusao e mudanca de unidade de itens com historico.
- `GET /api/stock`: lista apenas lotes ativos, ordenados por data da compra e ordem de entrada.
- `GET /api/stock?includeInactive=true`: inclui pacotes esgotados, mantendo o filtro por proprietario.
- `GET /api/stock/{id}`: consulta um lote do usuario, inclusive inativo.
- `POST /api/stock`: registra um pacote comprado com `marketItemId`, `entryDate`, `packageQty` e `packagePrice`. A quantidade e positiva, com ate 4 casas decimais, na unidade do insumo; o preco e nao negativo, com ate 2 casas. Retorna HTTP 201 com `Location`.
- `packageCount` permite registrar varios pacotes em uma chamada (inteiro positivo; padrao 1 quando omitido). `packageQty` e `packagePrice` sao por pacote. A resposta inclui `lots` com todos os lotes, preservando `lot` e `Location` para o primeiro. Todos os lotes sao criados na mesma transacao e a `revision` avanca uma unica vez; falhas desfazem o lote inteiro de cadastro.
- `PATCH /api/stock/{id}`: baixa manual, informando `remainingQty`, `reason` e `revision`; somente diminui o saldo.
- `PUT /api/stock`: corrige de 1 a 1.000 pacotes na mesma transacao. Recebe `lotIds` (sem duplicatas), `reason` (obrigatorio, ate 500 caracteres), `revision` e pelo menos um campo opcional: `marketItemId`, `entryDate`, `packageQty` ou `packagePrice`. Campos omitidos ou nulos sao preservados. Retorna HTTP 200 com `lot`, `lots` e a nova revisao; dados invalidos retornam 400, revisao obsoleta retorna 409.
- A correcao permite um pacote individual ou todos os pacotes restantes de uma unica compra (`EntryOrder`), desde que sejam do mesmo insumo. Multiplas compras, insumos diferentes e subconjuntos com mais de um pacote sao rejeitados com HTTP 400, sem alterar dados nem revisao.
- `DELETE /api/stock/{id}?revision={revision}`: exclui permanentemente um pacote sem consumo (inclusive estornado), baixa ou correcao. Retorna HTTP 204 sem corpo e avanca a revisao em uma unidade; consulte `GET /api/data` para obter a revisao atual. Pacotes com movimentacoes ou revisao obsoleta retornam 409; IDs inexistentes ou de outro usuario retornam 404. Exclusao e revisao usam a mesma transacao; nao altera outros pacotes da compra.
- `GET /api/productions/{id}/consumptions`: lista lotes usados, quantidades e custos historicos.
- `GET /api/recipes`: consulta receitas com seus ingredientes.
- `GET /api/recipes/{recipeId}/ingredients`: consulta os ingredientes de uma receita do usuario autenticado.
- `GET /api/productions`: consulta o historico de producoes do usuario autenticado.
- `GET /api/sales`: consulta as vendas do usuario autenticado.
- `GET /openapi/v1.json`: documento OpenAPI em ambiente de desenvolvimento.

Cadastros, producoes e vendas continuam em `PUT /api/data`; entradas e baixas manuais usam `api/stock`. As alteracoes enviam a ultima `revision` recebida. Omitir a revisao so e aceito em workspaces sem historico de estoque, para compatibilidade inicial. Categorias, clientes e observacoes podem continuar vazios.

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

A suite usa xUnit/VSTest, TestServer, SQLite em memoria e envio de email simulado. Executa os controllers, services, repositories e autenticacao por cookie reais sem acessar o SQL Server ou SMTP locais. Cobre sincronizacao, consultas por modulo, DTOs, categoria vazia, validacoes, isolamento entre contas, conflitos de identificadores, rollback, cadastro, confirmacao, sessao, bloqueio e falhas de email. Tambem verifica os limites das camadas e compara o modelo SQL Server com as migrations sem abrir conexao.

SQLite nao substitui uma verificacao de integracao com SQL Server para aspectos especificos do provedor, como concorrencia, collation e precisao decimal.

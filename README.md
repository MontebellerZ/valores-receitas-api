# Valores Receitas API

API ASP.NET Core 10 para o Conta de Chef. Os dados são persistidos no SQL Server usando EF Core.

## Configuração e execução

1. Instale o .NET 10 SDK e deixe o SQL Server acessível em `localhost`.
2. Configure a conexão em `appsettings.json`. O arquivo local já está preenchido para o ambiente informado e é ignorado pelo Git. Para outros ambientes, use `ConnectionStrings__DefaultConnection` como variável de ambiente.
3. Execute `dotnet run` nesta pasta. A migration do EF Core é aplicada automaticamente e a API fica disponível em `http://localhost:5232`.
4. Em outro terminal, inicie o front com `npm run dev` dentro de `valores-receitas-front`. O Vite encaminha `/api` para esta API.

O arquivo `appsettings.example.json` contém o formato de configuração sem a senha local.

## Endpoints

- `GET /api/health`: verifica a disponibilidade da API e do banco.
- `GET /api/data`: retorna mercado, receitas com ingredientes, produções e vendas.
- `PUT /api/data`: valida e substitui o snapshot completo em uma transação. É o endpoint usado pelo front para criar, editar e excluir registros sem gravações parciais.
- `GET /openapi/v1.json`: documento OpenAPI em ambiente de desenvolvimento.

O endpoint de gravação valida identificadores únicos, relações entre entidades, datas e saldo de pacotes antes de iniciar a transação.
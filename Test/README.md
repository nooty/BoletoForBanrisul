# Testes

Os testes estão separados por intenção:

- `Unit`: testes determinísticos, sem chamadas para a API do Banrisul.
- `Integration`: testes que usam a API real do Banrisul e exigem credenciais.
- `Support`: helpers compartilhados pelos testes.

## Rodar testes unitários

```bash
dotnet test Test/Test.csproj --filter "Category=Unit"
```

Também é seguro rodar `dotnet test Test/Test.csproj`: os testes de integração ficam desabilitados por padrão.

## Rodar testes de integração

Configure as variáveis de ambiente:

```bash
CLIENT_ID=<seu-client-id>
CLIENT_SECRET=<seu-client-secret>
RUN_BANRISUL_INTEGRATION_TESTS=true
```

Depois execute:

```bash
dotnet test Test/Test.csproj --filter "Category=Integration"
```

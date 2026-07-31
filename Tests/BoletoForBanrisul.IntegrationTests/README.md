# Testes

Os testes estão separados por projeto:

- `Tests/BoletoForBanrisul.Core.Tests`: testes determinísticos do model, validação e helpers do Core,
  sem chamadas para a API do Banrisul.
- `Tests/BoletoForBanrisul.Client.Tests`: testes determinísticos do client HTTP (autenticação, políticas
  de retry, extensões de DI), também sem chamadas reais.
- `Tests/BoletoForBanrisul.Report.Tests`: testes determinísticos de geração de relatório/PDF, com o
  conversor de PDF real substituído por um dublê.
- `Tests/BoletoForBanrisul.IntegrationTests` (este projeto): testes que usam a API real do Banrisul e
  exigem credenciais.

## Rodar os testes unitários

```bash
dotnet test Tests/BoletoForBanrisul.Core.Tests
dotnet test Tests/BoletoForBanrisul.Client.Tests
dotnet test Tests/BoletoForBanrisul.Report.Tests
```

## Rodar os testes de integração

Configure as variáveis de ambiente:

```bash
CLIENT_ID=<seu-client-id>
CLIENT_SECRET=<seu-client-secret>
RUN_BANRISUL_INTEGRATION_TESTS=true
```

Depois execute:

```bash
dotnet test Tests/BoletoForBanrisul.IntegrationTests
```

Também é seguro rodar `dotnet test Tests/BoletoForBanrisul.IntegrationTests` sem essas variáveis: os
testes ficam desabilitados por padrão (reportados como *Skipped*, sem nenhuma chamada de rede).

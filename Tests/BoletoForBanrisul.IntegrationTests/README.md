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

## Inspecionar os PDFs gerados

Os testes de geração de PDF gravam os arquivos num diretório temporário próprio, apagado ao final de
cada teste. Para conservá-los:

```bash
KEEP_BANRISUL_TEST_ARTIFACTS=true
```

O caminho do diretório é impresso no output do teste.

## Caçar falhas intermitentes

`repeat-pdf-tests.ps1` roda os testes de PDF repetidas vezes e grava a saída completa de toda
execução que falhar em `pdf-test-failures/` — com o assert, a exceção e o stack trace. Existe porque
uma falha intermitente só é útil se a mensagem for capturada na hora em que ela acontece.

```powershell
.\repeat-pdf-tests.ps1                                                   # 30x a classe inteira
.\repeat-pdf-tests.ps1 -Iterations 100 -Filter Should_Abort_Conversion_When_Cancelled
.\repeat-pdf-tests.ps1 -Iterations 200 -StopOnFirstFailure               # para no primeiro erro
```

Cada iteração também compara a quantidade de profiles do Chromium antes e depois, para separar "o
teste falhou" de "o teste falhou porque sobrou profile".

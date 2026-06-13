# BoletoForBanrisul

A solução **BoletoForBanrisul** contém componentes .NET destinados à integração com a [API de cobrança](https://developers.banrisul.com.br/pages/PORTAL_V1.8.3/docs/clientes-banrisul/api-cobranca-v1.4.1.html) do banco Banrisul. Esses pacotes permitem a automação de processos relacionados à emissão, impressão e gestão de boletos bancários.

## Componentes

### 1. **BoletoForBanrisul.Core**

O componente **BoletoForBanrisul.Core** reúne os modelos, enums, converters e helpers compartilhados usados pelo client de API e pelo gerador de PDF.

### 2. **BoletoForBanrisul.Client**

O componente **BoletoForBanrisul.Client** é um client .NET que oferece integração completa com a API de cobrança do banco Banrisul. Com ele, é possível realizar as seguintes operações:

- Registrar boletos
- Consultar boletos
- Alterar a data de vencimento
- Conceder abatimento
- Baixar (cancelar) boletos
- Emitir boletos em formato PDF
 
O componente também gerencia o processo de autenticação OAuth2. Os tokens de retorno são armazenados em cache para reutilização, facilitando a utilização da API de cobrança.

O pacote também inclui extensões para registrar o builder no contêiner de injeção de dependência do .NET.

Para a exportação de boletos em formato PDF localmente, utilize o componente complementar **BoletoForBanrisul.Report**.

### 3. **BoletoForBanrisul.Report**

**BoletoForBanrisul.Report** é um componente .NET que permite a exportação de boletos gerados em formato PDF. As principais funcionalidades incluem:

- Geração de boletos em formato PDF
- Geração de arquivo zip contendo múltiplos PDFs de boletos gerados

Este componente utiliza os modelos compartilhados do **BoletoForBanrisul.Core**, sem depender do **BoletoForBanrisul.Client**.

O pacote também inclui extensões para registrar o builder no contêiner de injeção de dependência do .NET.

**Observações**:
O componente depende da biblioteca DinkToPdf para a geração dos PDFs de boletos. Assim, a biblioteca nativa wkhtmltopdf deve ser incluída no diretório de execução do projeto.
Acesse https://github.com/rdvojmoc/DinkToPdf para mais informações.

## Instalação

Tu podes instalar os componentes diretamente do [NuGet](https://www.nuget.org/) utilizando os seguintes comandos:

Para instalar o **BoletoForBanrisul.Client**:

```bash
dotnet add package BoletoForBanrisul.Client
```

Para instalar o **BoletoForBanrisul.Core** diretamente quando precisar apenas dos modelos e helpers compartilhados:

```bash
dotnet add package BoletoForBanrisul.Core
```

Para instalar o **BoletoForBanrisul.Report**:

```bash
dotnet add package BoletoForBanrisul.Report
```

## Injeção de dependência

Para registrar o builder do client:

```csharp
using BoletoForBanrisul.Client.Extensions;

services.AddBoletoForBanrisulClientBuilder(builder =>
{
    builder
        .SetAuthentication("client-id", "client-secret")
        .SetBeneficiary("codigo-beneficiario");
});
```

Para registrar o builder de relatórios:

```csharp
using BoletoForBanrisul.Report.Extensions;

services.AddBoletoForBanrisulReportBuilder(builder =>
{
    builder.Configure(options =>
    {
        options.PaymentLocation = "PAGAR EM QUALQUER BANCO OU CORRESPONDENTE BANCÁRIO.";
    });
});
```

## Testes

Os testes unitários podem ser executados sem credenciais:

```bash
dotnet test Test/Test.csproj --filter "Category=Unit"
```

Os testes de integração com a API real do Banrisul ficam desabilitados por padrão. Veja `Test/README.md` para configurar e executar esses cenários manualmente.

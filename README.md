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
O componente utiliza a biblioteca ChromiumHtmlToPdfLib para a geração dos PDFs de boletos, através do Google Chrome/Chromium em modo headless. É necessário que o Chrome ou Chromium esteja instalado no ambiente de execução.

#### Geração de PDF

`IBoletoReport` é `IDisposable` e os dois métodos aceitam `CancellationToken`:

```csharp
using BoletoForBanrisul.Report;

using var report = new BoletoReportBuilder()
    .Configure(options => options.Beneficiary = new BeneficiarySettings { /* ... */ })
    .ConfigureChromium(chromium =>
    {
        chromium.ConversionTimeout = TimeSpan.FromMinutes(2);
        chromium.MinimumFreeDiskMegabytes = 500;
        chromium.Logger = logger;
    })
    .Build();

var pdf = await report.PrintAsync(boleto, cancellationToken);
var zip = await report.ZipBoletosAsync(boletos, cancellationToken);
```

Cada conversão usa um profile do Chromium próprio e descartável, sem cache, apagado ao final: um
Chromium morto no meio de uma conversão não contamina a próxima. Antes de subir o navegador é
verificado o espaço livre em disco — sem espaço, o Chromium pendura no meio da conversão em vez de
retornar erro.

O `Dispose` do relatório descarta o conversor **apenas** quando foi o `Build()` que o criou. Um
conversor fornecido por `SetPdfConverter` continua sendo de quem o forneceu.

`IBoletoReport` é imutável depois de construído e seguro para uso concorrente.

A renderização do HTML fica no `IBoletoTemplateRenderer`, separado do relatório porque tem tempo de
vida diferente: construí-lo resolve as referências de metadados do Roslyn e cria um cache de
templates, então uma instância por boleto recompilaria o template a cada impressão. Sem configuração,
os relatórios compartilham `RazorBoletoTemplateRenderer.Default`, com o template embutido no pacote.

Para um template próprio, construa um renderizador e reaproveite a mesma instância:

```csharp
var renderer = new RazorBoletoTemplateRenderer(@"C:\templates", "Boleto.cshtml");

using var report = new BoletoReportBuilder()
    .SetTemplateRenderer(renderer)
    .Build();
```

#### Tratamento de erros

| Exceção | Quando | Retentar? |
| --- | --- | --- |
| `InsufficientDiskSpaceException` | Espaço livre abaixo de `MinimumFreeDiskMegabytes` | Não, enquanto o disco não for liberado |
| `TimeoutException` | A conversão estourou o prazo — o `ConversionTimeout` do navegador ou o deadline duro `ConversionTimeout + HardTimeoutGrace` | Só com alguma tolerância; indica navegador travado |
| `OperationCanceledException` | O `CancellationToken` do chamador foi cancelado | Não |
| `PdfConversionException` | Qualquer outra falha da conversão (causa em `InnerException`) | Sim |

Os tipos vivem em `BoletoForBanrisul.Report.Exceptions`. A distinção existe para que a política de
retry não fique em `Handle<Exception>`, que retentaria também cancelamento e disco cheio —
multiplicando o tempo preso justamente nos casos em que retentar não resolve.

São dois prazos, com papéis diferentes. O `ConversionTimeout` é o orçamento da conversão, contado de
ponta a ponta pelo próprio conversor — partida, navegação e impressão. Vencido o orçamento, a conversão
é cancelada e o `HardTimeoutGrace` é o tempo que se dá para esse cancelamento ser observado; passado
ele, o navegador travou num nível que não devolve o `await` e o processo é derrubado à força. Estourar
qualquer um dos dois resulta em `TimeoutException`.

O orçamento é contado aqui, e não delegado ao navegador, por dois motivos: o prazo interno da
biblioteca só começa depois da navegação, deixando-a descoberta, e quando ele vence a biblioteca o
relata de um jeito indistinguível de um erro de conteúdo — o que torna impossível classificar um
timeout pelo tipo da exceção recebida.

Um `ConversionTimeout` abaixo de 1 segundo é recusado na construção: o navegador não termina de subir
nesse tempo, então o valor só produziria falha. As configurações também são copiadas na construção —
alterar o objeto depois de criar o conversor não tem efeito. Para trocar de configuração, construa
outro conversor.

Cada conversão usa um profile descartável, apagado ao final. Como rede de segurança contra o que
escapar disso — um processo morto no meio de uma conversão, um handle que não foi liberado a tempo —
o conversor varre os profiles esquecidos sozinho: quando a última varredura passou de
`OrphanCleanupInterval` (padrão 1 hora), a conversão seguinte dispara uma nova em background,
apagando o que estiver parado há mais de `OrphanProfileMaxAge`. Quem dispara é a conversão e não um
timer, porque profile órfão só nasce de uma conversão — um processo ocioso não acumula nada para
limpar.

Ao final de cada conversão o conversor confirma que o processo do navegador terminou e, se ele tiver
sobrevivido ao encerramento, o derruba junto com a árvore de subprocessos. Essa última parte depende do
runtime: `Process.Kill(entireProcessTree)` existe do **.NET Core 3.0** em diante, e é o que o pacote
usa quando disponível. Em runtimes mais antigos — incluindo .NET Framework, alcançável porque o pacote
tem como alvo `netstandard2.0` — só o processo principal é encerrado. A biblioteca de conversão, por
sua vez, nunca encerra a árvore, então um filho que sobreviva a um pai já morto fica fora do alcance de
qualquer um dos dois.

A idade, porém, é só o gatilho: quem autoriza a remoção é o lock de propriedade que cada conversão
mantém aberto dentro do seu profile. Antes de apagar, a varredura tenta tomar esse lock, e desiste do
diretório se ele ainda tiver dono. É o que permite compartilhar a raiz de profiles entre processos sem
que um deles apague o `--user-data-dir` do outro no meio de uma conversão — inclusive quando o vizinho
foi configurado com um deadline maior. Como rede adicional para os profiles que não têm lock, o
conversor nunca considera órfão um profile mais novo que `ConversionTimeout + HardTimeoutGrace`.

`ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.FromHours(1))` continua público para uma
varredura manual no start, útil depois de uma queda.

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

services.AddBoletoForBanrisulClientBuilder((builder, provider) =>
{
    builder
        .SetAuthentication("client-id", "client-secret")
        .SetBeneficiary("codigo-beneficiario");
});
```

Para registrar o gerador de relatórios:

```csharp
using BoletoForBanrisul.Report.Extensions;

services.AddBoletoForBanrisulReportBuilder(
    (builder, provider) => builder.Configure(options =>
    {
        options.PaymentLocation = "PAGAR EM QUALQUER BANCO OU CORRESPONDENTE BANCÁRIO.";
    }),
    chromium => chromium.ConversionTimeout = TimeSpan.FromMinutes(2));
```

Uma chamada registra tudo:

| Serviço | Tempo de vida | Por quê |
| --- | --- | --- |
| `IPdfConverter` | Singleton | Carrega recursos de processo: Chromium e o semáforo de concorrência |
| `IBoletoTemplateRenderer` | Singleton | Carrega o engine Razor e o cache de templates compilados |
| `BoletoReportBuilder` | Transiente | Monta o relatório com o `PrintSettings` do momento |

Depois disso, injete o `BoletoReportBuilder` e construa o relatório na hora do uso:

```csharp
using var report = builder
    .Configure(options => options.Beneficiary = beneficiarioDoRequest)
    .Build();

var pdf = await report.PrintAsync(boleto, cancellationToken);
```

O `IBoletoReport` **não** é registrado de propósito. Registrá-lo exigiria chamar `Build()` no momento
do registro, o que congelaria o `PrintSettings` — o beneficiário acima de tudo — da primeira
resolução para todos os boletos seguintes. A divisão é essa: o que é caro e serve a aplicação
inteira (conversor e renderizador) é singleton; o que muda a cada boleto (`PrintSettings`) fica no
relatório, que é barato de construir.

Nenhum registro é forçado: todos usam `TryAdd`. Um `IPdfConverter` ou `IBoletoTemplateRenderer` já
registrado permanece, e se o `configureBuilder` chamar `SetPdfConverter`, `ConfigureChromium` ou
`SetTemplateRenderer`, essa escolha é respeitada e o serviço do contêiner não é injetado.

O descarte é do contêiner, que descarta os serviços `IDisposable` que ele mesmo criou. Chame
`Dispose` apenas no que tu criares na mão — o relatório, por exemplo.

## Validação local

Antes de qualquer requisição HTTP, `CreateBoletoAsync` valida o `Boleto` localmente. Um boleto
inválido lança `ModelValidationException` e **nenhuma requisição é enviada**.

`ModelValidationException` vive em `BoletoForBanrisul.Core.Validation`, carrega
`IReadOnlyList<ValidationResult> Errors` com o caminho pontuado de cada campo inválido (por exemplo,
`Titulo.Pagador.Cep`), e **não herda de `ApiException`** — quem só tem `catch (ApiException)` não a
captura. A distinção é proposital: falha de validação local (o boleto está malformado) é uma coisa
diferente de falha do banco (a API rejeitou a requisição).

Para validar sem lançar exceção — por exemplo, para exibir os erros num formulário antes de enviar:

```csharp
using BoletoForBanrisul.Core.Validation;

if (!RecursiveValidator.TryValidate(boleto, out var errors))
{
    foreach (var error in errors)
        Console.WriteLine($"{string.Join(", ", error.MemberNames)}: {error.ErrorMessage}");
}
```

**Mudança de comportamento**: campos de texto que excedem o tamanho máximo agora são **rejeitados**
pela validação, em vez de truncados silenciosamente na serialização como acontecia antes.

## Migração do Report `1.0.0-rc.6` para `1.0.0-rc.7`

| Mudança | O que fazer |
| --- | --- |
| `IPdfConverter.ConvertToPdfAsync` agora recebe `CancellationToken` | Implementações próprias precisam do novo parâmetro e devem honrá-lo. Chamadas existentes continuam compilando (o parâmetro tem valor padrão). |
| `IBoletoReport` herda `IDisposable` | Descarte o relatório. Implementações próprias precisam de um `Dispose`. |
| Ctor `ChromiumHtmlToPdfConverter(IEnumerable<string>, int)` removido | Use `ChromiumHtmlToPdfConverter(Action<ChromiumConverterSettings>)` ou `BoletoReportBuilder.ConfigureChromium`. |
| `AddBoletoForBanrisulReportBuilder` passou a registrar também `IPdfConverter` e `IBoletoTemplateRenderer`, e ganhou um segundo parâmetro `configureChromium` | Nada. Os registros usam `TryAdd`, então o que já estiver registrado permanece. |
| `PrintSettings.TemplateDirectoryPath` e `PrintSettings.TemplateFileName` removidos | Use `new RazorBoletoTemplateRenderer(diretorio, arquivo)` com `BoletoReportBuilder.SetTemplateRenderer`. A origem do template é configuração de aplicação, não de cada boleto. |
| Padrões novos: profile descartável por conversão, sem cache, `ConversionTimeout` de 5 min → 2 min, mínimo de 500 MB livres em disco | Nada, se os padrões servem. Para voltar ao profile fixo, preencha `UserProfileDirectory`; para desligar a checagem de disco, use `MinimumFreeDiskMegabytes = 0`. |

Quem propagava o token por `AsyncLocal` para contornar a assinatura antiga pode passá-lo direto e
remover o contorno.

## Testes

Os testes estão organizados em quatro projetos sob `Tests/`. Os três primeiros são determinísticos e
não exigem credenciais:

```bash
dotnet test Tests/BoletoForBanrisul.Core.Tests
dotnet test Tests/BoletoForBanrisul.Client.Tests
dotnet test Tests/BoletoForBanrisul.Report.Tests
```

Os testes de integração com a API real do Banrisul (`Tests/BoletoForBanrisul.IntegrationTests`) ficam
desabilitados por padrão. Veja `Tests/BoletoForBanrisul.IntegrationTests/README.md` para configurar e
executar esses cenários manualmente.

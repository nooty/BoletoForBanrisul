# BoletoForBanrisul

.NET components for Banrisul's
[API Cobrança de Títulos](https://developers.banrisul.com.br/bpi/link/api-cobranca-titulos.html):
register, query and manage *boletos* — the Brazilian bank payment slips — and render them as PDF
locally.

> This is an independent, community project. It is not affiliated with, endorsed by, or supported by
> Banco do Estado do Rio Grande do Sul (Banrisul). Using the API requires credentials issued by the
> bank to your organization.

A few Portuguese terms are kept in this documentation and in the model members that mirror the
bank's payloads, because they are its own field names: **beneficiário** (the payee), **pagador**
(the payer), **nosso número** (the bank's identifier for a slip) and **linha digitável** (the number
you type to pay a slip).

## Components

| Package | What it is |
| --- | --- |
| `BoletoForBanrisul.Core` | Shared models, enums, JSON converters, helpers and the local validator |
| `BoletoForBanrisul.Client` | HTTP client for the billing API, with OAuth2 handled for you |
| `BoletoForBanrisul.Report` | PDF rendering of boletos through headless Chrome/Chromium |

`.Report` depends on `.Core` and **not** on `.Client` — you can render slips you obtained some other
way. All three target `netstandard2.0`.

## Installation

```bash
dotnet add package BoletoForBanrisul.Client
dotnet add package BoletoForBanrisul.Report
```

Both pull in `BoletoForBanrisul.Core`. Install it directly only if you want the models and helpers on
their own:

```bash
dotnet add package BoletoForBanrisul.Core
```

> `BoletoForBanrisul.Report` also needs Chrome or Chromium installed on the machine that runs the
> conversion — the NuGet package does not bring a browser with it. See
> [Running in Docker](#running-in-docker) for a container that has one.

## Quick start

Register a boleto and print the registered slip to PDF:

```csharp
using BoletoForBanrisul.Client;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Report;

var boleto = new Boleto
{
    Ambiente = Ambiente.Teste,
    Titulo = new Titulo
    {
        SeuNumero = "PED-1042",
        DataEmissao = DateTime.Today,
        DataVencimento = DateTime.Today.AddDays(10),
        ValorNominal = 1234.56m,
        Especie = Especie.Outros,
        Pagador = new Pagador
        {
            TipoPessoa = TipoPessoa.PessoaFisica,
            CpfCnpj = "67524451091",
            Nome = "PAGADOR FICTICIO",
            Endereco = "RUA CALDAS JUNIOR 120",
            Cep = "90010260",
            Cidade = "PORTO ALEGRE",
            Uf = "RS",
            Aceite = Aceite.Sim
        }
    }
};

using var client = new BoletoClientBuilder()
    .SetAuthentication("client-id", "client-secret")
    .SetBeneficiary("0123456789012")
    .UseSandbox()
    .Build();

var response = await client.CreateBoletoAsync(boleto, CancellationToken.None);

using var report = new BoletoReportBuilder()
    .Configure(options => options.Beneficiary = new BeneficiarySettings
    {
        Code = "0123456789012",
        Name = "ACME LTDA",
        CpfCnpj = "12345678000199",
        Address = "RUA CALDAS JUNIOR 120",
        City = "PORTO ALEGRE",
        State = "RS",
        ZipCode = "90010-260"
    })
    .Build();

// The bank returns the registered slip enriched with nosso número, barcode and linha digitável.
var pdf = await report.PrintAsync(new Boleto { Titulo = response.Titulo! }, CancellationToken.None);

await File.WriteAllBytesAsync("boleto.pdf", pdf);
```

`PrintAsync` returns the PDF bytes; nothing is written to disk unless you write it. The report also
renders slips the bank never saw: pass a `Boleto` you filled in yourself and it computes the barcode
and the linha digitável from its fields.

The `using var client` above fits this script, which runs once and exits. In an application that
serves many requests, build the client once and share it — see [Using the client](#using-the-client).

## Using the client

`BoletoClientBuilder` produces an `IBoletoClient`. Authentication credentials and the beneficiário
code are required; everything else is optional.

```csharp
using var client = new BoletoClientBuilder()
    .SetAuthentication("client-id", "client-secret")   // required; scope defaults to "boletos"
    .SetBeneficiary("0123456789012")                   // required; full beneficiário code
    .UseSandbox()                                      // apidev.banrisul.com.br instead of production
    .SetRequestMode(RequestModeType.Test)              // bergs-ambiente header: "T" instead of "P"
                                                       // (query and change operations only)
    .EnableLogging(@"C:\logs\banrisul")                // defaults to ./Logs when the path is omitted
    .SetTokenStorage(new SharedInMemoryTokenStorage()) // the default; swap for a distributed cache
    .Build();
```

`SetRequestMode` sends the `bergs-ambiente` header, which registration does not use — it has no
effect on `CreateBoletoAsync`. Registration carries its own environment in the payload instead, as
`Boleto.Ambiente` (default: `Teste`), which mirrors the bank's own request collection, where
`POST /boletos` carries no `bergs-ambiente` header at all.

`EnableLogging` writes four JSON files per call — request, request body, response, response body —
into the given directory. The `Authorization` header is redacted; **the bodies are not**, so those
files hold payer names and documents in the clear. Point it somewhere you are willing to keep
personal data, and leave it off in production unless you are actively investigating something.

`IBoletoClient` is meant to be built per use, not shared: `SetAuthentication`, `SetBeneficiary` and
`SetRequestMode` are set once at `Build()` and frozen from then on, so a multi-tenant application —
different credentials or a different beneficiário per request — rebuilds the client for each one. It
is `IDisposable` and owns the two `HttpClient` instances the builder creates, one for the API and a
separate one for the token endpoint; dispose it (`using var client = ...`) when that unit of work is
done. If your credentials never change, building once and sharing the instance is a valid
optimization, but it is not what the DI registration below assumes.

### Operations

| Method | What it does | Returns |
| --- | --- | --- |
| `CreateBoletoAsync(boleto, ct)` | Registers a slip | `CreateBoletoResponse` |
| `GetBoletoAsync(id, typeId, ct)` | Queries a slip | `GetBoletoResponse` |
| `ChangeDueDateAsync(id, newDueDate, typeId, ct)` | Changes the due date | `ChangeDueDateResponse` |
| `GrantAbatementAsync(id, abatement, typeId, ct)` | Grants a rebate | `GrantAbatementResponse` |
| `CancelBoletoAsync(id, typeId, ct)` | Requests cancellation (*baixa*) | `CancelBoletoResponse` |
| `IssueBoletoAsync(id, typeId, ct)` | Asks the **bank** for the PDF | `byte[]` |

Every method takes a `CancellationToken`. The identifier can be the nosso número, the barcode or the
linha digitável — say which through `BoletoIdentifierType`; omit it and the nosso número is assumed.

`IssueBoletoAsync` returns the bank's own rendering of the slip. Use
`BoletoForBanrisul.Report` instead when you want to render locally, with your own layout, without a
round trip.

### Authentication

OAuth2 is handled for you: the client requests a token on first use, caches it and reuses it until
shortly before it expires — the renewal margin is five minutes, capped at half the token's lifetime
so that short-lived tokens are not renewed on every call. `SharedInMemoryTokenStorage` is the default
and shares the cache across every client in the process, so two clients with the same credentials
authenticate once. Implement `ITokenStorage` and pass it to `SetTokenStorage` to keep the token
somewhere else — a distributed cache, for instance, so that several instances of your application do
not each request their own.

### Resilience

For retry-enabled requests, transient failures — connection errors, 5xx, 408 — are retried three times with decorrelated jitter
backoff starting at one second, and every request times out after 100 seconds.

Which calls are retried is decided per request, not per client. Only operations that do not mutate
boleto state are retried:

| Call | Retried | Why |
| --- | --- | --- |
| `GetBoletoAsync`, `IssueBoletoAsync` | Yes | Reading changes nothing at the bank |
| The OAuth token exchange | Yes | Asking for a token creates nothing at the bank |
| **`CreateBoletoAsync`**, **`CancelBoletoAsync`**, **`ChangeDueDateAsync`**, **`GrantAbatementAsync`** | **No** | Writes have side effects and no documented idempotency — see below |

A retryable request has its body buffered before the first attempt, so each attempt can send it
again. One that is not retryable skips the buffering and goes straight out.

> **Every boleto-mutating request is sent exactly once, on purpose.** The API documents no idempotency guarantee for
> any of them, so a resend after a lost response is a gamble rather than a correction. Registration
> is the costliest case — a resend can leave two slips where you asked for one — but a repeated
> *baixa* or *alteração* is no better understood: the state may well converge, while the second
> response comes back as an error about a slip already cancelled or already changed.

A transient failure on a write therefore reaches you as an exception rather than being absorbed, and
what happened at the bank is genuinely unknown at that point. Do not simply send it again: query the
slip with `GetBoletoAsync` first, whenever you hold an identifier it accepts — nosso número, barcode
or linha digitável.

If the response to a registration that lets the bank generate the nosso número is lost, you are left
holding none of those identifiers, and there is no query by `SeuNumero` to fall back on. That is the
argument for sending an explicit `NossoNumero`: after an ambiguous failure, you still hold an
identifier that `GetBoletoAsync` can query.

A retry you write in your own application code inherits the same responsibility: resending a write is
safe only once you have checked what the first attempt actually did.

### Error handling

| Exception | When |
| --- | --- |
| `ModelValidationException` | The boleto failed local validation; no request was sent |
| `InvalidRequestException` | 400 — malformed request or invalid values |
| `NotFoundException` | 404 — no such slip |
| `UnprocessableEntityException` | 422 — well-formed but not processable |
| `TooManyRequestsException` | 429 — throttled |
| `InternalServerErrorException` | 500 |
| `ServiceUnavailableException` | 503 — service temporarily unavailable |
| `ResponseDeserializationException` | The response did not have the expected shape |
| `UnauthorizedException` | 401 — the credentials were rejected |
| `TokenDeserializationException` | The token response did not have the expected shape |
| `ApiException` | Any other unsuccessful status, with the code in `HttpStatusCode` |
| `HttpRequestException` | Network or transport failure, or no valid HTTP response was obtained |

The API exceptions live in `BoletoForBanrisul.Client.Exceptions.Api` and derive from `ApiException`,
which carries the `HttpStatusCode` and a list of `Occurrences` — the bank's own error codes and
messages, when it sent any. The authentication ones live in `BoletoForBanrisul.Client.Exceptions.Auth`
and derive from `AuthenticationException`.

## Local validation

Before any HTTP request, `CreateBoletoAsync` validates the `Boleto`. An invalid slip throws
`ModelValidationException` and **nothing is sent**.

`ModelValidationException` lives in `BoletoForBanrisul.Core.Validation`, carries
`IReadOnlyList<ValidationResult> Errors` with the dotted path of each invalid field (for example,
`Titulo.Pagador.Cep`), and **does not derive from `ApiException`** — a `catch (ApiException)` will not
catch it. That is deliberate: a local validation failure (your slip is malformed) is a different
thing from a bank failure (the API rejected the request).

Text fields longer than their maximum are rejected by validation rather than truncated on
serialization.

To validate without throwing — to show the errors in a form before submitting, say:

```csharp
using BoletoForBanrisul.Core.Validation;

if (!RecursiveValidator.TryValidate(boleto, out var errors))
{
    foreach (var error in errors)
        Console.WriteLine($"{string.Join(", ", error.MemberNames)}: {error.ErrorMessage}");
}
```

## Using the report

`BoletoReportBuilder` produces an `IBoletoReport`, which is `IDisposable` and offers two methods,
both cancellable:

```csharp
using BoletoForBanrisul.Report;

using var report = new BoletoReportBuilder()
    .Configure(options =>
    {
        options.Beneficiary = new BeneficiarySettings { /* ... */ };
        options.PaymentLocation = "PAGAR EM QUALQUER BANCO OU CORRESPONDENTE BANCÁRIO.";
        options.FileNameTemplate = "boleto_{NossoNumero}_{DataVencimento}";
    })
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

`ZipBoletosAsync` names each entry from `FileNameTemplate` and appends ` (2)`, ` (3)` and so on when
two slips would produce the same name, so no entry is ever silently lost.

`IBoletoReport` is immutable once built and safe for concurrent use. `BoletoReport` also has public
constructors, if you would rather wire the parts yourself than go through the builder.

### Print settings

| Property | Default | Purpose |
| --- | --- | --- |
| `Beneficiary` | empty | Payee shown on the slip: code, name, CPF/CNPJ and address |
| `PaymentLocation` | `PAGAR EM QUALQUER BANCO...` | The "local de pagamento" line |
| `LogoFilePath` | embedded Banrisul logo | Path to your own logo image |
| `FileNameTemplate` | `boleto_{NossoNumero}` | Name of each PDF, without the extension |

`FileNameTemplate` accepts these placeholders: `{NossoNumero}`, `{SeuNumero}`, `{CpfCnpjPagador}`,
`{NomePagador}`, `{DataVencimento}`, `{DataEmissao}`, `{NomeBeneficiario}` and
`{CpfCnpjBeneficiario}`. Dates come out as `yyyyMMdd`, CPF/CNPJ as digits only.

### Custom templates

HTML rendering lives in an `IBoletoTemplateRenderer`, kept separate from the report because it has a
different lifetime: constructing one resolves Roslyn's metadata references and creates a template
cache, so an instance per slip would recompile the template on every print. Without configuration,
reports share `RazorBoletoTemplateRenderer.Default`, which uses the template embedded in the package.

For your own template, build one renderer and reuse that same instance:

```csharp
var renderer = new RazorBoletoTemplateRenderer(@"C:\templates", "Boleto.cshtml");

using var report = new BoletoReportBuilder()
    .SetTemplateRenderer(renderer)
    .Build();
```

### Chromium

PDF rendering goes through [ChromiumHtmlToPdfLib](https://github.com/Sicos1977/ChromiumHtmlToPdf) and
a headless Chrome/Chromium, which **must be installed on the machine that runs the conversion**.

`ConfigureChromium` tunes the default converter:

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConversionTimeout` | 2 min | Budget for a whole conversion — startup, navigation and printing |
| `HardTimeoutGrace` | 30 s | Extra time for the cancellation to be observed before the process is killed |
| `MinimumFreeDiskMegabytes` | 500 | Free space required before launching; `0` turns the check off |
| `MaxConcurrentConversions` | 1 | Conversions in flight per converter — one Chromium process each |
| `UserProfileDirectory` | null | Fixed `--user-data-dir`; null gives each conversion its own |
| `UseCache` | false | Chromium's on-disk cache |
| `ChromiumExecutablePath` | null | Where the browser is; null locates it automatically |
| `Browser` | `Chrome` | Which browser to drive |
| `ChromiumArguments` | 11 headless-friendly flags | Command line; add or remove, do not replace the list |
| `PageSettings` | ChromiumHtmlToPdfLib's default | Paper size, margins, orientation |
| `WaitForWindowStatus` | null | Value of `window.status` to wait for before printing |
| `WaitForWindowStatusTimeout` | 60000 ms | How long to wait for it |
| `MediaLoadTimeout` | null | How long to wait for media; null keeps ChromiumHtmlToPdfLib's behaviour |
| `OrphanProfileMaxAge` | 1 h | Age at which a forgotten profile counts as an orphan |
| `OrphanCleanupInterval` | 1 h | Minimum gap between orphan sweeps in one process |
| `Logger` | null | `ILogger` for the converter and for ChromiumHtmlToPdfLib |

Settings are copied when the converter is built — changing the object afterwards has no effect. To
run with different settings, build another converter. A `ConversionTimeout` below one second is
refused outright: Chromium cannot finish starting in that time, so the value could only ever produce
failure.

`SetPdfConverter` and `ConfigureChromium` cannot be combined — `ConfigureChromium` only configures the
default converter, which is not used at all once you supply your own. Supplying your own
`IPdfConverter` also means you own its lifetime: the report disposes the converter **only** when it
was `Build()` that created it.

### Errors and timeouts

| Exception | When | Retry? |
| --- | --- | --- |
| `InsufficientDiskSpaceException` | Free space below `MinimumFreeDiskMegabytes` | No, not until the disk is freed |
| `TimeoutException` | The conversion overran either clock (see below) | Depends — see below |
| `OperationCanceledException` | The caller's `CancellationToken` was cancelled | No |
| `PdfConversionException` | Any other conversion failure (cause in `InnerException`) | Usually |

`TimeoutException` covers two different situations. Overrunning `ConversionTimeout` may mean nothing
worse than a budget set too tight for a heavy slip on a loaded machine, and a retry is reasonable;
overrunning the hard deadline on top of it means Chromium did not answer cancellation at all, and
retrying immediately tends to reproduce it. `PdfConversionException` is worth retrying when the cause
is transient — a browser that failed to start, a broken DevTools session — but not when it is
deterministic, such as an invalid configuration or HTML that never renders. When a retry is
appropriate, keep it bounded; deterministic failures should surface immediately.

The types live in `BoletoForBanrisul.Report.Exceptions`. The distinction exists so that a retry policy
need not be written as `Handle<Exception>`, which would also retry cancellation and a full disk —
multiplying the time spent stuck in exactly the cases where retrying does not help.

There are two clocks, with different jobs. `ConversionTimeout` is the conversion's budget, counted end
to end by the converter itself — startup, navigation and printing. When the budget runs out the
conversion is cancelled, and `HardTimeoutGrace` is the time allowed for that cancellation to be
observed; past it, the browser is wedged at a level that never returns the `await` and the process is
killed. Overrunning either one produces `TimeoutException`.

The budget is counted by `ChromiumHtmlToPdfConverter` rather than delegated to ChromiumHtmlToPdfLib
for two reasons: the library's own clock starts only after navigation, leaving startup uncovered, and
when it does expire the library reports it in a way that is indistinguishable from a content error —
which makes classifying a timeout by exception type impossible.

## Dependency injection

To register the client builder:

```csharp
using BoletoForBanrisul.Client.Extensions;

services.AddBoletoForBanrisulClientBuilder((builder, provider) =>
{
    builder
        .SetAuthentication("client-id", "client-secret")
        .SetBeneficiary("0123456789012");
});
```

Inject `BoletoClientBuilder` and build the client where you use it, same as the report below:

```csharp
using var client = builder.Build();

var response = await client.CreateBoletoAsync(boleto, cancellationToken);
```

`IBoletoClient` is deliberately **not** registered either, for the same reason as `IBoletoReport`: it
is what `Build()` freezes credentials and beneficiário into, and those can differ from one resolution
to the next.

To register the report builder:

```csharp
using BoletoForBanrisul.Report.Extensions;

services.AddBoletoForBanrisulReportBuilder(
    (builder, provider) => builder.Configure(options =>
    {
        options.PaymentLocation = "PAGAR EM QUALQUER BANCO OU CORRESPONDENTE BANCÁRIO.";
    }),
    chromium => chromium.ConversionTimeout = TimeSpan.FromMinutes(2));
```

That one call registers everything the report needs:

| Service | Lifetime | Why |
| --- | --- | --- |
| `IPdfConverter` | Singleton | Holds process-level resources: Chromium and the concurrency semaphore |
| `IBoletoTemplateRenderer` | Singleton | Holds the Razor engine and the compiled-template cache |
| `BoletoReportBuilder` | Transient | Assembles the report with the `PrintSettings` of the moment |

Inject `BoletoReportBuilder` and build the report where you use it:

```csharp
using var report = builder
    .Configure(options => options.Beneficiary = beneficiaryFromRequest)
    .Build();

var pdf = await report.PrintAsync(boleto, cancellationToken);
```

`IBoletoReport` is deliberately **not** registered. Registering it would mean calling `Build()` at
registration time, which would freeze the `PrintSettings` — the beneficiário above all — of the first
resolution for every slip that followed. The split is that: what is expensive and serves the whole
application (converter and renderer) is a singleton; what changes per slip (`PrintSettings`) lives in
the report, which is cheap to build.

No registration is forced — all of them use `TryAdd`. An `IPdfConverter` or `IBoletoTemplateRenderer`
already registered stays, and if `configureBuilder` calls `SetPdfConverter`, `ConfigureChromium` or
`SetTemplateRenderer`, that choice wins and the container's service is not injected.

Disposal belongs to the container, which disposes the `IDisposable` services it created itself. Call
`Dispose` only on what you create by hand — the report, for instance.

## Running in Docker

The final stage of an Alpine image, with what the report needs and nothing else:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

RUN apk add --no-cache chromium tini icu-libs icu-data-full && \
    apk add --no-cache msttcorefonts-installer fontconfig && \
    update-ms-fonts && fc-cache -f

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=publish /app/publish .

ENTRYPOINT ["/sbin/tini", "--", "dotnet", "YourApp.dll"]
```

Each line earns its place:

- **`chromium`** is the browser itself. ChromiumHtmlToPdfLib finds it in `/usr/bin` on its own; set
  `ChromiumExecutablePath` if your base image puts it elsewhere.
- **`tini`**, as PID 1, reaps the processes Chromium leaves behind. Without an init they pile up as
  zombies until the container runs out of pids — see
  [Profiles and process teardown](#profiles-and-process-teardown). Docker's `--init` flag does the
  same job.
- **The fonts** matter because the bundled template asks for Arial. Without them fontconfig
  substitutes whatever is at hand and the fields shift out of their boxes — a defect that shows up
  only in the rendered PDF, never as an error.
- **`fontconfig`** is also required by SkiaSharp, which generates the QR code.
- **ICU and `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`** are both required, and so is
  `icu-data-full`. Amounts are formatted with `new CultureInfo("pt-BR")`. The Alpine base image ships
  no ICU and sets the flag to `true`, and in that mode the constructor throws
  `CultureNotFoundException`. Setting the flag without installing ICU is worse: the process fails
  fast at startup. Installing `icu-libs` alone is not enough either — it comes with a reduced data
  package that need not carry pt-BR, and when the data is missing the culture still reports itself as
  `pt-BR` while the separators fall back, so the slip prints `R$1,234.56` instead of `R$ 1.234,56`.
  That failure is silent and reaches the customer, which is why `icu-data-full` is named explicitly.

On Linux ChromiumHtmlToPdfLib always adds `--no-sandbox`, so Chromium's own sandbox — its main
defence against a malicious page — is off. The renderer is a security surface: run it as a non-root
user, give the container no more privilege than it needs, and be deliberate about where the HTML
comes from. A container boundary is worth having, but it is not a replacement for the sandbox that
was turned off.

## Implementation details

Nothing here is needed to use the library. It is written down because it is the work
`ChromiumHtmlToPdfConverter` does on top of ChromiumHtmlToPdfLib, and because it is what you would
otherwise have to reverse-engineer the day a conversion misbehaves in production.

### Profiles and process teardown

Each conversion gets its own disposable Chromium profile, no cache, deleted at the end: a Chromium
that dies mid-conversion cannot contaminate the next one. Free disk space is checked before the
browser starts, because a full disk can make Chromium fail or hang mid-conversion rather than return
a clear error.

As a safety net for whatever escapes that — a process killed mid-conversion, a handle not released in
time — the converter sweeps forgotten profiles on its own: when the last sweep is older than
`OrphanCleanupInterval`, the next conversion starts a new one in the background, deleting anything
idle for longer than `OrphanProfileMaxAge`. A conversion triggers it rather than a timer, because an
orphaned profile is only ever born of a conversion — an idle process accumulates nothing to clean.

Age is only the trigger. What authorizes the deletion is the owner lock each conversion holds open
inside its profile: before deleting, the sweep tries to take that lock and gives up on the directory
if it still has an owner. That is what makes it safe to share the profiles root between processes
without one deleting another's `--user-data-dir` mid-conversion — including a neighbour configured
with a longer deadline. As an additional net for profiles with no lock, the converter never treats a
profile younger than `ConversionTimeout + HardTimeoutGrace` as an orphan.

At the end of every conversion `ChromiumHtmlToPdfConverter` confirms the browser process is gone and,
if it survived teardown, kills it along with its subprocess tree. That last part depends on the
runtime: `Process.Kill(entireProcessTree)` exists from **.NET Core 3.0** onwards, and is used when
available. On older runtimes — including .NET Framework, reachable because the package targets
`netstandard2.0` — only the main process is killed. ChromiumHtmlToPdfLib never kills the tree either
(its own `KillProcessAndChildren` calls plain `Process.Kill()` on the main process), so a child that
outlives an already-dead parent is out of the reach of both layers.

Two members are public for operating the profiles yourself:

```csharp
using BoletoForBanrisul.Report.Utils;

// Where the temporary profiles live.
var root = ChromiumHtmlToPdfConverter.ProfilesRootDirectory;

// A manual sweep, useful at startup after a crash.
ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.FromHours(1));
```

## Testing

Tests are split across four projects under `Tests/`. The first three are deterministic and need no
credentials, no network and no browser:

```bash
dotnet test Tests/BoletoForBanrisul.Core.Tests
dotnet test Tests/BoletoForBanrisul.Client.Tests
dotnet test Tests/BoletoForBanrisul.Report.Tests
```

`Tests/BoletoForBanrisul.IntegrationTests` holds the tests that reach outside: those that call the
real Banrisul API, which need credentials, and those that render PDFs through a real Chrome, which
need the browser. All of them are disabled by default. See
[Tests/BoletoForBanrisul.IntegrationTests/README.md](Tests/BoletoForBanrisul.IntegrationTests/README.md)
to configure and run them.

## License

MIT — see [LICENSE](LICENSE).

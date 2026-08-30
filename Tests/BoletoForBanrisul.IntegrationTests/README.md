# Tests

Tests are split by project:

- `Tests/BoletoForBanrisul.Core.Tests`: deterministic tests of the Core models, validation and
  helpers, with no calls to the Banrisul API.
- `Tests/BoletoForBanrisul.Client.Tests`: deterministic tests of the HTTP client (authentication,
  retry policies, DI extensions), also without real calls.
- `Tests/BoletoForBanrisul.Report.Tests`: deterministic tests of report and PDF generation, with the
  real PDF converter replaced by a stub.
- `Tests/BoletoForBanrisul.IntegrationTests` (this project): the tests that reach outside the process.

This project holds two kinds of test, and they need different things:

| Tests | What they need |
| --- | --- |
| `AuthenticationTest`, `BoletoCreateTest`, `BoletoGetTest`, `BoletoCancelTest`, `BoletoChangeDueDateTest`, `BoletoGrantAbatementTest`, `IssueBoletoTest` | Sandbox credentials: they call the real API |
| `PdfGenerationTest` | A local Chrome/Chromium: it makes no network call |

Both kinds are gated by the same `RUN_BANRISUL_INTEGRATION_TESTS` variable, so running the PDF tests
means setting it even though no credential is involved.

## Running the unit tests

```bash
dotnet test Tests/BoletoForBanrisul.Core.Tests
dotnet test Tests/BoletoForBanrisul.Client.Tests
dotnet test Tests/BoletoForBanrisul.Report.Tests
```

## Running the integration tests

Set the environment variables:

```bash
CLIENT_ID=<your-client-id>
CLIENT_SECRET=<your-client-secret>
RUN_BANRISUL_INTEGRATION_TESTS=true
```

Then run:

```bash
dotnet test Tests/BoletoForBanrisul.IntegrationTests
```

The gate accepts `true` or `1`, in any casing. Anything else, including leaving it unset, disables
the tests, so `dotnet test Tests/BoletoForBanrisul.IntegrationTests` is safe to run as it is: the
tests report as *Skipped*, with no network call and no browser launched.

To run only the PDF tests, which need no credentials:

```bash
dotnet test Tests/BoletoForBanrisul.IntegrationTests --filter "FullyQualifiedName~PdfGenerationTest"
```

## Inspecting the generated PDFs

The PDF tests write their files to a temporary directory of their own, deleted when each test ends.
To keep them:

```bash
KEEP_BANRISUL_TEST_ARTIFACTS=true
```

The directory path is printed in the test output.

## Hunting intermittent failures

`repeat-pdf-tests.ps1` runs the PDF tests repeatedly and writes the full output of every failing run
to `pdf-test-failures/`: assert, exception and stack trace. It exists because an intermittent failure
is only useful if the message is captured at the moment it happens.

```powershell
.\repeat-pdf-tests.ps1                                                   # 30 runs of the whole class
.\repeat-pdf-tests.ps1 -Iterations 100 -Filter Should_Abort_Conversion_When_Cancelled
.\repeat-pdf-tests.ps1 -Iterations 200 -StopOnFirstFailure               # stop at the first failure
.\repeat-pdf-tests.ps1 -SkipBuild                                        # go straight to the runs
```

Each iteration also compares how many Chromium profiles exist before and after, to separate "the test
failed" from "the test failed because a profile was left behind".

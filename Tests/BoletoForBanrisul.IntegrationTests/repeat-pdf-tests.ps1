#Requires -Version 5.1
<#
.SYNOPSIS
    Roda os testes de geração de PDF repetidamente para caçar falhas intermitentes.

.DESCRIPTION
    Should_Abort_Conversion_When_Cancelled falhou uma vez e não reproduziu em 71 execuções
    posteriores. Uma falha assim só é útil se a mensagem for capturada quando ela acontecer - o que
    este script garante: cada execução que falha tem a saída completa gravada em disco, com o assert,
    a exceção e o stack trace.

    A cada iteração também é medida a quantidade de profiles do Chromium na raiz temporária, para
    separar "o teste falhou" de "o teste falhou porque sobrou profile".

.PARAMETER Iterations
    Quantas vezes repetir. Padrão: 30.

.PARAMETER Filter
    Filtro passado ao dotnet test. Padrão: PdfGenerationTest (a classe inteira, ~25s por iteração).
    Para martelar só o cancelamento, use Should_Abort_Conversion_When_Cancelled (~3s por iteração) -
    mas note que a falha original só apareceu com a classe inteira.

.PARAMETER StopOnFirstFailure
    Interrompe assim que a primeira falha for capturada, deixando o ambiente no estado do erro.

.PARAMETER SkipBuild
    Não compila antes de começar. Use quando já compilou e quer ir direto às iterações.

.PARAMETER LogDirectory
    Onde gravar as saídas das execuções que falharem. Padrão: ./pdf-test-failures.

.EXAMPLE
    .\repeat-pdf-tests.ps1
    Trinta execuções da classe inteira.

.EXAMPLE
    .\repeat-pdf-tests.ps1 -Iterations 100 -Filter Should_Abort_Conversion_When_Cancelled
    Cem execuções apenas do teste de cancelamento.

.EXAMPLE
    .\repeat-pdf-tests.ps1 -Iterations 200 -StopOnFirstFailure
    Roda até falhar e para, preservando o estado.
#>
[CmdletBinding()]
param(
    [int] $Iterations = 30,
    [string] $Filter = 'PdfGenerationTest',
    [switch] $StopOnFirstFailure,
    [switch] $SkipBuild,
    [string] $LogDirectory = (Join-Path $PSScriptRoot 'pdf-test-failures')
)

$ErrorActionPreference = 'Stop'

$projectPath = $PSScriptRoot
$profilesRoot = Join-Path $env:TEMP 'boletoforbanrisul-chromium-profiles'

# No PowerShell 5.1, 2>&1 sobre um executável nativo embrulha cada linha de stderr num ErrorRecord;
# com ErrorActionPreference = Stop isso derruba o script justamente quando um teste falha - que é o
# momento em que ele mais precisa continuar. Daí a captura ficar isolada aqui, com o preference
# relaxado só durante a chamada.
function Invoke-Dotnet {
    param([string[]] $Arguments)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try {
        # O ForEach desembrulha os ErrorRecord de volta para o texto puro do stderr; sem ele o log
        # fica salpicado da formatação de erro do PowerShell no meio da saída do xunit.
        $output = & dotnet @Arguments 2>&1 |
            ForEach-Object {
                if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.Exception.Message } else { $_ }
            } | Out-String
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }

    return [pscustomobject]@{ Output = $output; ExitCode = $code }
}

function Get-ProfileCount {
    if (Test-Path $profilesRoot) {
        return (Get-ChildItem $profilesRoot -Directory -ErrorAction SilentlyContinue).Count
    }
    return 0
}

# --- ambiente -------------------------------------------------------------------------------------

$previousRunFlag = $env:RUN_BANRISUL_INTEGRATION_TESTS
$env:RUN_BANRISUL_INTEGRATION_TESTS = 'true'

if (-not (Test-Path $LogDirectory)) {
    New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
}

Write-Host ''
Write-Host "Projeto     : $projectPath"
Write-Host "Filtro      : $Filter"
Write-Host "Iterações   : $Iterations"
Write-Host "Logs        : $LogDirectory"
Write-Host "Profiles em : $profilesRoot"
Write-Host ''

# --- build ----------------------------------------------------------------------------------------

if (-not $SkipBuild) {
    Write-Host 'Compilando...' -NoNewline
    $build = Invoke-Dotnet @('build', $projectPath, '--nologo', '-v', 'q')

    if ($build.ExitCode -ne 0) {
        Write-Host ' FALHOU' -ForegroundColor Red
        Write-Host $build.Output
        $env:RUN_BANRISUL_INTEGRATION_TESTS = $previousRunFlag
        exit 1
    }

    Write-Host ' ok'
    Write-Host ''
}

# --- laço -----------------------------------------------------------------------------------------

$failures = 0
$durations = New-Object System.Collections.Generic.List[double]
$startedAt = Get-Date

for ($i = 1; $i -le $Iterations; $i++) {
    $profilesBefore = Get-ProfileCount

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-Dotnet @(
        'test', $projectPath, '--nologo', '--no-build',
        '--filter', "FullyQualifiedName~$Filter",
        '-l', 'console;verbosity=detailed')
    $sw.Stop()

    $output = $run.Output
    $exitCode = $run.ExitCode
    $durations.Add($sw.Elapsed.TotalSeconds)

    $profilesAfter = Get-ProfileCount
    $leaked = $profilesAfter - $profilesBefore

    if ($exitCode -eq 0) {
        $suffix = ''
        if ($leaked -gt 0) { $suffix = " (+$leaked profiles)" }
        Write-Host ("[{0,4}/{1}] ok   {2,6:0.0}s{3}" -f $i, $Iterations, $sw.Elapsed.TotalSeconds, $suffix) -ForegroundColor DarkGray
        continue
    }

    # --- falhou: preserva tudo --------------------------------------------------------------------

    $failures++
    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $logFile = Join-Path $LogDirectory "falha-$stamp-iter$i.log"

    $header = @(
        "Iteração      : $i de $Iterations",
        "Momento       : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
        "Filtro        : $Filter",
        "Exit code     : $exitCode",
        "Duração       : $([math]::Round($sw.Elapsed.TotalSeconds, 1))s",
        "Profiles antes: $profilesBefore",
        "Profiles depois: $profilesAfter (delta $leaked)",
        ('-' * 100),
        ''
    ) -join [Environment]::NewLine

    ($header + $output) | Out-File -FilePath $logFile -Encoding utf8

    Write-Host ("[{0,4}/{1}] FALHOU {2,5:0.0}s  ->  {3}" -f $i, $Iterations, $sw.Elapsed.TotalSeconds, $logFile) -ForegroundColor Red

    # Trecho relevante direto no console, para não precisar abrir o log no caso comum.
    $lines = $output -split "`r?`n"
    $match = $lines | Select-String -Pattern '\[FAIL\]|^\s*Failed\s|Error Message' | Select-Object -First 1

    if ($match) {
        $from = [Math]::Max(0, $match.LineNumber - 1)
        $to = [Math]::Min($from + 28, $lines.Count - 1)

        Write-Host ''
        Write-Host ($lines[$from..$to] -join [Environment]::NewLine) -ForegroundColor Yellow
        Write-Host ''
    }

    if ($StopOnFirstFailure) {
        Write-Host 'Parando na primeira falha (-StopOnFirstFailure).' -ForegroundColor Yellow
        break
    }
}

# --- resumo ---------------------------------------------------------------------------------------

$env:RUN_BANRISUL_INTEGRATION_TESTS = $previousRunFlag

$executed = $durations.Count
$elapsed = (Get-Date) - $startedAt

Write-Host ''
Write-Host ('=' * 60)
Write-Host "Execuções   : $executed"
Write-Host "Falhas      : $failures"

if ($executed -gt 0) {
    $rate = [math]::Round(100.0 * $failures / $executed, 1)
    $stats = $durations | Measure-Object -Average -Minimum -Maximum
    Write-Host "Taxa        : $rate%"
    Write-Host ("Duração     : min {0:0.0}s  média {1:0.0}s  max {2:0.0}s" -f $stats.Minimum, $stats.Average, $stats.Maximum)
}

Write-Host "Tempo total : $([math]::Round($elapsed.TotalMinutes, 1)) min"
Write-Host ('=' * 60)

if ($failures -gt 0) {
    Write-Host ''
    Write-Host "Logs das falhas em: $LogDirectory" -ForegroundColor Yellow
    exit 1
}

Write-Host ''
Write-Host 'Nenhuma falha.' -ForegroundColor Green
exit 0

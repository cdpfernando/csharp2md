# Runs the slopwatch gate over first-party source only, from anywhere in the repo.
# Extra arguments are forwarded, e.g. `.slopwatch\analyze.ps1 --no-baseline --stats`.
#
# The exclusions cannot live in .slopwatch/config.json -- see README.md in this directory.

$excluded = @(
    '**/artifacts/**'
    '**/.scratch/**'
    '**/fixtures/**'
    '**/obj/**'
    '**/bin/**'
)

Push-Location (Split-Path -Parent $PSScriptRoot)
try
{
    # Slopwatch prints its banner on stderr; fold it into stdout so the shell does not report a failure.
    dotnet slopwatch analyze --exclude @excluded @args 2>&1 | ForEach-Object { [string]$_ }
}
finally
{
    Pop-Location
}

exit $LASTEXITCODE

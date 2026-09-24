$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "==> [1/6] Limpando saídas geradas conhecidas..." -ForegroundColor Cyan
Get-ChildItem -Path $root -Include "bin","obj" -Recurse -Directory | ForEach-Object {
    Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
}
Get-ChildItem -Path (Join-Path $root "local-feed") -Filter "*.nupkg" | ForEach-Object {
    Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
}

Write-Host "==> [2/6] Empacotando pacotes privados no feed local..." -ForegroundColor Cyan
dotnet pack "$root/package-source/PacotePrivado.Parametros/PacotePrivado.Parametros.csproj" -o "$root/local-feed" -c Release
if ($LASTEXITCODE -ne 0) { throw "Falha ao empacotar PacotePrivado.Parametros" }

dotnet pack "$root/package-source/PacotePrivado.Broker/PacotePrivado.Broker.csproj" -o "$root/local-feed" -c Release
if ($LASTEXITCODE -ne 0) { throw "Falha ao empacotar PacotePrivado.Broker" }

$solutions = @(
    "$root/src/SistemaA/SistemaA.slnx",
    "$root/src/SistemaB/SistemaB.slnx",
    "$root/src/SistemaC/SistemaC.slnx",
    "$root/src/SistemaD/SistemaD.slnx",
    "$root/src/SistemaE/SistemaE.slnx",
    "$root/src/SistemaB/Copias/SistemaE.Copia/SistemaE.Copia.slnx"
)

Write-Host "==> [3/6] Restaurando soluções..." -ForegroundColor Cyan
foreach ($sln in $solutions) {
    Write-Host "Restaurando $sln"
    dotnet restore $sln
    if ($LASTEXITCODE -ne 0) { throw "Falha ao restaurar $sln" }
}

Write-Host "==> [4/6] Compilando soluções em Release..." -ForegroundColor Cyan
foreach ($sln in $solutions) {
    Write-Host "Compilando $sln"
    dotnet build $sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar $sln" }
}

Write-Host "==> [5/6] Executando testes unitários..." -ForegroundColor Cyan
foreach ($sln in $solutions) {
    Write-Host "Testando $sln"
    dotnet test $sln -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Falha nos testes de $sln" }
}

Write-Host "==> [6/6] Executando testes de integridade do oráculo..." -ForegroundColor Cyan
dotnet test "$root/src/SistemaA/SistemaA.Testes/SistemaA.Testes.csproj" -c Release --no-build --filter "FullyQualifiedName~CorpusIntegrityTests"
if ($LASTEXITCODE -ne 0) { throw "Falha nos testes de integridade do oráculo" }

Write-Host "==> Build, testes e integridade concluídos com sucesso!" -ForegroundColor Green

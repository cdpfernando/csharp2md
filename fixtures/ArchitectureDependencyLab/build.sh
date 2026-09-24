#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> [1/6] Limpando saídas geradas conhecidas..."
find "$SCRIPT_DIR" -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
rm -f "$SCRIPT_DIR/local-feed"/*.nupkg 2>/dev/null || true

echo "==> [2/6] Empacotando pacotes privados no feed local..."
dotnet pack "$SCRIPT_DIR/package-source/PacotePrivado.Parametros/PacotePrivado.Parametros.csproj" -o "$SCRIPT_DIR/local-feed" -c Release
dotnet pack "$SCRIPT_DIR/package-source/PacotePrivado.Broker/PacotePrivado.Broker.csproj" -o "$SCRIPT_DIR/local-feed" -c Release

SOLUTIONS=(
  "$SCRIPT_DIR/src/SistemaA/SistemaA.slnx"
  "$SCRIPT_DIR/src/SistemaB/SistemaB.slnx"
  "$SCRIPT_DIR/src/SistemaC/SistemaC.slnx"
  "$SCRIPT_DIR/src/SistemaD/SistemaD.slnx"
  "$SCRIPT_DIR/src/SistemaE/SistemaE.slnx"
  "$SCRIPT_DIR/src/SistemaB/Copias/SistemaE.Copia/SistemaE.Copia.slnx"
)

echo "==> [3/6] Restaurando soluções..."
for sln in "${SOLUTIONS[@]}"; do
  echo "Restaurando $sln"
  dotnet restore "$sln"
done

echo "==> [4/6] Compilando soluções em Release..."
for sln in "${SOLUTIONS[@]}"; do
  echo "Compilando $sln"
  dotnet build "$sln" -c Release --no-restore
done

echo "==> [5/6] Executando testes unitários..."
for sln in "${SOLUTIONS[@]}"; do
  echo "Testando $sln"
  dotnet test "$sln" -c Release --no-build
done

echo "==> [6/6] Executando testes de integridade do oráculo..."
dotnet test "$SCRIPT_DIR/src/SistemaA/SistemaA.Testes/SistemaA.Testes.csproj" -c Release --no-build --filter "FullyQualifiedName~CorpusIntegrityTests"

echo "==> Build, testes e integridade concluídos com sucesso!"

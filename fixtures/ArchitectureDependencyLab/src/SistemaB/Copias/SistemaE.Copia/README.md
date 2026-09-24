# SistemaE.Copia

<!-- SCENARIO:STR-003 -->
Este diretório contém uma cópia estrutural do SistemaE isolada dentro de `src/SistemaB/Copias/SistemaE.Copia`.

## Regras de Isolamento
1. Possui sua própria solução `SistemaE.Copia.slnx`.
2. Todos os assemblies e namespaces terminam em `.Copia`.
3. Reproduz exatamente as 28 Project References internas entre projetos `SistemaE.Copia.*`.
4. Não é referenciado nem incluído em `SistemaB.slnx`.
5. Não possui dependências nem conexões HTTP/mensageria com SistemaB.
6. Serve como oráculo negativo para comprovar que semelhança de código e nomes aninhados não fundem identidades de soluções independentes.

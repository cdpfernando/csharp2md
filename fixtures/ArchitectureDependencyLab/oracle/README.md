# Oráculo Normativo do Corpus `architecture-dependency-lab`

Este diretório contém os oráculos normativos de validação para analisadores estáticos de código C#/.NET 10.

## Arquivos
- `scenarios.json`: catálogo dos 63 cenários mapeados individualmente no código através de marcadores `// SCENARIO:<id>`.
- `project-references.json`: mapeamento exato das 87 `ProjectReference` do corpus (incluindo as 28 da cópia aninhada isolada).

## Estados Permitidos no Oráculo
- `confirmed`: dependência ou invocação direta observável e semanticamente confirmada.
- `candidate`: dependência plausível por convenção, configuração em tempo de execução ou reflexão.
- `unknown`: relacionamento ambíguo ou coincidente que requer evidência posterior.
- `open-frontier`: fronteira aberta ou lacuna deliberada (ex: evento sem consumidor ou migração faltante).
- `absent`: relação ausente ou lookalike (usado nos 12 cenários negativos `NEG-001` a `NEG-012`).

## Regras de Validação do Analisador
1. Cada solução (`.slnx`) deve ser analisada isoladamente no seu boundary de compilação.
2. Não devem ser inferidas ligações entre sistemas diferentes (A, B, C, D e E são independentes).
3. A cópia em `src/SistemaB/Copias/SistemaE.Copia` não pode ser mesclada com `SistemaB` nem com `SistemaE`.
4. Os cenários `NEG-*` devem apontar para lookalikes no código e ser avaliados como `absent`.

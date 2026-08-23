# 10 — Compact relation retrieval index

**Feature slug:** `compact-retrieval-index` (`.specs/features/compact-retrieval-index/` once Specify starts)
**Roadmap row:** §9 P0 "Índice de recuperação compacto"
**Fase (tlc-spec-driven):** Implemented
**Execução:** agnóstica de agente/LLM — processo lido de `.agents/skills/tlc-spec-driven/` e validado pelos
scripts empacotados no próprio processo. Ver roadmap § "Estado do documento".

**What to build:** A execução continua oferecendo consultas por projeto, origem, destino, tipo de relação e
estado de resolução, mas deixa de copiar o `RetrievalRelationEntry` completo para cada família. Cada relação
derivada é serializada uma única vez; os índices secundários guardam somente postings compactos que apontam
para esse registro. A projeção também deixa de criar um diretório por chave e de reserializar todo o shard a
cada item apenas para verificar o teto de tamanho. `raw/facts` permanece a autoridade e não é alterado.

**Blocked by:** 02 — Relation retrieval index & sharding (Verified; blocker already complete, so this ticket
can start immediately).

**Status:** implemented-awaiting-verifier

**Validação independente:** pendente em `.specs/features/compact-retrieval-index/validation.md`.

- [x] Cada relação aparece como payload derivado completo exatamente uma vez, independentemente de quantas
      famílias de consulta a referenciem
- [x] As consultas por projeto, `source_id`, `target_id`, `relation_kind` e `resolution` preservam os mesmos
      resultados observáveis do índice atual sem abrir o agregado factual completo
- [x] Os índices secundários contêm apenas identidades/localizadores compactos e nunca repetem evidência,
      proveniência ou texto observado da relação
- [x] Todo shard continua respeitando o teto UTF-8 de 262144 bytes e uma entrada individual acima do teto
      falha de forma determinística, sem ser descartada
- [x] Uma fixture de alta cardinalidade prova que o número de diretórios não cresce um-para-um com chaves
      únicas de origem ou destino
- [x] Um teste instrumentado prova que acrescentar uma entrada não reserializa o prefixo inteiro do shard;
      o trabalho de serialização cresce linearmente com entradas e postings emitidos
- [x] Grupos UNKNOWN são persistidos em um único artefato; o resumo mantém somente totais e uma referência,
      sem repetir listas de `relation_id`
- [x] A mudança incrementa a versão do schema derivado e impede que leitores confundam silenciosamente o
      formato compacto com o formato anterior
- [x] Duas execuções com a mesma entrada continuam produzindo `analysis_run_id`, postings e bytes de índice
      determinísticos
- [x] Uma medição reproduzível sobre um corpus grande registra bytes, arquivos, tempo de projeção e pico de
      memória antes/depois, comprovando redução em todas as quatro métricas

**Próximo passo:** um Verifier independente deve produzir `validation.md`. Somente após PASS, marque `Fase`
como `Verified` e mude a linha correspondente do roadmap para `CONCLUÍDO`.

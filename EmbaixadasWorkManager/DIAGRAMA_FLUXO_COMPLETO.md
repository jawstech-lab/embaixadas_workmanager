# Diagrama de Fluxo Completo do Sistema

## 🔄 **Fluxo End-to-End Completo**

```
╔═══════════════════════════════════════════════════════════════════════════╗
║                    FASE 1: RECEBIMENTO E INÍCIO                           ║
╚═══════════════════════════════════════════════════════════════════════════╝

1. fila-execucao recebe ID da execução
   ↓
2. Worker → MessageProcessorService → ProcessorService
   ↓
3. Busca Execucao do DynamoDB (GetExecucaoAsync)
   ↓
4. ✨ NOVO: Grava em Tabelas de Performance
   ├─ ExecucaoEmpresaStatus (auditoria completa)
   │  └─ PK: EMP#MA, SK: DATA#2025-10-09...#execId
   └─ ExecucaoResumoView (última execução)
      └─ PK: VIEW#LAST_EXEC, SK: EMP#MA
      └─ Condição: Atualiza só se data mais recente
   ↓
5. Atualiza Status → EmProcessamento
   ↓

╔═══════════════════════════════════════════════════════════════════════════╗
║                 FASE 2: PROCESSAMENTO DE VERIFICAÇÕES                     ║
╚═══════════════════════════════════════════════════════════════════════════╝

6. ProcessExecucaoAsync()
   ├─ Busca verificações (específicas, por embaixadas ou todas)
   └─ Para cada verificação:
       ├─ VerificacaoProcessorService
       ├─ Cria ExecucaoVerificacao
       └─ Envia ID para fila-execucao-query
   ↓
7. Atualiza Execucao:
   ├─ Status: AguardandoProcessamento
   ├─ QuantidadeVerificacoes: N
   └─ VerificacoesProcessadas: 0
   ↓

╔═══════════════════════════════════════════════════════════════════════════╗
║              FASE 3: EXECUÇÃO DE QUERIES E RESULTADOS                     ║
╚═══════════════════════════════════════════════════════════════════════════╝

8. QueryExecutionProcessor (processa fila-execucao-query)
   ├─ Busca ExecucaoVerificacao
   ├─ Executa SQL
   ├─ Salva resultados detalhados na tabela Resultado
   │  └─ PK: EXEC#<ExecId>#VERIF#<VerifId>
   │  └─ SK: ERRO#<Timestamp>#<Seq>
   │  └─ GSI1_PK: EXEC#<ExecId> (para agregação)
   └─ Envia resultado para fila-execucao-processo
   ↓

╔═══════════════════════════════════════════════════════════════════════════╗
║            FASE 4: CONSOLIDAÇÃO E FINALIZAÇÃO                             ║
╚═══════════════════════════════════════════════════════════════════════════╝

9. ProcessoProcessorService (processa fila-execucao-processo)
   ├─ Busca ExecucaoVerificacao
   ├─ Busca Execucao
   ├─ Atualiza contadores:
   │  ├─ VerificacoesProcessadas++
   │  ├─ VerificacoesComErro++ (se erro)
   │  └─ TotalApontamentos += resultado.TotalRecords
   ↓
10. Verifica se TODAS as verificações foram processadas:
    totalProcessadas >= QuantidadeVerificacoes
    ↓

╔═══════════════════════════════════════════════════════════════════════════╗
║          FASE 5: PÓS-PROCESSAMENTO (PIPELINE EXTENSÍVEL)                  ║
╚═══════════════════════════════════════════════════════════════════════════╝

11. ✨ NOVO: Pipeline de Pós-Processamento Acionado
    ↓
12. PostProcessingPipeline.ExecuteAsync()
    ↓
13. Ordena steps habilitados (Order crescente)
    ↓
14. STEP 1 (Order: 1): AgrupamentoStep
    ├─ Agrupa ExecucaoVerificacao por status
    └─ Tempo: ~100ms
    ↓
15. STEP 2 (Order: 2): ExclusaoRegistrosStep
    └─ DESABILITADO (pulado)
    ↓
16. ✨ STEP 3 (Order: 3): AgregacaoResultadosStep
    ↓
    ├─ ETAPA 1: Busca Otimizada com GSI
    │  ├─ Query no GSI_Agregacao
    │  ├─ GSI1_PK = "EXEC#<ExecucaoId>"
    │  ├─ Retorna TODOS os apontamentos da execução
    │  └─ Performance: 25x mais rápido (Query vs Scan)
    ↓
    ├─ ETAPA 2: Agrupamento em Memória
    │  ├─ Agrupa por: Empresa + Tabela + Campo + Ref + Tipo
    │  ├─ Conta apontamentos por grupo
    │  ├─ Calcula total de apontamentos
    │  └─ Identifica empresas únicas
    ↓
    ├─ ETAPA 3: Inserção em ResultadoAgregado
    │  ├─ PK: EXEC#<ExecucaoId>
    │  ├─ SK: EMP#<Emp>#<Tab>#<Campo>#<Ref>#<Tipo>
    │  ├─ QTD: Quantidade do grupo
    │  └─ Insert em lotes de 25
    ↓
    └─ ETAPA 4: Atualização Condicional
       ├─ Para cada empresa com apontamentos
       ├─ Atualiza TotalApontamentos em ExecucaoResumoView
       ├─ Condição: ExecucaoId = :execId (SEGURANÇA)
       └─ Evita sobrescrever execuções mais recentes
    ↓
17. Pipeline retorna resultado completo
    ↓
18. Logs detalhados de todas as etapas
    ↓

╔═══════════════════════════════════════════════════════════════════════════╗
║                        FASE 6: FINALIZAÇÃO                                ║
╚═══════════════════════════════════════════════════════════════════════════╝

19. Salva Execucao no DynamoDB com status final:
    ├─ Status: FinalizadaComSucesso ou FinalizadaComErro
    ├─ DataFim: timestamp
    ├─ TotalApontamentos: total calculado
    └─ Erros: lista de erros detalhados (se houver)
    ↓
20. Sistema pronto para próxima execução
```

---

## 🎯 **Pontos de Integração das Novas Funcionalidades**

### **Integração 1: Performance por Empresa**
```
ProcessorService.cs (linha ~60)
  └─ Logo após: GetExecucaoAsync()
  └─ Chama: ExecucaoEmpresaService.GravarExecucaoPorEmpresasAsync()
```

### **Integração 2: Pipeline de Pós-Processamento**
```
ProcessoProcessorService.cs (linha ~120)
  └─ Quando: totalProcessadas >= QuantidadeVerificacoes
  └─ Chama: ExecutarPosProcessamentoAsync()
```

### **Integração 3: Agregação de Resultados**
```
PostProcessingPipeline (automático)
  └─ Order: 3 (após AgrupamentoStep)
  └─ Executa: AgregacaoResultadosStep.ExecuteAsync()
```

---

## 📊 **Dados em Cada Tabela**

### **ExecucaoEmpresaStatus**
```
Quando: Logo no início (após buscar execução)
Quantos: 1 registro por empresa
Exemplo: 3 registros (MA, RS, SP)
```

### **ExecucaoResumoView**
```
Quando: Logo no início + atualização no final
Quantos: 1 registro por empresa
Atualizações: 2x (início: Status, final: TotalApontamentos)
```

### **Resultado**
```
Quando: Durante execução de queries
Quantos: N apontamentos (variável)
Exemplo: 150 apontamentos de erro
```

### **ResultadoAgregado**
```
Quando: Pós-processamento (final)
Quantos: N grupos (variável)
Exemplo: 10 grupos agregados
```

---

## 🔧 **Services Implementados**

```
ExecucaoEmpresaService
  └─ Grava performance por empresa

PostProcessingPipeline
  ├─ Gerencia execução de steps
  └─ Coleta resultados

AgrupamentoStep
  └─ Agrupa ExecucaoVerificacao

ExclusaoRegistrosStep
  └─ Exclui registros antigos (desabilitado)

AgregacaoResultadosStep
  ├─ Busca com GSI
  ├─ Agrupa em memória
  ├─ Insere agregados
  └─ Atualiza totais
```

---

## 📈 **Resumo de Performance**

| Fase | Operação | Tempo Estimado |
|------|----------|----------------|
| 1 | Buscar execução | ~10ms |
| 1 | Gravar performance | ~50ms |
| 2 | Processar verificações | ~500ms |
| 3 | Executar queries | ~5000ms (variável) |
| 4 | Consolidar resultados | ~20ms por verificação |
| 5 | Pipeline pós-processamento | ~850ms |
| - | Step 1: Agrupamento | ~100ms |
| - | Step 3: Agregação GSI | ~750ms |
|   | **Total (médio)** | **~6.5 segundos** |

**Nota**: Tempo varia conforme quantidade de verificações e complexidade das queries.

---

Este é o sistema completo implementado! 🎉




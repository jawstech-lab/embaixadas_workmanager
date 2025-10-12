# Gargalos de Performance Identificados

## 🚨 **Problemas Críticos**

### **1. GetAllAsync() - SCAN Completo (Muito Lento!)**

#### **Locais Afetados**

| Arquivo | Linha | Operação | Problema |
|---------|-------|----------|----------|
| `AgregacaoResultadosStep.cs` | 194 | `GetAllAsync<ExecucaoVerificacao>()` | SCAN de TODA a tabela |
| `AgrupamentoStep.cs` | 115 | `GetAllAsync<ExecucaoVerificacao>()` | SCAN de TODA a tabela |
| `DynamoDbService.cs` | 282 | `GetAllAsync<Verificacao>()` | SCAN de TODA a tabela |
| `DynamoDbService.cs` | 304 | `GetAllAsync<Verificacao>()` | SCAN de TODA a tabela |

#### **Impacto**

```
SCAN completo da tabela:
- 10 registros: ~50ms
- 100 registros: ~200ms
- 1.000 registros: ~2 segundos
- 10.000 registros: ~20 segundos ❌❌❌
- 100.000 registros: ~200 segundos ❌❌❌

Performance: O(n) - lê TODOS os registros
Custo: Alto - consome RCUs de todos os registros
```

---

### **2. WaitTimeSeconds = 10 (Long Polling)**

#### **Configuração Atual**

```json
{
  "SQS": {
    "WaitTimeSeconds": 10  ← Espera até 10 segundos por mensagens
  }
}
```

#### **Impacto**

```
Se não há mensagens na fila:
- Worker fica esperando 10 segundos
- Mesmo que chegue mensagem, só lê após o ciclo atual
- Latência de até 10 segundos

Ciclo do Worker:
1. ReceiveMessages: 10s (esperando)
2. ProcessExecutionQueue: 10s
3. ProcessProcessoQueue: 10s  
4. Delay: 30s
Total: ~60 segundos por ciclo ❌
```

---

## 📊 **Análise Detalhada**

### **AgregacaoResultadosStep - PROBLEMA CRÍTICO**

```csharp
// LINHA 194 - MUITO LENTO!
var todasVerificacoes = await _dynamoDbService.GetAllAsync<ExecucaoVerificacao>();
var verificacoesDaExecucao = todasVerificacoes
    .Where(v => v.ExecucaoId == execucaoId)  // Filtra EM MEMÓRIA
    .ToList();

Problema:
- Busca TODAS as ExecucaoVerificacao (pode ser 100.000+)
- Filtra apenas as da execução (ex: 10)
- Desperdiça 99.99% da leitura
- Lentidão exponencial conforme cresce
```

### **DynamoDbService.GetVerificacoesPorEmbaixadasAsync - PROBLEMA CRÍTICO**

```csharp
// LINHA 304 - MUITO LENTO!
var todasVerificacoes = await GetAllAsync<Verificacao>();
var verificacoesFiltradas = todasVerificacoes
    .Where(v => v.IdEmbaixadas != null && 
                v.IdEmbaixadas.Any() && 
                v.IdEmbaixadas.Any(idEmb => idEmbaixadas.Contains(idEmb)))
    .ToList();

Problema:
- Busca TODAS as Verificacao (pode ser milhares)
- Filtra em memória
- Muito lento
```

---

## ✅ **SOLUÇÕES**

### **1. Criar GSI em ExecucaoVerificacao**

#### **GSI: GSI_ExecucaoId**

```
GSI1_PK: ExecucaoId
GSI1_SK: VerificacaoId (ou DataCriacao)

Permite:
Query ExecucaoId = "abc-123"
Retorna: APENAS as ExecucaoVerificacao desta execução

Performance: O(log n) vs O(n)
Ganho: 100-1000x mais rápido
```

#### **Implementação**

```csharp
// ANTES (LENTO)
var todasVerificacoes = await _dynamoDbService.GetAllAsync<ExecucaoVerificacao>();
var verificacoesDaExecucao = todasVerificacoes
    .Where(v => v.ExecucaoId == execucaoId)
    .ToList();

// DEPOIS (RÁPIDO)
var verificacoesDaExecucao = await _dynamoDbService.QueryByGSIAsync<ExecucaoVerificacao>(
    "GSI_ExecucaoId", 
    "ExecucaoId", 
    execucaoId);
```

---

### **2. Reduzir WaitTimeSeconds**

#### **Configuração Recomendada**

```json
{
  "SQS": {
    "WaitTimeSeconds": 5,  ← Reduzir de 10 para 5
    "MaxNumberOfMessages": 10  ← Aumentar de 5 para 10
  }
}
```

#### **Benefícios**

```
ANTES:
- WaitTime: 10s
- MaxMessages: 5
- Ciclo: ~60s

DEPOIS:
- WaitTime: 5s
- MaxMessages: 10
- Ciclo: ~30s

Ganho: 2x mais rápido + processa mais mensagens
```

---

### **3. Remover GetAllAsync do Pós-Processamento**

#### **Opção A: Usar GSI (RECOMENDADO)**

```csharp
// Criar GSI em ExecucaoVerificacao
[DynamoDBGlobalSecondaryIndexHashKey("GSI1_PK_ExecId", "GSI_ExecucaoId")]
public string ExecucaoIdGSI { get; set; }  // = ExecucaoId

// Query otimizada
var verificacoes = await QueryByGSI(...);
```

#### **Opção B: Desabilitar Step Temporariamente**

```json
{
  "PostProcessing": {
    "Agrupamento": {
      "Enabled": false  ← Desabilitar temporariamente
    },
    "AgregacaoResultados": {
      "Enabled": true
    }
  }
}
```

---

### **4. Otimizar DynamoDbService.GetTodasVerificacoesAsync**

#### **Criar GSI ou Cache**

```csharp
// Opção 1: Cache em memória (5 minutos)
private static List<Verificacao>? _cacheVerificacoes;
private static DateTime _cacheExpiration;

public async Task<List<Verificacao>> GetTodasVerificacoesAsync()
{
    if (_cacheVerificacoes != null && DateTime.UtcNow < _cacheExpiration)
    {
        return _cacheVerificacoes;
    }
    
    var verificacoes = await GetAllAsync<Verificacao>();
    _cacheVerificacoes = verificacoes.ToList();
    _cacheExpiration = DateTime.UtcNow.AddMinutes(5);
    
    return _cacheVerificacoes;
}
```

---

## 📊 **Estimativa de Tempo Atual**

### **Processamento de 1 Mensagem**

```
1. ReceiveMessages (SQS): 0-10s (long polling)
2. ProcessExecucaoMessageAsync:
   ├─ GetExecucaoAsync: ~10ms
   ├─ GravarExecucaoPorEmbaixadas: ~100ms
   ├─ GetTodasVerificacoesAsync: ~5-20s ❌ LENTO (SCAN)
   ├─ ProcessarVerificacoes: ~500ms
   └─ UpdateAsync: ~20ms
3. DeleteMessage: ~10ms

TOTAL POR MENSAGEM: 5-30 segundos ❌
```

### **Ciclo Completo do Worker**

```
1. ProcessExecutionQueue: 5-30s (se tiver mensagens)
2. ProcessProcessoQueue: 5-30s (se tiver mensagens)
3. Delay: 30s

TOTAL: 40-90 segundos por ciclo ❌
```

---

## 🚀 **OTIMIZAÇÕES RÁPIDAS**

### **AÇÃO IMEDIATA 1: Reduzir WaitTimeSeconds**

```json
{
  "SQS": {
    "WaitTimeSeconds": 2,  ← De 10 para 2
    "MaxNumberOfMessages": 10  ← De 5 para 10
  }
}
```

**Ganho**: Ciclo de 90s → 30s (3x mais rápido)

---

### **AÇÃO IMEDIATA 2: Desabilitar AgrupamentoStep**

```json
{
  "PostProcessing": {
    "Agrupamento": {
      "Enabled": false  ← Desabilitar (usa GetAllAsync)
    }
  }
}
```

**Ganho**: Remove SCAN no pós-processamento

---

### **AÇÃO IMEDIATA 3: Cache de Verificações**

Se usa `BuscarTodasVerificacoesSeVazio = true`, implementar cache.

---

## 📈 **Ganho Estimado com Otimizações**

| Cenário | Antes | Depois | Ganho |
|---------|-------|--------|-------|
| Ciclo sem mensagens | 40s | 10s | **4x** |
| Processar 1 mensagem | 25s | 2s | **12x** |
| Processar 10 mensagens | 250s | 20s | **12x** |

---

## 🎯 **RECOMENDAÇÃO IMEDIATA**

### **Arquivo: appsettings.json**

```json
{
  "SQS": {
    "WaitTimeSeconds": 2,         ← Reduzir de 10 para 2
    "MaxNumberOfMessages": 10     ← Aumentar de 5 para 10
  },
  "Processamento": {
    "BuscarTodasVerificacoesSeVazio": false  ← Desabilitar se possível
  },
  "PostProcessing": {
    "Agrupamento": {
      "Enabled": false            ← Desabilitar temporariamente
    }
  }
}
```

**Ganho esperado**: 5-10x mais rápido

---

## 📋 **Otimizações de Médio Prazo**

### **1. Criar GSI em ExecucaoVerificacao**

```sql
-- Adicionar ao modelo
[DynamoDBGlobalSecondaryIndexHashKey("ExecucaoId", "GSI_ExecucaoId")]
public string ExecucaoId { get; set; }

-- Criar índice no DynamoDB
IndexName: GSI_ExecucaoId
PK: ExecucaoId
SK: DataCriacao (ou VerificacaoId)
```

### **2. Implementar Cache**

```csharp
// Cache de Verificações (5 minutos)
// Cache de ExecucaoVerificacao por ExecucaoId
```

### **3. Usar Query em vez de Scan**

Sempre que possível, usar Query com chave primária ou GSI.

---

## ⏱️ **Tempo Atual Estimado**

Com base no código:

```
Ciclo do Worker:
├─ ReceiveMessages: 10s (WaitTime)
├─ ProcessExecutionQueue: 
│  ├─ GetTodasVerificacoesAsync: 5-20s ❌ SCAN
│  └─ Processamento: 1-2s
├─ ProcessProcessoQueue:
│  └─ Pós-processamento: 1-5s
│  └─ AgregacaoResultadosStep: 2-10s (GetAllAsync) ❌
└─ Delay: 30s

TOTAL: 49-67 segundos por ciclo ❌
```

---

**Quer que eu implemente as otimizações imediatas?** 🚀

Posso:
1. Atualizar `appsettings.json` com valores otimizados
2. Adicionar cache em `GetTodasVerificacoesAsync`
3. Criar script para adicionar GSI

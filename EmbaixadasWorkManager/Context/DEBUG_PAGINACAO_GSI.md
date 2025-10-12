# Debug: Problema de Paginação no GSI

## Problema

**Grupo PA**: `PA|3c8fad11-...|UNTRMT|PER_TOT|2024-02-01T00:00:00.000Z|VIF|1`
- **DynamoDB tem**: 96 registros com GSI1_PK e GSI1_SK corretos ✅
- **GSI Query retorna**: 90 registros ❌
- **Diferença**: 6 registros não são retornados

## Verificações Realizadas

✅ **Todos os 96 registros têm**:
- GSI1_PK preenchido
- GSI1_SK preenchido
- Valores corretos e iguais

❌ **Query GSI retorna apenas 90**

---

## Possíveis Causas

### **1. Paginação Incompleta**

**Código atual**:
```csharp
do
{
    var request = new QueryRequest
    {
        TableName = "Resultado",
        IndexName = "GSI_Agregacao",
        KeyConditionExpression = "GSI1_PK = :execId",
        ExclusiveStartKey = lastEvaluatedKey
        // SEM Limit definido - usa padrão do DynamoDB
    };
    
    var response = await _dynamoClient.QueryAsync(request);
    // Processar items...
    lastEvaluatedKey = response.LastEvaluatedKey;
    
} while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);
```

**Possível problema**:
- Query pode estar parando antes de ler todas as páginas
- `LastEvaluatedKey` pode não estar sendo retornado corretamente

---

### **2. Eventually Consistent Read (Mais Provável)**

**GSI usa leitura Eventually Consistent por padrão**:
- DynamoDB replica dados do GSI de forma assíncrona
- Pode haver delay entre a gravação na tabela base e a disponibilidade no GSI
- Os 6 registros podem ter sido gravados recentemente

**Exemplo**:
```
T0: 90 registros gravados → GSI sincronizado ✅
T1: 6 registros gravados → GSI ainda não sincronizado ⏳
T2: Query executada → Retorna apenas 90 ❌
T3: GSI sincroniza → Agora tem 96 ✅
```

**Solução**:
- Aguardar alguns segundos antes da query
- Usar `ConsistentRead = true` (se suportado - GSI geralmente não suporta)

---

### **3. Limite de 1MB por Request**

**DynamoDB tem limites**:
- Máximo de 1MB de dados por request
- Se ultrapassar, retorna `LastEvaluatedKey` para continuar

**Verificação**:
- Total de apontamentos: 291.950
- Se cada item tem ~500 bytes → ~146 MB total
- Deveria ter múltiplas páginas (146 páginas de 1MB)

---

### **4. Problema de Order no GSI**

Se os 6 registros tiverem `GSI1_SK` que coloca eles no "meio" da paginação:
- Página 1: items 1-1000 ✅
- Página 2: items 1001-2000 ✅ (mas os 6 estão aqui e não são processados)
- Página 3: items 2001-3000 ✅

---

## Logs de Debug Adicionados

### **1. Contagem de Páginas**
```csharp
int pageCount = 0;
do
{
    pageCount++;
    _logger.LogDebug("GSI Query - Página {Page}: Buscando registros...", pageCount);
    // ...
    _logger.LogInformation(
        "GSI Query - Página {Page}: Retornou {Count} itens. " +
        "ScannedCount={ScannedCount}, HasMorePages={HasMore}",
        pageCount, response.Items.Count, response.ScannedCount, 
        response.LastEvaluatedKey != null);
} while (...);
```

### **2. Log dos PKs Encontrados**
```csharp
var pksPA = resultados
    .Where(r => r.Empresa == "PA" && ...)
    .Select(r => new { r.PK, r.Referencia, r.TipoApontamento, r.Nivel })
    .Take(10)
    .ToList();

_logger.LogDebug("DEBUG: Primeiros 10 PKs do grupo PA encontrados:");
foreach (var item in pksPA)
{
    _logger.LogDebug("  PK={PK}, Ref={Ref}, Tipo={Tipo}, Nivel={Nivel}", ...);
}
```

---

## Próximos Passos

### **1. Verificar Logs de Paginação**

Procurar nos logs:
```
[INFO] GSI Query - Página 1: Retornou 1000 itens. HasMorePages=True
[INFO] GSI Query - Página 2: Retornou 1000 itens. HasMorePages=True
...
[INFO] GSI Query - Página N: Retornou 500 itens. HasMorePages=False
[INFO] Busca no GSI concluida. Total de N páginas processadas. Total: 291950
```

**Verificar**:
- Quantas páginas foram processadas?
- Última página tem `HasMorePages=False`?
- Total de items = soma de todos os items das páginas?

---

### **2. Comparar PKs**

**No DynamoDB Console**:
```
Query na tabela Resultado com Filter:
- Empresa = "PA"
- VerificacaoId = "3c8fad11-..."
- Tabela = "UNTRMT"
- Campo = "PER_TOT"

Copiar os 96 PKs
```

**Nos Logs**:
```
Verificar os PKs logados
Comparar com os 96 do DynamoDB
```

**Identificar quais 6 PKs estão faltando**

---

### **3. Verificar Timestamp dos Registros**

Se os 6 registros foram inseridos recentemente:
```
Verificar `timestamp` ou `createdAt` dos 96 registros
```

Se os 6 são os mais recentes → Problema de Eventually Consistency

---

### **4. Adicionar Delay (Teste)**

```csharp
// Antes da Query GSI
_logger.LogInformation("Aguardando 5s para consistência do GSI...");
await Task.Delay(TimeSpan.FromSeconds(5));

// Executar query
var resultados = await BuscarApontamentosAsync(execucaoId);
```

Se com delay retorna 96 → **Confirma problema de Eventually Consistency**

---

## Soluções

### **Solução 1: Aguardar Consistência**
```csharp
// Adicionar delay antes de buscar
await Task.Delay(TimeSpan.FromSeconds(10));
```

### **Solução 2: Query com Retry**
```csharp
int tentativa = 0;
int countEsperado = execucao.TotalApontamentos;
List<Resultado> resultados;

do
{
    tentativa++;
    resultados = await BuscarApontamentosAsync(execucaoId);
    
    if (resultados.Count < countEsperado)
    {
        _logger.LogWarning(
            "Tentativa {Tentativa}: Encontrados {Count}/{Expected}. " +
            "Aguardando 5s para nova tentativa...",
            tentativa, resultados.Count, countEsperado);
        
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
} while (resultados.Count < countEsperado && tentativa < 3);
```

### **Solução 3: Query na Tabela Base ao Invés do GSI**
```csharp
// Usar Scan com Filter ao invés de GSI Query
// Mais lento, mas consistente
```

---

## Status

🔍 **Investigação em andamento**
- Logs de paginação adicionados
- Aguardando próxima execução para análise


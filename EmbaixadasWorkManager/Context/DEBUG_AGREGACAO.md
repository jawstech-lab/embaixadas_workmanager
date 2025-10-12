# Debug: Discrepância na Contagem do ResultadoAgregado

## Problema Identificado

**Grupo**: `EMP#PA#VER#3c8fad11-24da-45f9-8bb1-464d5aca1385#UNTRMT#PER_FER#2024-02-01T00:00:00.000Z#VIF`

- **ResultadoAgregado**: QTD = 90
- **Tabela Resultado**: 94 registros reais
- **Diferença**: 4 registros não contabilizados ❌

---

## Investigação

### **1. Verificar se TODOS os Registros Estão Sendo Buscados**

**Query GSI**:
```csharp
do
{
    var request = new QueryRequest
    {
        TableName = "Resultado",
        IndexName = "GSI_Agregacao",
        KeyConditionExpression = "GSI1_PK = :execId",
        ExclusiveStartKey = lastEvaluatedKey  // ← Paginação
    };
    
    var response = await _dynamoClient.QueryAsync(request);
    
    // Processar items...
    
    lastEvaluatedKey = response.LastEvaluatedKey;
    
} while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);
```

**✅ Código parece correto** - Deveria buscar TODOS os registros.

---

### **2. Verificar Logs de Contagem**

**Adicionar logs detalhados**:

```csharp
// No BuscarApontamentosAsync
_logger.LogInformation("Busca no GSI concluida. Total de apontamentos encontrados: {Total}", resultados.Count);

// No AgruparResultados
_logger.LogDebug("Iniciando agrupamento de {Count} resultados em memoria", resultados.Count);
_logger.LogDebug("Agrupamento concluido. Grupos: {Grupos}, Total: {Total}", grupos.Count, totalApontamentos);

// No InserirResultadosAgregadosAsync
_logger.LogInformation("Inserindo {Count} grupos na tabela ResultadoAgregado", grupos.Count);
```

**Verificar nos logs**:
1. Quantos registros foram buscados do GSI?
2. Quantos foram processados no agrupamento?
3. Quantos grupos foram criados?

---

### **3. Possíveis Causas da Discrepância**

#### **Causa A: Campos Vazios/Nulos**

Se algum campo crítico estiver vazio, pode causar agrupamento incorreto:

```csharp
// ChaveAgrupamento
public string ToKey()
{
    return $"{Empresa}|{VerificacaoId}|{Tabela}|{Campo}|{Referencia}|{TipoApontamento}|{Nivel}";
}
```

**Exemplo problemático**:
```
Registro 1: "PA|verif-123|UNTRMT|PER_FER|2024-02-01|VIF|1"  ✅
Registro 2: "PA|verif-123|UNTRMT|PER_FER||VIF|1"           ← Referencia vazia!
```

Se `Referencia` estiver vazia em 4 registros, eles são agrupados em uma chave diferente!

---

#### **Causa B: Parse de Nivel Falhando**

Se o `Nivel` não for parseado corretamente, pode criar chaves diferentes:

```csharp
// Parse de Nivel
if (int.TryParse(item["Nivel"].S, out var nivelTemp))
{
    nivelParsed = nivelTemp;
}
else
{
    nivelParsed = 1;  // ← Default
}
```

**Exemplo**:
- 90 registros com `Nivel = "1"` → Agrupados com chave `....|1`
- 4 registros com `Nivel = ""` ou inválido → Agrupados com chave `....|1` (default)

Mas isso **não causaria** diferença, pois ambos teriam Nivel=1.

---

#### **Causa C: Empresa com Espaços ou Caracteres Especiais**

```csharp
Empresa = item.ContainsKey("Empresa") ? item["Empresa"].S : string.Empty
```

Se alguns registros tiverem `Empresa = "PA "` (com espaço) e outros `Empresa = "PA"`:
- Chave 1: `"PA|..."`
- Chave 2: `"PA |..."`  ← Diferente!

---

#### **Causa D: Desnormalização Duplicando Contagem?**

**Improvável**, mas se a empresa for `"PA,MA"` em alguns registros:
- Desnormalização cria 2 registros separados
- Mas ambos teriam QTD = X (não dobra)

---

## Recomendações para Debug

### **1. Adicionar Logs Detalhados**

```csharp
// No loop de busca do GSI
foreach (var item in response.Items)
{
    var resultado = new Resultado { /* ... */ };
    resultados.Add(resultado);
    
    // LOG DETALHADO
    _logger.LogTrace(
        "Resultado adicionado: Empresa={Empresa}, VerifId={VerifId}, Tabela={Tabela}, " +
        "Campo={Campo}, Ref={Ref}, Tipo={Tipo}, Nivel={Nivel}",
        resultado.Empresa, resultado.VerificacaoId, resultado.Tabela,
        resultado.Campo, resultado.Referencia, resultado.TipoApontamento, resultado.Nivel);
}

_logger.LogInformation("Total buscado do GSI: {Total}", resultados.Count);
```

```csharp
// No agrupamento
foreach (var resultado in resultados)
{
    var chave = ChaveAgrupamento.FromResultado(resultado);
    
    // LOG DA CHAVE
    _logger.LogTrace("Chave criada: {ChaveString}", chave.ToKey());
    
    if (!grupos.ContainsKey(chave))
    {
        grupos[chave] = 0;
        _logger.LogDebug("Novo grupo criado: {Chave}", chave.ToKey());
    }
    
    grupos[chave]++;
    totalApontamentos++;
}

_logger.LogInformation(
    "Agrupamento: {TotalResultados} resultados → {TotalGrupos} grupos → {TotalApontamentos} contados",
    resultados.Count, grupos.Count, totalApontamentos);
```

---

### **2. Query Manual no DynamoDB**

Execute no AWS Console ou CLI:

```bash
aws dynamodb query \
  --table-name Resultado \
  --index-name GSI_Agregacao \
  --key-condition-expression "GSI1_PK = :execId" \
  --expression-attribute-values '{":execId":{"S":"EXEC#<execucaoId>"}}' \
  --select COUNT
```

**Verificar**:
- Count retornado = 94? ✅
- Count no log do app = 94? ❓

---

### **3. Filtrar Grupo Específico**

Query manual para o grupo problemático:

```bash
aws dynamodb query \
  --table-name Resultado \
  --index-name GSI_Agregacao \
  --key-condition-expression "GSI1_PK = :execId" \
  --expression-attribute-values '{":execId":{"S":"EXEC#<execucaoId>"}}' \
  --filter-expression "Empresa = :emp AND VerificacaoId = :verif AND Tabela = :tab" \
  --expression-attribute-values '{
    ":execId":{"S":"EXEC#..."},
    ":emp":{"S":"PA"},
    ":verif":{"S":"3c8fad11-24da-45f9-8bb1-464d5aca1385"},
    ":tab":{"S":"UNTRMT"}
  }'
```

**Verificar**:
- Existem exatamente 94 registros?
- Todos têm os mesmos valores para: `Campo`, `Referencia`, `TipoApontamento`, `Nivel`?
- Algum campo está vazio/nulo em 4 deles?

---

### **4. Adicionar Validação de Integridade**

```csharp
// Após agrupamento
var totalContado = grupos.Sum(g => g.Value);
if (totalContado != resultados.Count)
{
    _logger.LogError(
        "DISCREPANCIA DETECTADA! Resultados buscados: {Buscados}, Total contado: {Contado}, Diferenca: {Diff}",
        resultados.Count, totalContado, resultados.Count - totalContado);
    
    // Identificar registros problemáticos
    var todasChaves = resultados.Select(r => ChaveAgrupamento.FromResultado(r).ToKey()).ToList();
    var chavesDuplicadas = todasChaves.GroupBy(k => k).Where(g => g.Count() > 1);
    
    foreach (var grupo in chavesDuplicadas)
    {
        _logger.LogWarning("Chave com {Count} ocorrencias: {Chave}", grupo.Count(), grupo.Key);
    }
}
```

---

## Hipótese Mais Provável

### **Campo `Referencia` ou `Campo` com Valores Diferentes**

Os 4 registros que "faltam" provavelmente têm um valor diferente em algum campo da chave:

**Exemplo Real**:
```
90 registros: Referencia = "2024-02-01T00:00:00.000Z"
4 registros:  Referencia = "2024-02-01"  ← Formato diferente!
```

Isso cria **2 grupos separados**:
- Grupo 1: `EMP#PA#...#2024-02-01T00:00:00.000Z#VIF` → QTD = 90
- Grupo 2: `EMP#PA#...#2024-02-01#VIF` → QTD = 4

---

## Ação Imediata

1. ✅ Adicionar logs detalhados (acima)
2. ✅ Executar novamente a agregação
3. ✅ Comparar:
   - Total buscado do GSI
   - Total contado no agrupamento
   - Total de grupos criados
4. ✅ Se números não batem, verificar campos vazios/nulos nos 4 registros "perdidos"

---

## Status

❌ **Investigação em andamento**


# ✅ Confirmação: Fluxo de Deleção Usando GSI2_PK

## 📋 **Resposta Rápida**

**SIM!** Estamos usando `GSI2_PK = "EMP#[Empresa]"` corretamente no código! 🎯

---

## 🔍 **Fluxo Completo (Passo a Passo)**

### **ETAPA 1: Query no GSI_Empresa**

```csharp
// Linha 414-426 em AgregacaoResultadosStep.cs
var queryRequest = new QueryRequest
{
    TableName = "ResultadoAgregado",
    IndexName = "GSI_Empresa",                          // ← Usa o GSI
    KeyConditionExpression = "GSI2_PK = :pk",          // ← Busca por GSI2_PK
    FilterExpression = "begins_with(PK, :pkGlobal)",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":pk", new AttributeValue { S = $"EMP#{sigla}" } },  // ← "EMP#MA" ✅
        { ":pkGlobal", new AttributeValue { S = "EXEC#" } }
    },
    ProjectionExpression = "PK, SK"                     // ← Retorna apenas PK e SK
};

var response = await _dynamoClient.QueryAsync(queryRequest);
```

**Exemplo de Query**:
```
Query GSI_Empresa WHERE GSI2_PK = "EMP#MA"
```

**Resultado da Query** (exemplo):
```json
[
  {
    "PK": "EXEC#abc-123#EMB#GLOBAL",
    "SK": "EMP#MA#VER#v1#PIP#FAS_CON#123#ERRO"
  },
  {
    "PK": "EXEC#abc-123#EMB#emb-001",
    "SK": "EMP#MA#VER#v1#PIP#FAS_CON#456#ERRO"
  },
  {
    "PK": "EXEC#xyz-789#EMB#GLOBAL",
    "SK": "EMP#MA#VER#v2#CON#NUM#999#CRITICO"
  }
]
```

**✅ Verificação**: Query usa `GSI2_PK = "EMP#MA"`

---

### **ETAPA 2: Filtrar Registros** (GLOBAL vs SEGMENTADO)

#### **Para GLOBAL**:
```csharp
// Linha 445-447
var itensGlobais = response.Items
    .Where(item => item["PK"].S.EndsWith("#EMB#GLOBAL"))  // ← Filtra apenas GLOBAL
    .ToList();
```

**Resultado Filtrado** (exemplo):
```json
[
  {
    "PK": "EXEC#abc-123#EMB#GLOBAL",
    "SK": "EMP#MA#VER#v1#PIP#FAS_CON#123#ERRO"
  },
  {
    "PK": "EXEC#xyz-789#EMB#GLOBAL",
    "SK": "EMP#MA#VER#v2#CON#NUM#999#CRITICO"
  }
]
```

#### **Para SEGMENTADO**:
```csharp
// Linha 512-518
var itensSegmentados = response.Items
    .Where(item => 
    {
        var pk = item["PK"].S;
        return pk.Contains("#EMB#") && !pk.EndsWith("#EMB#GLOBAL");  // ← Filtra SEGMENTADOS
    })
    .ToList();
```

**Resultado Filtrado** (exemplo):
```json
[
  {
    "PK": "EXEC#abc-123#EMB#emb-001",
    "SK": "EMP#MA#VER#v1#PIP#FAS_CON#456#ERRO"
  }
]
```

---

### **ETAPA 3: Deletar Usando PK e SK**

```csharp
// Linha 557-567
var deleteRequests = batch.Select(item => new WriteRequest
{
    DeleteRequest = new DeleteRequest
    {
        Key = new Dictionary<string, AttributeValue>
        {
            { "PK", item["PK"] },    // ← Usa PK retornado do Query
            { "SK", item["SK"] }     // ← Usa SK retornado do Query
        }
    }
}).ToList();

var batchRequest = new BatchWriteItemRequest
{
    RequestItems = new Dictionary<string, List<WriteRequest>>
    {
        { "ResultadoAgregado", deleteRequests }  // ← Delete na tabela principal
    }
};

await _dynamoClient.BatchWriteItemAsync(batchRequest);
```

**Exemplo de BatchWriteItem**:
```json
{
  "ResultadoAgregado": [
    {
      "DeleteRequest": {
        "Key": {
          "PK": "EXEC#abc-123#EMB#GLOBAL",
          "SK": "EMP#MA#VER#v1#PIP#FAS_CON#123#ERRO"
        }
      }
    },
    {
      "DeleteRequest": {
        "Key": {
          "PK": "EXEC#xyz-789#EMB#GLOBAL",
          "SK": "EMP#MA#VER#v2#CON#NUM#999#CRITICO"
        }
      }
    }
  ]
}
```

**✅ Verificação**: Delete usa `PK` e `SK` retornados do Query

---

## 📊 **Resumo Visual**

```
┌─────────────────────────────────────────────────────────────────┐
│ PASSO 1: Query no GSI_Empresa                                  │
├─────────────────────────────────────────────────────────────────┤
│ Query: GSI2_PK = "EMP#MA"                                       │
│        IndexName = "GSI_Empresa"                                │
│                                                                  │
│ Retorna TODOS os registros de MA (qualquer execução):          │
│   - EXEC#abc-123#EMB#GLOBAL                                     │
│   - EXEC#abc-123#EMB#emb-001                                    │
│   - EXEC#xyz-789#EMB#GLOBAL                                     │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ PASSO 2: Filtrar (GLOBAL ou SEGMENTADO)                        │
├─────────────────────────────────────────────────────────────────┤
│ GLOBAL:                                                         │
│   - EXEC#abc-123#EMB#GLOBAL ✅                                  │
│   - EXEC#xyz-789#EMB#GLOBAL ✅                                  │
│                                                                  │
│ SEGMENTADO:                                                     │
│   - EXEC#abc-123#EMB#emb-001 ✅                                 │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ PASSO 3: Deletar usando PK e SK                                │
├─────────────────────────────────────────────────────────────────┤
│ BatchWriteItem:                                                 │
│   DELETE WHERE PK="EXEC#abc-123#EMB#GLOBAL"                    │
│               AND SK="EMP#MA#VER#v1#PIP#FAS_CON#123#ERRO"      │
│                                                                  │
│   DELETE WHERE PK="EXEC#xyz-789#EMB#GLOBAL"                    │
│               AND SK="EMP#MA#VER#v2#CON#NUM#999#CRITICO"       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🎯 **Pontos Importantes**

### **1. GSI2_PK é usado APENAS para buscar**
```
✅ CORRETO: Query no GSI usando GSI2_PK
❌ ERRADO: Tentar deletar usando GSI2_PK (DynamoDB não permite)
```

### **2. Deleção usa PK e SK da tabela principal**
```
✅ CORRETO: Delete usando PK e SK retornados do Query
❌ ERRADO: Delete usando GSI2_PK e GSI2_SK
```

### **3. Formato do GSI2_PK**
```csharp
string gsi2_pk = $"EMP#{sigla}";  // Exemplo: "EMP#MA"
```

**✅ Verificação no código (linha 422)**:
```csharp
{ ":pk", new AttributeValue { S = $"EMP#{sigla}" } }
```

---

## 🔧 **Como Verificar se Está Funcionando**

### **1. Verificar se GSI2_PK está correto nos registros**:
```bash
aws dynamodb scan \
  --table-name ResultadoAgregado \
  --projection-expression "PK, SK, GSI2_PK" \
  --limit 5 \
  --region sa-east-1
```

**Esperado**:
```json
{
  "Items": [
    {
      "PK": "EXEC#abc-123#EMB#GLOBAL",
      "SK": "EMP#MA#VER#v1#...",
      "GSI2_PK": "EMP#MA"  ← ✅ Formato correto!
    }
  ]
}
```

**❌ Se GSI2_PK estiver vazio ou incorreto**:
```json
{
  "Items": [
    {
      "PK": "EXEC#abc-123#EMB#GLOBAL",
      "SK": "EMP#MA#VER#v1#...",
      "GSI2_PK": "MA"  ← ❌ ERRADO! Faltou "EMP#"
    }
  ]
}
```

---

### **2. Testar Query no GSI**:
```bash
aws dynamodb query \
  --table-name ResultadoAgregado \
  --index-name GSI_Empresa \
  --key-condition-expression "GSI2_PK = :pk" \
  --expression-attribute-values '{":pk":{"S":"EMP#MA"}}' \
  --projection-expression "PK, SK, GSI2_PK" \
  --region sa-east-1
```

**Se retornar registros**: ✅ GSI está funcionando!
**Se retornar 0 registros**: ❌ Problema no GSI ou GSI2_PK não preenchido

---

### **3. Verificar logs da aplicação**:

**Log esperado**:
```
[INFO] Iniciando limpeza GLOBAL para empresa MA. GSI2_PK = EMP#MA
[DEBUG] Query GSI_Empresa: Table=ResultadoAgregado, Index=GSI_Empresa, GSI2_PK=EMP#MA
[INFO] Query GSI_Empresa retornou 5 registros para empresa MA
[INFO] Empresa MA (GLOBAL): 5 registros encontrados, 2 filtrados para GLOBAL
[INFO] Iniciando delecao de 2 registros em lotes de 25 (GLOBAL:MA)
[INFO] Batch de 2 registros deletados com sucesso (GLOBAL:MA)
```

**✅ Se vê esses logs**: Código está funcionando!
**❌ Se vê "0 registros"**: GSI2_PK não está preenchido nos registros

---

## 📝 **Conclusão**

**✅ SIM, o código está usando `GSI2_PK = "EMP#[Empresa]"` corretamente!**

**Fluxo**:
1. ✅ Query no GSI usando `GSI2_PK = "EMP#MA"`
2. ✅ Retorna PK e SK dos registros
3. ✅ Delete usando PK e SK

**Se não estiver funcionando**:
- ❌ GSI_Empresa não existe ou não está ACTIVE
- ❌ Registros não têm GSI2_PK preenchido
- ❌ GSI2_PK tem formato incorreto (ex: "MA" em vez de "EMP#MA")

**Próximo passo**: Verificar os logs com as novas informações detalhadas! 🚀

---

**Data**: 11/10/2025
**Status**: ✅ Código correto


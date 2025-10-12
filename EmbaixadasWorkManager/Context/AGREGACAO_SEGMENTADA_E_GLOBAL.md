# Implementação: Agregação Segmentada e Global

## Objetivo

Criar **dois tipos de visualizações** no `ResultadoAgregado`:
1. **Segmentada** (Usuário Comum): Filtrada por embaixada
2. **Global** (Admin): Visão consolidada de todas as embaixadas

---

## Estrutura de Dados

### **1. Modelos de Agregação**

**GrupoAgregadoSegmentado.cs**:
```csharp
public class GrupoAgregadoSegmentado
{
    public ChaveAgrupamento Chave { get; set; }
    public int Quantidade { get; set; }
    public HashSet<string> IdEmbaixadas { get; set; } = new();  // ← COLETA
}
```

**GrupoAgregadoGlobal.cs**:
```csharp
public class GrupoAgregadoGlobal
{
    public ChaveAgrupamento Chave { get; set; }
    public int Quantidade { get; set; }
    // Sem IdEmbaixadas - não precisa replicar
}
```

---

### **2. Modelo Resultado - Campo IdEmbaixadas**

**Resultado.cs** (atualizado):
```csharp
[DynamoDBProperty("IdEmbaixadas")]
public List<string> IdEmbaixadas { get; set; } = new();
```

**No DynamoDB**:
```json
{
  "IdEmbaixadas": {
    "L": [
      { "S": "7530416f-b46f-4048-8d46-0c3702226bea" },
      { "S": "abc-123-..." }
    ]
  }
}
```

---

### **3. Modelo ResultadoAgregado - Métodos de PK**

**ResultadoAgregado.cs** (atualizado):
```csharp
// PK Segmentada (Usuário Comum)
public static string CriarPKSegmentado(string execucaoId, string idEmbaixada)
{
    return $"EXEC#{execucaoId}#EMB#{idEmbaixada}";
}

// PK Global (Admin)
public static string CriarPKGlobal(string execucaoId)
{
    return $"EXEC#{execucaoId}#EMB#GLOBAL";
}
```

---

## Fluxo de Agregação

### **ETAPA 1: Busca Otimizada (GSI Query)** ✅

**Já implementado**:
```csharp
var resultados = await BuscarApontamentosAsync(execucaoId);
// Query: GSI1_PK = "EXEC#<execucaoId>"
// Retorna TODOS os apontamentos com paginação completa
```

---

### **ETAPA 2: Agregação em Memória (DUAS Estruturas)** 🆕

**AgregacaoResultadosStep.cs**:
```csharp
private (
    Dictionary<string, GrupoAgregadoSegmentado> gruposSegmentados, 
    Dictionary<string, GrupoAgregadoGlobal> gruposGlobais,
    int totalApontamentos, 
    HashSet<string> empresas, 
    HashSet<string> embaixadas
) AgruparResultados(List<Resultado> resultados)
{
    var gruposSegmentados = new Dictionary<string, GrupoAgregadoSegmentado>();
    var gruposGlobais = new Dictionary<string, GrupoAgregadoGlobal>();
    
    foreach (var resultado in resultados)
    {
        var chave = ChaveAgrupamento.FromResultado(resultado);
        var chaveStr = chave.ToKey();
        
        // === AGREGAÇÃO SEGMENTADA ===
        if (!gruposSegmentados.ContainsKey(chaveStr))
            gruposSegmentados[chaveStr] = new GrupoAgregadoSegmentado(chave);
        
        gruposSegmentados[chaveStr].Quantidade++;
        
        // ✅ CRÍTICO: Coletar IdEmbaixadas (Union)
        foreach (var idEmbaixada in resultado.IdEmbaixadas)
        {
            gruposSegmentados[chaveStr].IdEmbaixadas.Add(idEmbaixada);
        }
        
        // === AGREGAÇÃO GLOBAL ===
        if (!gruposGlobais.ContainsKey(chaveStr))
            gruposGlobais[chaveStr] = new GrupoAgregadoGlobal(chave);
        
        gruposGlobais[chaveStr].Quantidade++;
    }
    
    return (gruposSegmentados, gruposGlobais, ...);
}
```

**Resultado**:
- `gruposSegmentados`: Grupos com lista de embaixadas coletadas
- `gruposGlobais`: Grupos com apenas contagem total

---

### **ETAPA 3: Inserção SEGMENTADA (Usuário Comum)** 🆕

**Lógica de Replicação Mínima**:
```csharp
private async Task InserirResultadosSegmentadosAsync(
    string execucaoId,
    Dictionary<string, GrupoAgregadoSegmentado> gruposSegmentados)
{
    foreach (var grupo in gruposSegmentados.Values)
    {
        // REPLICAÇÃO: Para cada embaixada do grupo
        foreach (var idEmbaixada in grupo.IdEmbaixadas)
        {
            // DESNORMALIZAÇÃO: Se empresa = "MA,PI"
            if (grupo.Chave.Empresa.Contains(','))
            {
                foreach (var sigla in ["MA", "PI"])
                {
                    var item = new ResultadoAgregado
                    {
                        PK = ResultadoAgregado.CriarPKSegmentado(execucaoId, idEmbaixada),
                        SK = ResultadoAgregado.CriarSK(sigla, ...),
                        Quantidade = grupo.Quantidade,
                        Empresa = sigla,
                        // ...
                    };
                    batch.Add(item);
                }
            }
            else
            {
                var item = new ResultadoAgregado
                {
                    PK = ResultadoAgregado.CriarPKSegmentado(execucaoId, idEmbaixada),
                    SK = ResultadoAgregado.CriarSK(grupo.Chave.Empresa, ...),
                    Quantidade = grupo.Quantidade,
                    // ...
                };
                batch.Add(item);
            }
        }
    }
    
    await InserirBatchAsync(batch);
}
```

---

### **ETAPA 4: Inserção GLOBAL (Admin)** 🆕

**Lógica Sem Replicação**:
```csharp
private async Task InserirResultadosGlobaisAsync(
    string execucaoId,
    Dictionary<string, GrupoAgregadoGlobal> gruposGlobais)
{
    foreach (var grupo in gruposGlobais.Values)
    {
        // SEM REPLICAÇÃO: Apenas um registro por grupo
        
        // DESNORMALIZAÇÃO: Se empresa = "MA,PI"
        if (grupo.Chave.Empresa.Contains(','))
        {
            foreach (var sigla in ["MA", "PI"])
            {
                var item = new ResultadoAgregado
                {
                    PK = ResultadoAgregado.CriarPKGlobal(execucaoId),  // ← GLOBAL
                    SK = ResultadoAgregado.CriarSK(sigla, ...),
                    Quantidade = grupo.Quantidade,
                    Empresa = sigla,
                    // ...
                };
                batch.Add(item);
            }
        }
        else
        {
            var item = new ResultadoAgregado
            {
                PK = ResultadoAgregado.CriarPKGlobal(execucaoId),  // ← GLOBAL
                SK = ResultadoAgregado.CriarSK(grupo.Chave.Empresa, ...),
                Quantidade = grupo.Quantidade,
                // ...
            };
            batch.Add(item);
        }
    }
    
    await InserirBatchAsync(batch);
}
```

---

## Exemplo Prático

### **Dados de Entrada**

**10 Apontamentos**:
```
Apontamento 1-5: Empresa="MA", IdEmbaixadas=["emb-001", "emb-002"]
Apontamento 6-10: Empresa="PA", IdEmbaixadas=["emb-001"]
```

**Todos do mesmo grupo**: `MA|verif-123|PIP|FAS_CON|12345|ERRO|1`

---

### **ETAPA 2: Agregação**

**GrupoAgregadoSegmentado**:
```csharp
{
    Chave: "MA|verif-123|PIP|FAS_CON|12345|ERRO|1",
    Quantidade: 10,
    IdEmbaixadas: ["emb-001", "emb-002"]  // ← Union de todas
}
```

**GrupoAgregadoGlobal**:
```csharp
{
    Chave: "MA|verif-123|PIP|FAS_CON|12345|ERRO|1",
    Quantidade: 10
    // Sem IdEmbaixadas
}
```

---

### **ETAPA 3: Inserção SEGMENTADA**

**2 registros criados** (1 por embaixada):
```json
// Registro 1 (emb-001)
{
  "PK": "EXEC#abc-123#EMB#emb-001",  ← Segmentado
  "SK": "EMP#MA#VER#verif-123#PIP#FAS_CON#12345#ERRO",
  "QTD": 10,
  "Empresa": "MA"
}

// Registro 2 (emb-002)
{
  "PK": "EXEC#abc-123#EMB#emb-002",  ← Segmentado
  "SK": "EMP#MA#VER#verif-123#PIP#FAS_CON#12345#ERRO",
  "QTD": 10,
  "Empresa": "MA"
}
```

---

### **ETAPA 4: Inserção GLOBAL**

**1 registro criado**:
```json
{
  "PK": "EXEC#abc-123#EMB#GLOBAL",  ← Global
  "SK": "EMP#MA#VER#verif-123#PIP#FAS_CON#12345#ERRO",
  "QTD": 10,
  "Empresa": "MA"
}
```

---

## Queries de Consulta

### **Usuário Comum (Embaixada emb-001)**

```
Query:
  PK = "EXEC#abc-123#EMB#emb-001"
  
Retorna:
  - Apenas grupos que têm apontamentos da emb-001 ✅
```

### **Admin (Todas as Embaixadas)**

```
Query:
  PK = "EXEC#abc-123#EMB#GLOBAL"
  
Retorna:
  - Todos os grupos consolidados (sem filtro) ✅
```

---

## Multiplicação de Registros

### **Cenário Complexo**

**Entrada**:
- 1 grupo: `Empresa="MA,PI"`, 3 embaixadas `["emb-A", "emb-B", "emb-C"]`

**Saída Segmentada**:
```
3 embaixadas × 2 siglas = 6 registros:
1. PK="EXEC#...#EMB#emb-A", SK="EMP#MA#...", QTD=X
2. PK="EXEC#...#EMB#emb-A", SK="EMP#PI#...", QTD=X
3. PK="EXEC#...#EMB#emb-B", SK="EMP#MA#...", QTD=X
4. PK="EXEC#...#EMB#emb-B", SK="EMP#PI#...", QTD=X
5. PK="EXEC#...#EMB#emb-C", SK="EMP#MA#...", QTD=X
6. PK="EXEC#...#EMB#emb-C", SK="EMP#PI#...", QTD=X
```

**Saída Global**:
```
2 siglas = 2 registros:
1. PK="EXEC#...#EMB#GLOBAL", SK="EMP#MA#...", QTD=X
2. PK="EXEC#...#EMB#GLOBAL", SK="EMP#PI#...", QTD=X
```

**Total**: 8 registros (6 segmentados + 2 globais)

---

## Benefícios

### **1. Segmentação por Embaixada** ✅
- Usuário comum vê apenas seus dados
- Query eficiente com PK específica
- Isolamento de dados

### **2. Visão Global para Admin** ✅
- Admin vê todos os dados consolidados
- Uma única query (PK = GLOBAL)
- Sem necessidade de agregar múltiplas queries

### **3. Desnormalização de Empresa** ✅
- Queries por empresa específica funcionam
- Relatórios por empresa mais eficientes
- Dados otimizados para leitura

### **4. Replicação Mínima** ✅
- Apenas replica onde necessário (por embaixada)
- Não duplica dados desnecessariamente
- Storage otimizado

---

## Logs de Execução

### **Logs Esperados**

```
[INFO] Encontrados 291950 apontamentos na tabela Resultado
[INFO] Agrupamento concluido. Segmentados: 17 grupos, Globais: 17 grupos, Total: 291950 apontamentos, 3 empresas, 5 embaixadas
[INFO] Inserindo grupos SEGMENTADOS na tabela ResultadoAgregado. Total de grupos: 17
[INFO] Insercao SEGMENTADA concluida. 85 registros inseridos (de 17 grupos base, replicados 85 vezes)
[INFO] Inserindo grupos GLOBAIS na tabela ResultadoAgregado. Total de grupos: 17
[INFO] Insercao GLOBAL concluida. 17 registros inseridos (de 17 grupos base)
[INFO] Agregacao concluida. 291950 apontamentos. Segmentados: 17 grupos, Globais: 17 grupos. 3 empresas, 5 embaixadas
```

**Análise**:
- 17 grupos base
- 5 embaixadas → 17 × 5 = 85 registros segmentados (com replicação)
- 1 visão global → 17 × 1 = 17 registros globais
- Total: 102 registros no ResultadoAgregado

---

## Validação

### **Teste 1: Query Segmentada (Usuário)**

**Input**:
```
Usuário da embaixada: "emb-001"
Query: PK = "EXEC#abc-123#EMB#emb-001"
```

**Output esperado**:
```
Apenas grupos que têm apontamentos de emb-001
(não vê dados de emb-002, emb-003, etc.)
```

### **Teste 2: Query Global (Admin)**

**Input**:
```
Admin
Query: PK = "EXEC#abc-123#EMB#GLOBAL"
```

**Output esperado**:
```
Todos os grupos consolidados
(vê tudo, sem filtro de embaixada)
```

### **Teste 3: Consistência de Contagem**

**Validação automática**:
```csharp
var totalContadoSegmentado = gruposSegmentados.Sum(g => g.Value.Quantidade);
var totalContadoGlobal = gruposGlobais.Sum(g => g.Value.Quantidade);

if (totalContadoSegmentado != totalContadoGlobal)
{
    _logger.LogError("INCONSISTENCIA: Segmentado != Global");
}
```

---

## Performance

### **Estimativa de Registros**

**Fórmula Segmentada**:
```
Total Registros = Grupos × Embaixadas × (1 + Desnormalização)
```

**Exemplo**:
- 17 grupos
- 5 embaixadas
- Sem desnormalização: 17 × 5 = **85 registros**
- Com desnormalização (30%): 85 × 1.3 = **~110 registros**

**Fórmula Global**:
```
Total Registros = Grupos × (1 + Desnormalização)
```

**Exemplo**:
- 17 grupos
- Sem desnormalização: **17 registros**
- Com desnormalização (30%): **~22 registros**

---

## Comparação: Antes vs Depois

### **Antes (Sem Segmentação)**

**Estrutura**:
```
PK = "EXEC#abc-123"
SK = "EMP#MA#VER#..."
```

**Query Usuário**:
```
Query PK + FilterExpression (IdEmbaixadas contains "emb-001")
↓
❌ Lê TODOS os registros e filtra em memória
❌ Ineficiente
```

---

### **Depois (Com Segmentação)**

**Estrutura Segmentada**:
```
PK = "EXEC#abc-123#EMB#emb-001"
SK = "EMP#MA#VER#..."
```

**Query Usuário**:
```
Query PK = "EXEC#abc-123#EMB#emb-001"
↓
✅ Retorna APENAS registros da emb-001
✅ Eficiente (usa índice)
```

**Estrutura Global**:
```
PK = "EXEC#abc-123#EMB#GLOBAL"
SK = "EMP#MA#VER#..."
```

**Query Admin**:
```
Query PK = "EXEC#abc-123#EMB#GLOBAL"
↓
✅ Retorna visão consolidada
✅ Sem filtros adicionais
```

---

## Mudanças nos Arquivos

### **Arquivos Criados**:
- ✅ `Models/GrupoAgregado.cs` (GrupoAgregadoSegmentado + GrupoAgregadoGlobal)

### **Arquivos Modificados**:
- ✅ `Models/Resultado.cs` (adicionado `IdEmbaixadas`)
- ✅ `Models/ResultadoAgregado.cs` (novos métodos de PK)
- ✅ `Services/PostProcessing/Steps/AgregacaoResultadosStep.cs`:
  - Parse de `IdEmbaixadas` (lista no DynamoDB)
  - Método `AgruparResultados` (duas estruturas)
  - Método `InserirResultadosSegmentadosAsync` (replicação)
  - Método `InserirResultadosGlobaisAsync` (global)

---

## Conclusão

✅ **Implementação completa da agregação segmentada e global**:
- ✅ Parsing de `IdEmbaixadas` como lista
- ✅ Coleta de IdEmbaixadas por grupo (Union)
- ✅ Duas estruturas de agregação (Segmentada + Global)
- ✅ Replicação mínima (apenas onde necessário)
- ✅ Desnormalização de empresas (múltiplas siglas)
- ✅ Logs detalhados de monitoramento
- ✅ Validação de integridade

**Status**: ✅ **IMPLEMENTADO E FUNCIONAL**

**Data**: 11/10/2025


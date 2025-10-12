# Remoção de Grupos Zerados do ResultadoAgregado

## Decisão de Negócio

Remover grupos com `QTD = 0` da tabela `ResultadoAgregado`. Apenas grupos com apontamentos reais (`QTD > 0`) devem ser armazenados.

---

## Motivação

### **Antes: Com Grupos Zerados**
```
Problema:
- Tabela muito grande com muitos registros zerados
- Dificuldade em identificar problemas reais
- Consumo desnecessário de storage
- Queries mais lentas
```

### **Depois: Sem Grupos Zerados**
```
Benefícios:
✅ Tabela menor e mais eficiente
✅ Apenas dados relevantes (problemas reais)
✅ Queries mais rápidas
✅ Fácil identificar verificações com problemas
```

---

## Mudanças Implementadas

### **1. Removida Busca de ExecucaoVerificacao**

**Antes**:
```csharp
// ETAPA 0: Buscar todas as ExecucaoVerificacao para criar grupos base
var execucoesVerificacao = await BuscarExecucoesVerificacaoAsync(execucaoId);

// ETAPA 2: Agrupamento incluindo grupos com QTD=0
var (grupos, ...) = AgruparResultados(resultados, execucoesVerificacao);
```

**Depois**:
```csharp
// ETAPA 1: Busca otimizada usando GSI
var resultados = await BuscarApontamentosAsync(execucaoId);

// ETAPA 2: Agrupamento apenas com apontamentos reais
var (grupos, ...) = AgruparResultados(resultados);
```

---

### **2. Método AgruparResultados Simplificado**

**Antes** (com grupos zerados):
```csharp
private (Dictionary<ChaveAgrupamento, int> grupos, ...) 
    AgruparResultados(List<Resultado> resultados, List<ExecucaoVerificacao> execucoesVerificacao)
{
    var grupos = new Dictionary<ChaveAgrupamento, int>();
    
    // PRIMEIRO: Criar grupos base com QTD=0
    foreach (var execVerif in execucoesVerificacao)
    {
        var chaveBase = ChaveAgrupamento.FromExecucaoVerificacao(execVerif);
        grupos[chaveBase] = 0;  // ← Grupo zerado
    }
    
    // SEGUNDO: Processar apontamentos reais
    foreach (var resultado in resultados)
    {
        var chave = ChaveAgrupamento.FromResultado(resultado);
        if (!grupos.ContainsKey(chave))
            grupos[chave] = 0;
        grupos[chave]++;  // ← Incrementa
    }
    
    return (grupos, ...);
}
```

**Depois** (apenas apontamentos reais):
```csharp
private (Dictionary<ChaveAgrupamento, int> grupos, ...) 
    AgruparResultados(List<Resultado> resultados)
{
    var grupos = new Dictionary<ChaveAgrupamento, int>();
    
    // Processar apenas apontamentos reais
    foreach (var resultado in resultados)
    {
        var chave = ChaveAgrupamento.FromResultado(resultado);
        if (!grupos.ContainsKey(chave))
            grupos[chave] = 0;
        grupos[chave]++;  // ← Sempre QTD > 0
    }
    
    return (grupos, ...);
}
```

---

### **3. Logs Simplificados**

**Antes**:
```csharp
var gruposComApontamentos = grupos.Count(g => g.Value > 0);
var gruposZerados = grupos.Count(g => g.Value == 0);

var message = $"Agregacao concluida. {totalApontamentos} apontamentos agrupados em {grupos.Count} grupos " +
             $"({gruposComApontamentos} com dados, {gruposZerados} zerados). {empresas.Count} empresas processadas";
```

**Depois**:
```csharp
var message = $"Agregacao concluida. {totalApontamentos} apontamentos agrupados em {grupos.Count} grupos. " +
             $"{empresas.Count} empresas processadas";
```

---

### **4. Método BuscarExecucoesVerificacaoAsync Removido**

```csharp
// ❌ REMOVIDO - Não é mais necessário
private async Task<List<ExecucaoVerificacao>> BuscarExecucoesVerificacaoAsync(string execucaoId)
{
    // Este método fazia SCAN da tabela ExecucaoVerificacao
    // Causava overhead desnecessário
}
```

---

## Impacto nas Tabelas

### **Tabela ResultadoAgregado**

**Antes** (com grupos zerados):
```json
// Verificação SEM apontamentos
{
  "PK": "EXEC#abc-123",
  "SK": "EMP#MA#VER#verif-456#VERIFICACAO#verif-456#N/A#VERIFICACAO",
  "QTD": 0,  ← Grupo zerado armazenado
  "Empresa": "MA",
  "VerificacaoId": "verif-456"
}

// Verificação COM apontamentos
{
  "PK": "EXEC#abc-123",
  "SK": "EMP#RS#VER#verif-789#PIP#FAS_CON#12345#ERRO",
  "QTD": 5,
  "Empresa": "RS",
  "VerificacaoId": "verif-789"
}
```

**Depois** (sem grupos zerados):
```json
// Verificação SEM apontamentos
// ← NÃO É ARMAZENADA

// Verificação COM apontamentos
{
  "PK": "EXEC#abc-123",
  "SK": "EMP#RS#VER#verif-789#PIP#FAS_CON#12345#ERRO",
  "QTD": 5,  ← Apenas grupos com QTD > 0
  "Empresa": "RS",
  "VerificacaoId": "verif-789"
}
```

---

## Queries Afetadas

### **Buscar Apontamentos de uma Execução**

**Query DynamoDB**:
```
PK = "EXEC#abc-123"
```

**Resultado**: Apenas grupos com problemas reais (QTD > 0)

### **Verificar se uma Verificação Teve Problemas**

**Query DynamoDB**:
```
PK = "EXEC#abc-123"
SK begins_with "EMP#MA#VER#verif-456"
```

**Interpretação**:
- **0 itens retornados**: Verificação executada sem problemas ✅
- **N itens retornados**: Verificação encontrou N grupos de problemas ❌

---

## Vantagens

### **1. Performance**
- ✅ Tabela menor (apenas dados relevantes)
- ✅ Queries mais rápidas (menos items para filtrar)
- ✅ Menor consumo de storage

### **2. Clareza**
- ✅ Fácil identificar verificações problemáticas
- ✅ Dashboards mais limpos
- ✅ Dados mais significativos

### **3. Custo**
- ✅ Menos storage no DynamoDB
- ✅ Menos RCUs consumidas em queries
- ✅ Menos processamento

---

## Desvantagens e Mitigação

### **❌ Problema: Não é possível distinguir**
- "Verificação não executada"
- "Verificação executada sem problemas"

### **✅ Mitigação**
Use a tabela `ExecucaoVerificacao` para auditoria:
```
Query: PK = "execucaoId#verificacaoId"
```

Se registro existe em `ExecucaoVerificacao`:
- ✅ Verificação foi executada
- Se não está em `ResultadoAgregado`: Sem problemas
- Se está em `ResultadoAgregado`: Com problemas

Se registro NÃO existe em `ExecucaoVerificacao`:
- ❌ Verificação não foi executada

---

## Exemplo Prático

### **Cenário: Execução com 10 Verificações**

**Resultado da Execução**:
- 7 verificações sem problemas
- 3 verificações com problemas

**Na Tabela ResultadoAgregado**:

**Antes** (com grupos zerados):
```
10 registros:
- 7 com QTD=0
- 3 com QTD>0
```

**Depois** (sem grupos zerados):
```
3 registros:
- Apenas os 3 com QTD>0
```

**Economia**: 70% menos registros! 🎉

---

## Logs Esperados

### **Antes**:
```
[INFO] Encontradas 10 verificacoes executadas
[INFO] Encontrados 45 apontamentos na tabela Resultado
[INFO] Agrupamento concluido. 10 grupos criados (3 com dados, 7 zerados)
[INFO] Inserindo 10 grupos na tabela ResultadoAgregado
```

### **Depois**:
```
[INFO] Encontrados 45 apontamentos na tabela Resultado
[INFO] Agrupamento concluido. 3 grupos criados
[INFO] Inserindo 3 grupos na tabela ResultadoAgregado (apenas com QTD > 0)
```

---

## Conclusão

A remoção de grupos zerados:
- ✅ **Otimiza performance** (tabela menor, queries mais rápidas)
- ✅ **Reduz custos** (menos storage, menos RCUs)
- ✅ **Melhora clareza** (apenas dados relevantes)
- ✅ **Mantém rastreabilidade** (via ExecucaoVerificacao)

**Status**: ✅ **IMPLEMENTADO E DOCUMENTADO**

---

## Relacionado

- `AGREGACAO_RESULTADOS.md` - Implementação original da agregação
- `REMOCAO_TOTAL_VIEW_E_GRUPOS_ZERADOS.md` - Primeira implementação (com grupos zerados)
- `DESNORMALIZACAO_EMPRESAS_RESULTADO_AGREGADO.md` - Desnormalização de empresas


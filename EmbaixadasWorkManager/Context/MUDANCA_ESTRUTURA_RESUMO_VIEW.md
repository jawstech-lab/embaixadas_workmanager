# Mudança na Estrutura do ExecucaoResumoView

## Visão Geral

Mudança na estrutura de chaves da tabela `ExecucaoResumoView` para suportar última execução **por embaixada E por empresa**, ao invés de apenas por empresa globalmente.

## Mudança Implementada

### **ANTES (Estrutura Antiga)**
```
PK: VIEW#LAST_EXEC (fixo para todas as empresas)
SK: EMP#<Sigla>

Exemplo:
PK_VIEW = "VIEW#LAST_EXEC"
SK_VIEW = "EMP#MA"

Problema: Apenas UMA última execução por empresa (global)
```

### **DEPOIS (Nova Estrutura)**
```
PK: VIEW#LAST_EXEC#EMB#<IdEmbaixada>
SK: EMP#<Sigla>

Exemplo:
PK_VIEW = "VIEW#LAST_EXEC#EMB#7530416f-b46f-4048-8d46-0c3702226bea"
SK_VIEW = "EMP#MA"

Benefício: Última execução por embaixada E por empresa
```

## Impacto da Mudança

### **Dados Armazenados**

#### **Antes**
```
Execução com:
- IdEmbaixadas: [emb1, emb2, emb3]
- Empresas: MA, RS

Registros criados: 2
- PK: VIEW#LAST_EXEC, SK: EMP#MA
- PK: VIEW#LAST_EXEC, SK: EMP#RS
```

#### **Depois**
```
Execução com:
- IdEmbaixadas: [emb1, emb2, emb3]
- Empresas: MA, RS

Registros criados: 6 (3 embaixadas × 2 empresas)
- PK: VIEW#LAST_EXEC#EMB#emb1, SK: EMP#MA
- PK: VIEW#LAST_EXEC#EMB#emb1, SK: EMP#RS
- PK: VIEW#LAST_EXEC#EMB#emb2, SK: EMP#MA
- PK: VIEW#LAST_EXEC#EMB#emb2, SK: EMP#RS
- PK: VIEW#LAST_EXEC#EMB#emb3, SK: EMP#MA
- PK: VIEW#LAST_EXEC#EMB#emb3, SK: EMP#RS
```

## Benefícios

### **1. Granularidade**
- Controle de última execução por embaixada
- Cada embaixada tem seu próprio conjunto de últimas execuções

### **2. Isolamento**
- Embaixadas independentes
- Atualizações em uma não afetam outras

### **3. Consultas Específicas**
```csharp
// Última execução de MA na embaixada específica
PK = "VIEW#LAST_EXEC#EMB#emb1"
SK = "EMP#MA"

// Todas as últimas execuções de uma embaixada
PK = "VIEW#LAST_EXEC#EMB#emb1"
```

## Implementação

### **Modelo Atualizado**

```csharp
[DynamoDBTable("ExecucaoResumoView")]
public class ExecucaoResumoView
{
    [DynamoDBHashKey("PK_VIEW")]
    public string PK { get; set; } = string.Empty;  // VIEW#LAST_EXEC#EMB#<IdEmbaixada>

    [DynamoDBRangeKey("SK_VIEW")]
    public string SK { get; set; } = string.Empty;  // EMP#<Sigla>

    [DynamoDBProperty("IdEmbaixada")]
    public string IdEmbaixada { get; set; } = string.Empty;  // NOVO CAMPO

    // Métodos helper
    public static string CriarPK(string idEmbaixada) => $"VIEW#LAST_EXEC#EMB#{idEmbaixada}";
    public static string CriarSK(string siglaEmpresa) => $"EMP#{siglaEmpresa.ToUpper()}";
}
```

### **Modelo Resultado Atualizado**

```csharp
[DynamoDBTable("Resultado")]
public class Resultado
{
    [DynamoDBProperty("IdEmbaixada")]
    public string IdEmbaixada { get; set; } = string.Empty;  // NOVO CAMPO
    
    // ... outros campos
}
```

## Service Atualizado

### **ExecucaoEmpresaService**

```csharp
// Método renomeado e atualizado
public async Task GravarExecucaoPorEmbaixadasEEmpresasAsync(
    string execucaoId,
    List<string> idEmbaixadas,      // NOVO PARÂMETRO
    string empresasString,
    DateTime dataSolicitacao,
    string status)
{
    var siglas = ExtrairSiglasEmpresas(empresasString);
    
    // NOVO: Loop duplo (embaixada × empresa)
    foreach (var idEmbaixada in idEmbaixadas)
    {
        foreach (var sigla in siglas)
        {
            // Grava em ExecucaoEmpresaStatus
            await GravarExecucaoEmpresaStatusAsync(...);
            
            // Grava em ExecucaoResumoView com NOVA estrutura
            await AtualizarExecucaoResumoViewAsync(
                execucaoId, 
                idEmbaixada,  // NOVO
                sigla, 
                dataSolicitacao, 
                status);
        }
    }
}

// Método atualizado
private async Task<bool> AtualizarExecucaoResumoViewAsync(
    string execucaoId,
    string idEmbaixada,  // NOVO PARÂMETRO
    string sigla,
    DateTime dataSolicitacao,
    string status)
{
    var pk = ExecucaoResumoView.CriarPK(idEmbaixada);  // VIEW#LAST_EXEC#EMB#...
    var sk = ExecucaoResumoView.CriarSK(sigla);        // EMP#MA
    
    // PutItem com nova estrutura...
}
```

### **ProcessorService**

```csharp
// Chamada atualizada
await _execucaoEmpresaService.GravarExecucaoPorEmbaixadasEEmpresasAsync(
    execucao.Id,
    execucao.IdEmbaixadas,  // NOVO - passa lista de embaixadas
    execucao.Empresa,
    execucao.DataSolicitacao,
    execucao.Status);
```

### **AgregacaoResultadosStep**

```csharp
// ETAPA 2: Agrupamento rastreia embaixadas
var (grupos, totalApontamentos, empresas, embaixadas) = AgruparResultados(resultados);

// ETAPA 4: Atualização considerando embaixadas
private async Task AtualizarResumoViewAsync(
    string execucaoId,
    HashSet<string> embaixadas,  // NOVO
    HashSet<string> empresas,
    int totalApontamentos)
{
    // Loop duplo: embaixada × empresa
    foreach (var idEmbaixada in embaixadas)
    {
        foreach (var empresa in empresas)
        {
            var pk = ExecucaoResumoView.CriarPK(idEmbaixada);
            var sk = ExecucaoResumoView.CriarSK(empresa);
            
            // UpdateItem com nova estrutura...
        }
    }
}
```

## Consultas

### **Última Execução de Empresa em Embaixada Específica**
```csharp
// DynamoDB GetItem
PK = "VIEW#LAST_EXEC#EMB#7530416f-b46f-4048-8d46-0c3702226bea"
SK = "EMP#MA"

// Retorna: Última execução de MA na embaixada específica
```

### **Todas as Últimas Execuções de uma Embaixada**
```csharp
// DynamoDB Query
PK = "VIEW#LAST_EXEC#EMB#7530416f-b46f-4048-8d46-0c3702226bea"

// Retorna: Última execução de cada empresa nesta embaixada
```

### **Comparação: Últimas Execuções de Múltiplas Embaixadas**
```csharp
// Para cada embaixada, fazer Query
foreach (var idEmb in embaixadas)
{
    PK = $"VIEW#LAST_EXEC#EMB#{idEmb}"
    // Retorna últimas execuções desta embaixada
}
```

## Volume de Dados

### **Exemplo Real**

```
Cenário:
- 50 embaixadas diferentes
- 10 empresas (MA, RS, SP, PR, SC, BA, MG, RJ, ES, GO)
- 1000 execuções/mês

Registros na ExecucaoResumoView:
- Máximo: 50 embaixadas × 10 empresas = 500 registros
- Cada execução atualiza registros existentes (não cria novos)

Storage:
- 500 registros × ~1KB = ~500KB
- Custo: Praticamente zero

Writes por execução:
- 50 embaixadas × 10 empresas = 500 PutItems
- Com condição: Muitos serão rejeitados (data não mais recente)
- Writes efetivos: ~100-200 por execução
```

## Comparação: Antes vs Depois

| Aspecto | Antes | Depois |
|---------|-------|--------|
| Granularidade | Por empresa (global) | Por embaixada + empresa |
| Registros | 10 (empresas) | 500 (50 emb × 10 emp) |
| Consulta | Última execução global | Última por embaixada |
| Writes/exec | 10 | 100-200 (com filtro) |
| Storage | ~10KB | ~500KB |
| Performance | O(1) | O(1) (mesma) |
| Custo | ~$0.01/mês | ~$0.30/mês |

## Migração de Dados Existentes

### **Atenção**

Se já existem dados na tabela `ExecucaoResumoView` com estrutura antiga:

1. **Opção 1**: Recriar tabela (perda de dados históricos)
2. **Opção 2**: Migração manual dos dados

### **Script de Migração (Conceitual)**

```powershell
# 1. Backup da tabela antiga
aws dynamodb create-backup --table-name ExecucaoResumoView

# 2. Deletar tabela antiga
aws dynamodb delete-table --table-name ExecucaoResumoView

# 3. Criar nova tabela
.\create-table-execucao-resumo-view.ps1

# 4. Migrar dados (se necessário - implementar script)
```

## Logs Gerados

### **Início (ExecucaoEmpresaService)**
```
[INFO] Iniciando gravacao de execucao por embaixadas e empresas
       ExecucaoId: abc-123, Embaixadas: 3, Empresas: MA,RS
[INFO] Encontradas 3 embaixadas e 2 empresas para processar
       Total de combinacoes: 6
[DEBUG] Processando embaixada emb1, empresa: MA
[DEBUG] Resumo atualizado condicionalmente: Embaixada=emb1, Empresa=MA
       PK=VIEW#LAST_EXEC#EMB#emb1, SK=EMP#MA
[INFO] Gravacao concluida. Embaixadas: 3, Empresas: 2, Combinacoes: 6
       Status: 6 sucesso, 0 falhas. Resumo: 6 atualizado, 0 nao atualizado
```

### **Final (AgregacaoResultadosStep)**
```
[INFO] Atualizando ExecucaoResumoView para 3 embaixadas x 2 empresas
       Total de combinacoes: 6. Apontamentos: 150
[DEBUG] TotalApontamentos atualizado: Embaixada=emb1, Empresa=MA, Total=150
[INFO] Atualizacao da ExecucaoResumoView concluida. Sucessos: 6, Falhas: 0
```

## Conclusão

A nova estrutura oferece:

- ✅ **Maior granularidade**: Última execução por embaixada
- ✅ **Isolamento**: Embaixadas independentes
- ✅ **Flexibilidade**: Consultas específicas por embaixada
- ✅ **Performance**: Mesma velocidade O(1)
- ✅ **Escalabilidade**: Suporta múltiplas embaixadas

**Custo adicional**: Mínimo (~$0.30/mês vs $0.01/mês)  
**Benefício**: Muito maior (controle granular por embaixada)

**Status**: ✅ **IMPLEMENTAÇÃO COMPLETA** - Build compilado sem erros!



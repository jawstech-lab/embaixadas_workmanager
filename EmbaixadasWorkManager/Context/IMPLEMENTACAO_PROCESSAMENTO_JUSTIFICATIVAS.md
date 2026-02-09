# Implementação do Processamento de Justificativas

## Visão Geral

Sistema de processamento de justificativas aprovadas que remove apontamentos da tabela `Resultado` antes da agregação. Implementado como um **Step de Pós-Processamento** que executa automaticamente após a finalização de uma execução.

## Problema Resolvido

### **Antes**
- Apontamentos justificados e aprovados permaneciam na tabela `Resultado`
- Apontamentos justificados eram incluídos na agregação, inflando os números
- Não havia forma automatizada de processar justificativas
- Usuários precisavam justificar apontamentos manualmente

### **Depois**
- Justificativas aprovadas são processadas automaticamente
- Apontamentos justificados são removidos da tabela `Resultado` ANTES da agregação
- Sistema busca justificativas aprovadas usando GSI otimizado
- Suporte para dois modos: "Remover Todos" ou "Remover IDs Específicos"
- Logs detalhados de cada remoção

## Arquitetura

### **Componentes**

```
ProcessamentoJustificativasStep
  ├─ Busca justificativas aprovadas (GSI_Status)
  ├─ Processa cada justificativa
  │   ├─ Modo "Selecionar Todos" (SelecionarTodos = 1)
  │   │   └─ Remove todos os registros que atendem critérios
  │   └─ Modo "IDs Específicos" (SelecionarTodos = 0)
  │       └─ Remove apenas IDs da lista
  ├─ Deleta registros em lote (BatchWriteItem)
  └─ Aguarda propagação no GSI antes da próxima etapa
```

### **Fluxo Completo**

```
1. Execução Finalizada (todas verificações processadas)
   ↓
2. ProcessoProcessorService detecta finalização
   ↓
3. PostProcessingPipeline é executado
   ↓
4. ProcessamentoJustificativasStep executa (Order: 5)
   ↓
5. Busca justificativas aprovadas via GSI_Status
   ├─ GSI1_PK = STATUS#APROVADO
   └─ Paginação completa
   ↓
6. Para cada justificativa encontrada:
   ├─ Verifica SelecionarTodos
   │   ├─ Se 1: RemoverPorCriteriosAsync()
   │   │   ├─ Query no GSI_Agregacao
   │   │   └─ BatchDeleteResultadosAsync()
   │   └─ Se 0: RemoverPorIdsAsync()
   │       ├─ GetItem para cada ID
   │       └─ BatchDeleteResultadosAsync()
   └─ Log de registros removidos
   ↓
7. Aguarda 5 segundos para propagação no GSI
   ↓
8. Próximo step (AgregacaoResultadosStep) executa
   ↓
9. Agregação conta apenas apontamentos não justificados
```

## Modelo de Dados

### **Justificativa**

```csharp
[DynamoDBTable("Justificativas")]
public class Justificativa
{
    // Chave Primária
    [DynamoDBHashKey("Id")]
    public string Id { get; set; }

    // GSI_Status (para busca otimizada)
    [DynamoDBGlobalSecondaryIndexHashKey("GSI1_PK", "GSI_Status")]
    public string GSI1_PK { get; set; }  // STATUS#<Status>

    [DynamoDBGlobalSecondaryIndexRangeKey("GSI1_SK", "GSI_Status")]
    public string GSI1_SK { get; set; }  // DATA#<Data>

    // Campos de Identificação do Apontamento
    public string VerificacaoId { get; set; }        // ID da verificação
    public string Empresa { get; set; }              // Sigla da empresa
    public string TabelaReferencia { get; set; }     // Tabela do banco
    public string Campo { get; set; }                // Campo com erro
    public string? Referencia { get; set; }          // Referência do registro
    public string TipoApontamento { get; set; }      // Tipo do apontamento

    // Controle de Seleção
    public string Status { get; set; }               // Solicitado, Aprovado, Rejeitado
    public int SelecionarTodos { get; set; }         // 1 = todos, 0 = IDs específicos
    public List<string>? IdsRelacionados { get; set; } // IDs para remover (SelecionarTodos = 0)

    // Propriedades Computadas
    public bool IsSelecionarTodos => SelecionarTodos == 1;
    public bool IsAprovado => Status == "Aprovado";
}
```

### **Estrutura da Tabela**

```
Tabela: Justificativas
  PK: Id (String)
  
  Campos Principais:
    - VerificacaoId (String)
    - Empresa (String)
    - TabelaReferencia (String)
    - Campo (String)
    - Referencia (String?)
    - TipoApontamento (String)
    - Status (String)
    - SelecionarTodos (Number: 0 ou 1)
    - IdsRelacionados (List<String>)
    
  GSI_Status:
    - GSI1_PK: STATUS#<Status> (ex: STATUS#APROVADO)
    - GSI1_SK: DATA#<Data> (ex: DATA#2025-10-19T20:37:43.435Z)

---

Nota sobre SK da Tabela Resultado:
  Formato ANTIGO: RES#<CodId>
  Formato NOVO: RES#<CodId>#GUID#<GuidAleatorio>
  
  Para buscar por CodId, usamos: begins_with(SK, "RES#<CodId>#")
```

## Implementação Técnica

### **ETAPA 1: Buscar Justificativas Aprovadas**

```csharp
private async Task<List<Justificativa>> BuscarJustificativasAprovadasAsync()
{
    // Query no GSI_Status para buscar apenas justificativas aprovadas
    var request = new QueryRequest
    {
        TableName = "Justificativas",
        IndexName = "GSI_Status",
        KeyConditionExpression = "GSI1_PK = :status",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            { ":status", new AttributeValue { S = "STATUS#APROVADO" } }
        }
    };
    
    // Paginação completa
    do {
        var response = await _dynamoClient.QueryAsync(request);
        // Processar items...
        lastEvaluatedKey = response.LastEvaluatedKey;
    } while (lastEvaluatedKey != null);
    
    return justificativas;
}
```

**Características**:
- Query otimizada usando GSI_Status
- Paginação completa para todas as justificativas
- Filtro automático para status = "Aprovado"

### **ETAPA 2a: Remover por Critérios (SelecionarTodos = 1)**

```csharp
private async Task<int> RemoverPorCriteriosAsync(
    string execucaoId,
    Justificativa justificativa)
{
    // Construir prefixo do GSI1_SK baseado na configuração
    string gsi1SkPrefix;
    
    if (_config.UsarFiltroCompleto)
    {
        // Filtro COMPLETO: VER#{VerifId}#EMP#{Emp}#TAB#{Tab}#CAMPO#{Campo}
        gsi1SkPrefix = $"VER#{justificativa.VerificacaoId}#EMP#{justificativa.Empresa}#TAB#{justificativa.TabelaReferencia}#CAMPO#{justificativa.Campo}";
    }
    else
    {
        // Filtro SIMPLES: apenas VER#{VerifId}
        gsi1SkPrefix = $"VER#{justificativa.VerificacaoId}";
    }
    
    // Query no GSI_Agregacao usando begins_with
    var resultados = await QueryResultadosPorGSIAsync(execucaoId, gsi1SkPrefix);
    
    // Deletar todos os registros encontrados
    return await BatchDeleteResultadosAsync(resultados);
}
```

**Modos de Filtro**:

1. **Filtro Completo** (UsarFiltroCompleto = true):
   - Remove apenas registros que atendem TODOS os critérios
   - GSI1_SK = `VER#<VerifId>#EMP#<Empresa>#TAB#<Tabela>#CAMPO#<Campo>`
   - Mais preciso, remove menos registros

2. **Filtro Simples** (UsarFiltroCompleto = false):
   - Remove TODOS os registros da verificação
   - GSI1_SK = `VER#<VerifId>`
   - Menos preciso, remove todos da verificação

### **ETAPA 2b: Remover por IDs Específicos (SelecionarTodos = 0)**

```csharp
private async Task<int> RemoverPorIdsAsync(
    string execucaoId,
    Justificativa justificativa)
{
    var pk = $"VER#{execucaoId}#{justificativa.VerificacaoId}";
    var resultados = new List<Resultado>();
    
    // Para cada ID, buscar registro usando begins_with no SK
    foreach (var codId in justificativa.IdsRelacionados)
    {
        var skPrefix = $"RES#{codId}#";  // Prefix para begins_with
        var itens = await QueryResultadosPorSKAsync(pk, skPrefix);
        
        if (itens.Any())
        {
            resultados.AddRange(itens);
        }
    }
    
    // Deletar registros encontrados
    return await BatchDeleteResultadosAsync(resultados);
}
```

**Características**:
- Query na tabela principal com `PK = :pk AND begins_with(SK, :sk)`
- Suporta SK com formato: `RES#<CodId>#GUID#<GuidAleatorio>`
- Retorna todos os registros para cada CodId (pode haver múltiplos devido ao GUID)
- Paginação completa para garantir todos os registros

### **ETAPA 3: Deletar Registros em Lote**

```csharp
private async Task<int> BatchDeleteResultadosAsync(List<Resultado> resultados)
{
    var batches = resultados.Chunk(_config.BatchSize).ToList(); // BatchSize = 25
    
    foreach (var batch in batches)
    {
        var writeRequests = batch.Select(r => new WriteRequest
        {
            DeleteRequest = new DeleteRequest
            {
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = r.PK } },
                    { "SK", new AttributeValue { S = r.SK } }
                }
            }
        }).ToList();
        
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                { _dynamoConfig.TableNameResultado, writeRequests }
            }
        };
        
        var response = await _dynamoClient.BatchWriteItemAsync(request);
        
        // Tratar itens não processados (throttling)
        if (response.UnprocessedItems?.Any() == true)
        {
            await Task.Delay(500); // Backoff
            await RetryBatchDeleteAsync(response.UnprocessedItems);
        }
    }
    
    return totalDeletados;
}
```

**Características**:
- Processa em lotes de 25 registros (limite do DynamoDB)
- Retry automático para itens não processados
- Tratamento de throttling com backoff

### **ETAPA 4: Aguardar Propagação no GSI**

```csharp
if (totalRemovidos > 0)
{
    _logger.LogInformation(
        "Aguardando 5 segundos para propagacao das delecoes no GSI_Agregacao...");
    await Task.Delay(5000);
    _logger.LogInformation("Propagacao concluida. Proximo step pode executar.");
}
```

**Por que isso é importante?**
- DynamoDB GSIs têm propagação eventual
- Agregação usa o mesmo GSI_Agregacao para buscar apontamentos
- Aguardar 5 segundos garante que a agregação veja os dados atualizados

## Configuração

### **ProcessamentoJustificativasStepConfiguration**

```csharp
public class ProcessamentoJustificativasStepConfiguration
{
    public bool Enabled { get; set; } = true;              // Habilitar step
    public int Order { get; set; } = 5;                    // Ordem de execução
    public int TimeoutSeconds { get; set; } = 60;          // Timeout
    public int BatchSize { get; set; } = 25;               // Tamanho do lote
    public bool UsarFiltroCompleto { get; set; } = true;   // Filtro completo vs simples
}
```

### **appsettings.json**

```json
{
  "PostProcessing": {
    "ProcessamentoJustificativas": {
      "Enabled": true,
      "Order": 5,
      "TimeoutSeconds": 60,
      "BatchSize": 25,
      "UsarFiltroCompleto": true
    }
  }
}
```

**Ordem de Execução**:
- Order 5: **ANTES** da AgregacaoResultadosStep (Order 10)
- Garante que apontamentos justificados sejam removidos antes da agregação

## Logs Gerados

### **Início do Processamento**

```
[INFO] Iniciando processamento de justificativas aprovadas para execucao exec-123
```

### **Nenhuma Justificativa Encontrada**

```
[INFO] Nenhuma justificativa aprovada encontrada. Pulando step.
```

### **Justificativas Encontradas**

```
[INFO] Encontradas 3 justificativas aprovadas para processamento
```

### **Processamento de Justificativa**

```
[INFO] Processando justificativa justif-456: SelecionarTodos=True, Verificacao=verif-789, Empresa=MA, Tabela=empresa, Campo=CNPJ
```

**Modo "Selecionar Todos"**:
```
[INFO] Buscando registros para deletar (Filtro COMPLETO): GSI1_PK=EXEC#exec-123, GSI1_SK begins_with=VER#verif-789#EMP#MA#TAB#empresa#CAMPO#CNPJ
[INFO] Encontrados 45 registros para deletar (VerificacaoId=verif-789, Empresa=MA, Tabela=empresa, Campo=CNPJ)
[INFO] Deletando 45 registros em 2 lotes de até 25 itens
[DEBUG] Query GSI concluida. GSI1_PK=EXEC#exec-123, GSI1_SK begins_with=VER#verif-789#EMP#MA#TAB#empresa#CAMPO#CNPJ, Total=45
[DEBUG] BatchDelete concluido. 45 registros deletados
[INFO] Justificativa justif-456: 45 registros removidos
```

**Modo "IDs Específicos"**:
```
[INFO] Removendo 15 IDs específicos: id-001, id-002, id-003, id-004, id-005...
[DEBUG] PK para busca: VER#exec-123#verif-789 (ExecId=exec-123, VerifId=verif-789)
[DEBUG] Buscando registro: PK=VER#exec-123#verif-789, SK begins_with=RES#id-001#
[DEBUG] Encontrados 2 registros para CodId=id-001
[DEBUG] Query por SK concluida. PK=VER#exec-123#verif-789, SK begins_with=RES#id-001#, Total=2
[INFO] Encontrados 14 registros (pode haver multiplos por CodId devido ao GUID) de 15 IDs para deletar
[DEBUG] BatchDelete concluido. 14 registros deletados
[INFO] Justificativa justif-456: 14 registros removidos
```

### **Conclusão**

```
[INFO] Processamento de justificativas concluido. 3 justificativas processadas, 142 registros removidos
[INFO] Aguardando 5 segundos para propagacao das delecoes no GSI_Agregacao...
[INFO] Propagacao concluida. Proximo step pode executar.
```

### **Erros**

```
[WARNING] Nenhum registro encontrado para deletar! ExecucaoId=exec-123, GSI1_SK_Prefix=VER#verif-789#EMP#MA#TAB#empresa#CAMPO#CNPJ
[WARNING] Justificativa justif-456 tem SelecionarTodos=0 mas IdsRelacionados esta vazio. Pulando.
[WARNING] Nenhum dos 15 IDs foi encontrado na tabela Resultado! ExecucaoId=exec-123, VerificacaoId=verif-789
[DEBUG] Query por SK concluida. PK=VER#exec-123#verif-789, SK begins_with=RES#id-001#, Total=0
[ERROR] Erro ao processar justificativa justif-456. Continuando com proximas...
[ERROR] Erro no processamento de justificativas: Timeout após 60 segundos
```

## Exemplos de Uso

### **Exemplo 1: Remover Todos os Apontamentos de uma Verificação**

**Justificativa**:
```json
{
  "Id": "justif-001",
  "VerificacaoId": "verif-duplicidade",
  "Empresa": "MA",
  "TabelaReferencia": "empresa",
  "Campo": "CNPJ",
  "Status": "Aprovado",
  "SelecionarTodos": 1
}
```

**Processamento**:
1. Busca todos os registros que atendem: `VER#verif-duplicidade#EMP#MA#TAB#empresa#CAMPO#CNPJ`
2. Remove os 45 registros encontrados
3. Agregação não conta esses registros

### **Exemplo 2: Remover IDs Específicos**

**Justificativa**:
```json
{
  "Id": "justif-002",
  "VerificacaoId": "verif-vif",
  "Empresa": "RS",
  "TabelaReferencia": "funcionario",
  "Campo": "CPF",
  "Status": "Aprovado",
  "SelecionarTodos": 0,
  "IdsRelacionados": ["id-001", "id-002", "id-003", "id-004"]
}
```

**Processamento**:
1. Para cada ID, busca usando begins_with: `PK=VER#exec-123#verif-vif, SK begins_with=RES#id-001#`
2. Query retorna todos os registros para cada CodId (pode haver múltiplos por GUID)
3. Remove apenas os registros encontrados
4. Agregação não conta esses registros

## Scripts de Infraestrutura

### **1. Criar Tabela Justificativas**

```powershell
cd EmbaixadasWorkManager
.\create-table-justificativas.ps1
```

**Estrutura criada**:
- Tabela: `Justificativas`
- PK: `Id` (String)
- GSI: `GSI_Status` (GSI1_PK: String, GSI1_SK: String)

### **2. Criar GSI_Status (se tabela já existe)**

```powershell
cd EmbaixadasWorkManager
.\create-gsi-status-justificativa.ps1
```

### **3. Atualizar Registros Existentes**

```powershell
cd EmbaixadasWorkManager
.\atualizar-gsi-justificativas.ps1
```

**O que faz**:
- Popula `GSI1_PK` com `STATUS#<Status>`
- Popula `GSI1_SK` com `DATA#<Data>`
- Necessário para que o GSI funcione corretamente

## Integração com Outros Steps

### **Pipeline de Pós-Processamento**

```
Execução Finalizada
  ↓
Pipeline Inicia (Order crescente)
  ↓
[Order 1] AgrupamentoStep (desabilitado)
  ↓
[Order 2] ExclusaoRegistrosStep (desabilitado)
  ↓
[Order 5] ProcessamentoJustificativasStep ✅
  ├─ Remove apontamentos justificados
  └─ Aguarda 5s para propagação
  ↓
[Order 10] AgregacaoResultadosStep ✅
  ├─ Busca apontamentos (não justificados)
  ├─ Agrupa por critérios
  └─ Insere em ResultadoAgregado
  ↓
Pipeline Concluído
```

**Por que Order 5?**
- **ANTES** da AgregacaoResultadosStep (Order 10)
- Garante que apontamentos justificados sejam removidos primeiro
- Agregação conta apenas apontamentos não justificados

## Benefícios da Implementação

### **1. Automatização**
- Justificativas aprovadas são processadas automaticamente
- Sem necessidade de intervenção manual
- Reduz tempo de processamento

### **2. Precisão**
- Suporte para dois modos de remoção
- Filtros configuráveis (completo vs simples)
- Logs detalhados de cada remoção

### **3. Performance**
- Query otimizada usando GSI_Status
- Deletions em lote (BatchWriteItem)
- Tratamento de throttling com retry

### **4. Observabilidade**
- Logs detalhados em cada etapa
- Contagem de registros removidos
- Tratamento de erros robusto

### **5. Integração**
- Executa automaticamente no pipeline
- Ordem configurável via appsettings
- Habilitar/desabilitar facilmente

## Limitações e Considerações

### **1. Propagação do GSI**
- DynamoDB GSIs têm propagação eventual
- Aguardamos 5 segundos para garantir consistência
- Em casos raros, pode ser necessário aumentar esse tempo

### **2. Performance**
- Query no GSI_Status busca TODAS as justificativas aprovadas
- Se houver muitas justificativas, pode adicionar latência
- Considerar cache se necessário

### **3. Escopo de Execução**
- Processa justificativas de TODAS as execuções
- Não filtra por ExecucaoId na justificativa
- Justificativas são globais, não por execução

### **4. Ordem de Execução**
- **CRÍTICO**: Deve executar ANTES da AgregacaoResultadosStep
- Mudança na ordem pode causar inconsistências
- Verificar configuração do Order

## Próximos Passos Sugeridos

### **1. Melhorias de Performance**
- Cache de justificativas aprovadas
- Filtro por ExecucaoId na justificativa
- Processamento paralelo de múltiplas justificativas

### **2. Funcionalidades Adicionais**
- Dashboard de justificativas processadas
- Métricas de taxa de remoção
- Alertas para justificativas com IDs não encontrados

### **3. Validações**
- Validação de integridade referencial
- Verificação de duplicidade de remoções
- Rollback em caso de falha crítica

## Conclusão

O sistema de processamento de justificativas implementado é **automático**, **configurável** e **robusto**. Permite remover apontamentos justificados de forma eficiente, garantindo que a agregação conte apenas os apontamentos realmente relevantes.

O step se integra perfeitamente ao pipeline de pós-processamento, executando na ordem correta (antes da agregação) e fornecendo logs detalhados para monitoramento e debugging.

**Status**: ✅ **IMPLEMENTAÇÃO COMPLETA** - Pronto para uso!


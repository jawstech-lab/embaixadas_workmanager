# Tabelas de Performance por Empresa - Implementação Completa

## Visão Geral

Esta implementação adiciona duas novas tabelas ao DynamoDB para melhorar a performance de consultas e manter auditoria completa das execuções por empresa. O sistema agora grava automaticamente registros nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus sempre que uma execução é processada.

## Objetivo

Resolver o problema de performance ao consultar execuções por empresa, criando:
1. **Visão rápida** da última execução de cada empresa
2. **Histórico completo** de todas as execuções por empresa

## Arquitetura

### **Fluxo de Gravação**

```
1. Execução recebida da fila
   ↓
2. Busca execução no DynamoDB
   ↓
3. NOVO: Grava nas tabelas de performance
   ├─ Extrai siglas de empresas (ex: "MA,RS" → ["MA", "RS"])
   ├─ Para cada empresa:
   │  ├─ AÇÃO 1: Grava em ExecucaoEmpresaStatus (auditoria)
   │  └─ AÇÃO 2: Atualiza ExecucaoResumoView (condicional)
   ↓
4. Continua processamento normal
```

### **Ponto de Integração**

**Arquivo**: `Services/ProcessorService.cs`
**Método**: `ProcessExecucaoMessageAsync()`
**Linha**: Logo após buscar a execução (linha ~60)

```csharp
// Buscar a execução existente no DynamoDB
var execucao = await _dynamoDbService.GetExecucaoAsync(execucaoId);

// NOVO: Gravar nas tabelas de performance
await _execucaoEmpresaService.GravarExecucaoPorEmpresasAsync(
    execucao.Id,
    execucao.Empresa,
    execucao.DataSolicitacao,
    execucao.Status);
```

## Tabelas Implementadas

### **A. ExecucaoResumoView (Visão Rápida)**

#### **Propósito**
Armazena apenas a última execução de cada empresa para consultas rápidas.

#### **Estrutura de Chaves**
```
PK: VIEW#LAST_EXEC (fixo para todas as empresas)
SK: EMP#<Sigla> (ex: EMP#MA, EMP#RS)
```

#### **Atributos**
| Atributo | Tipo | Descrição | Exemplo |
|----------|------|-----------|---------|
| PK_VIEW | String | Partition Key fixo | VIEW#LAST_EXEC |
| SK_VIEW | String | Sort Key com sigla | EMP#MA |
| ExecucaoId | String | ID da última execução | c5a903ce-... |
| DataSolicitacao | DateTime | Data da execução | 2025-10-05T20:27:59.457Z |
| Status | String | Status da execução | Cadastrado |
| SiglaEmpresa | String | Sigla da empresa | MA |

#### **Exemplo de Item**
```json
{
  "PK_VIEW": "VIEW#LAST_EXEC",
  "SK_VIEW": "EMP#MA",
  "ExecucaoId": "c5a903ce-a749-4c82-9d55-9fae7c8e8a6f",
  "DataSolicitacao": "2025-10-05T20:27:59.457Z",
  "Status": "Cadastrado",
  "SiglaEmpresa": "MA"
}
```

#### **Condição de Atualização**
```
attribute_not_exists(DataSolicitacao) OR :novaData > DataSolicitacao
```
- Atualiza apenas se não existe registro
- OU se a nova data é mais recente que a existente

#### **Consultas Possíveis**
```csharp
// Buscar última execução de uma empresa
PK = "VIEW#LAST_EXEC"
SK = "EMP#MA"

// Buscar últimas execuções de todas as empresas
PK = "VIEW#LAST_EXEC"
```

---

### **B. ExecucaoEmpresaStatus (Auditoria Completa)**

#### **Propósito**
Mantém histórico completo de todas as execuções de cada empresa, ordenado por data.

#### **Estrutura de Chaves**
```
PK: EMP#<Sigla> (ex: EMP#MA, EMP#RS)
SK: DATA#<DataISO>#<ExecucaoId>
```

#### **Atributos**
| Atributo | Tipo | Descrição | Exemplo |
|----------|------|-----------|---------|
| PK_STATUS | String | Partition Key com empresa | EMP#MA |
| SK_STATUS | String | Sort Key com data e ID | DATA#2025-10-05T20:27:59.457Z#c5a903ce-... |
| ExecucaoId | String | ID da execução | c5a903ce-... |
| DataSolicitacao | DateTime | Data da execução | 2025-10-05T20:27:59.457Z |
| Status | String | Status da execução | Cadastrado |
| SiglaEmpresa | String | Sigla da empresa | MA |

#### **Exemplo de Item**
```json
{
  "PK_STATUS": "EMP#MA",
  "SK_STATUS": "DATA#2025-10-05T20:27:59.457Z#c5a903ce-a749-4c82-9d55-9fae7c8e8a6f",
  "ExecucaoId": "c5a903ce-a749-4c82-9d55-9fae7c8e8a6f",
  "DataSolicitacao": "2025-10-05T20:27:59.457Z",
  "Status": "Cadastrado",
  "SiglaEmpresa": "MA"
}
```

#### **Características**
- SEMPRE grava um novo registro (sem condição)
- Cria histórico completo de todas as execuções
- Sort Key permite ordenação cronológica

#### **Consultas Possíveis**
```csharp
// Buscar todas as execuções de uma empresa
PK = "EMP#MA"

// Buscar execuções de uma empresa em um período
PK = "EMP#MA"
SK BETWEEN "DATA#2025-10-01" AND "DATA#2025-10-31"

// Buscar últimas N execuções de uma empresa
PK = "EMP#MA"
ScanIndexForward = false
Limit = 10
```

## Implementação Técnica

### **1. Modelos**

#### **ExecucaoResumoView.cs**
```csharp
[DynamoDBTable("ExecucaoResumoView")]
public class ExecucaoResumoView
{
    [DynamoDBHashKey("PK_VIEW")]
    public string PK { get; set; } = "VIEW#LAST_EXEC";

    [DynamoDBRangeKey("SK_VIEW")]
    public string SK { get; set; } = string.Empty;

    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    [DynamoDBProperty("DataSolicitacao")]
    public DateTime DataSolicitacao { get; set; }

    [DynamoDBProperty("Status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDBProperty("SiglaEmpresa")]
    public string SiglaEmpresa { get; set; } = string.Empty;

    public static string CriarSK(string siglaEmpresa) => $"EMP#{siglaEmpresa.ToUpper()}";
}
```

#### **ExecucaoEmpresaStatus.cs**
```csharp
[DynamoDBTable("ExecucaoEmpresaStatus")]
public class ExecucaoEmpresaStatus
{
    [DynamoDBHashKey("PK_STATUS")]
    public string PK { get; set; } = string.Empty;

    [DynamoDBRangeKey("SK_STATUS")]
    public string SK { get; set; } = string.Empty;

    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    [DynamoDBProperty("DataSolicitacao")]
    public DateTime DataSolicitacao { get; set; }

    [DynamoDBProperty("Status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDBProperty("SiglaEmpresa")]
    public string SiglaEmpresa { get; set; } = string.Empty;

    public static string CriarPK(string siglaEmpresa) => $"EMP#{siglaEmpresa.ToUpper()}";
    
    public static string CriarSK(DateTime dataSolicitacao, string execucaoId)
    {
        var dataFormatada = dataSolicitacao.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        return $"DATA#{dataFormatada}#{execucaoId}";
    }
}
```

### **2. Service**

#### **IExecucaoEmpresaService**
```csharp
public interface IExecucaoEmpresaService
{
    Task GravarExecucaoPorEmpresasAsync(
        string execucaoId, 
        string empresasString, 
        DateTime dataSolicitacao, 
        string status);
    
    List<string> ExtrairSiglasEmpresas(string empresasString);
}
```

#### **ExecucaoEmpresaService**
- **Parse de empresas**: Suporta separadores: `,`, `;`, `|`
- **Normalização**: Remove espaços, converte para maiúsculas, remove duplicatas
- **Operação condicional**: Usa `ConditionExpression` para ExecucaoResumoView
- **Tratamento de erros**: Continua processando outras empresas se uma falhar
- **Logs detalhados**: Registra sucesso/falha de cada operação

### **3. Integração**

#### **ProcessorService.cs**
```csharp
public ProcessorService(
    ILogger<ProcessorService> logger,
    IDynamoDbService dynamoDbService,
    IVerificacaoProcessorService verificacaoProcessor,
    ISqsService sqsService,
    IExecucaoEmpresaService execucaoEmpresaService, // NOVO
    IOptions<ProcessamentoConfiguration> processamentoConfig)
{
    // ...
    _execucaoEmpresaService = execucaoEmpresaService;
}

public async Task<bool> ProcessExecucaoMessageAsync(string messageBody, string messageId)
{
    var execucao = await _dynamoDbService.GetExecucaoAsync(execucaoId);
    
    // NOVO: Gravar nas tabelas de performance
    try
    {
        await _execucaoEmpresaService.GravarExecucaoPorEmpresasAsync(
            execucao.Id,
            execucao.Empresa,
            execucao.DataSolicitacao,
            execucao.Status);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao gravar execucao por empresas. Continuando processamento.");
        // Não interrompe o processamento
    }
    
    // Continua processamento normal...
}
```

#### **Program.cs**
```csharp
// Serviços de performance e auditoria
services.AddSingleton<IExecucaoEmpresaService, ExecucaoEmpresaService>();
```

### **4. Configuração**

#### **DynamoDbConfiguration**
```csharp
public class DynamoDbConfiguration
{
    public string TableNameExecucaoResumoView { get; set; } = "ExecucaoResumoView";
    public string TableNameExecucaoEmpresaStatus { get; set; } = "ExecucaoEmpresaStatus";
}
```

#### **appsettings.json**
```json
{
  "DynamoDB": {
    "TableNameExecucaoResumoView": "ExecucaoResumoView",
    "TableNameExecucaoEmpresaStatus": "ExecucaoEmpresaStatus"
  }
}
```

## Uso e Instalação

### **1. Criar Tabelas no DynamoDB**

#### **Opção A: Criar todas de uma vez**
```powershell
.\create-tables-performance.ps1 -Region "sa-east-1" -ProfileName "default"
```

#### **Opção B: Criar individualmente**
```powershell
# Criar ExecucaoResumoView
.\create-table-execucao-resumo-view.ps1

# Criar ExecucaoEmpresaStatus
.\create-table-execucao-empresa-status.ps1
```

### **2. Executar o Sistema**
```bash
dotnet run --environment Development
```

### **3. Enviar Mensagem de Teste**
```powershell
# Criar execução com múltiplas empresas
# Campo Empresa: "MA,RS,SP"
.\test-send-messages.ps1 -MessageCount 1
```

## Logs Gerados

### **Logs de Sucesso**
```
[INFO] Iniciando gravacao de execucao por empresas. ExecucaoId: c5a903ce-..., Empresas: MA,RS,SP
[INFO] Encontradas 3 empresas para processar: MA, RS, SP
[DEBUG] Processando empresa: MA
[DEBUG] Status detalhado gravado com sucesso: Empresa=MA, PK=EMP#MA, SK=DATA#2025-10-05...
[DEBUG] Resumo atualizado condicionalmente: Empresa=MA, PK=VIEW#LAST_EXEC, SK=EMP#MA
[INFO] Gravacao concluida para 3 empresas. Status: 3 sucesso, 0 falhas. Resumo: 3 atualizado, 0 nao atualizado
```

### **Logs de Condição Não Atendida**
```
[DEBUG] Condicao nao atendida para empresa MA. Registro existente e mais recente ou igual
```

### **Logs de Erro**
```
[ERROR] Erro ao gravar status detalhado para empresa MA
[ERROR] Erro ao processar empresa MA para execucao c5a903ce-...
[ERROR] Erro ao gravar execucao por empresas. Continuando processamento. ExecucaoId: c5a903ce-...
```

## Benefícios

### **1. Performance**
- Consulta da última execução: O(1) - acesso direto por chave
- Histórico de empresa: Ordenado automaticamente por data
- Sem necessidade de scan da tabela principal

### **2. Escalabilidade**
- Billing mode: PAY_PER_REQUEST (auto-scaling)
- Estrutura otimizada para consultas frequentes
- Separação de dados por empresa (partition key)

### **3. Auditoria**
- Histórico completo mantido em ExecucaoEmpresaStatus
- Rastreabilidade de todas as execuções
- Dados imutáveis (sempre insere novo registro)

### **4. Flexibilidade**
- Suporta múltiplos separadores (`,`, `;`, `|`)
- Normalização automática (maiúsculas, sem espaços)
- Tratamento de erros não interrompe processamento

### **5. Manutenibilidade**
- Código limpo e bem documentado
- Separação clara de responsabilidades
- Logs detalhados para debugging
- Scripts automatizados para criação de tabelas

## Casos de Uso

### **1. Dashboard - Última Execução por Empresa**
```csharp
// Query rápida para buscar última execução de todas as empresas
var query = new QueryRequest
{
    TableName = "ExecucaoResumoView",
    KeyConditionExpression = "PK_VIEW = :pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":pk", new AttributeValue { S = "VIEW#LAST_EXEC" } }
    }
};
```

### **2. Histórico de uma Empresa**
```csharp
// Query para buscar todas as execuções de MA
var query = new QueryRequest
{
    TableName = "ExecucaoEmpresaStatus",
    KeyConditionExpression = "PK_STATUS = :pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":pk", new AttributeValue { S = "EMP#MA" } }
    },
    ScanIndexForward = false, // Mais recentes primeiro
    Limit = 10
};
```

### **3. Execuções em Período**
```csharp
// Query para buscar execuções de MA em outubro/2025
var query = new QueryRequest
{
    TableName = "ExecucaoEmpresaStatus",
    KeyConditionExpression = "PK_STATUS = :pk AND SK_STATUS BETWEEN :start AND :end",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":pk", new AttributeValue { S = "EMP#MA" } },
        { ":start", new AttributeValue { S = "DATA#2025-10-01" } },
        { ":end", new AttributeValue { S = "DATA#2025-10-31" } }
    }
};
```

## Custos Estimados

### **PAY_PER_REQUEST**
- **Write**: $1.25 por milhão de requisições
- **Read**: $0.25 por milhão de requisições

### **Exemplo de Uso**
```
Cenário: 1000 execuções/dia, cada uma com 3 empresas

Writes por dia:
- ExecucaoEmpresaStatus: 1000 × 3 = 3000 writes
- ExecucaoResumoView: ~1000 × 3 = 3000 writes (algumas serão rejeitadas)
- Total: ~6000 writes/dia

Custo mensal (30 dias):
- Writes: 180.000 × $1.25/milhão = $0.225
- Reads (estimado 10x): 1.800.000 × $0.25/milhão = $0.45
- Total: ~$0.68/mês
```

## Considerações e Limitações

### **1. Consistência Eventual**
- DynamoDB usa consistência eventual por padrão
- Leituras logo após escrita podem não refletir últimos dados

### **2. Condição de Atualização**
- ExecucaoResumoView: Pode falhar se data não for mais recente
- Não é erro - comportamento esperado
- ConditionalCheckFailedException é capturada e logada como DEBUG

### **3. Parsing de Empresas**
- Assume formato: "SIGLA,SIGLA,SIGLA"
- Remove espaços e converte para maiúsculas
- Suporta múltiplos separadores

### **4. Falhas Isoladas**
- Erro em uma empresa não interrompe outras
- Erro na gravação das tabelas não interrompe processamento principal
- Logs detalhados para debugging

## Próximos Passos Sugeridos

### **1. Índices Secundários**
- GSI por Status para filtrar execuções por status
- GSI por Data global para consultas cross-empresa

### **2. TTL (Time To Live)**
- Configurar TTL em ExecucaoEmpresaStatus
- Manter apenas últimos N meses de histórico

### **3. Métricas**
- CloudWatch Metrics para monitorar uso
- Alertas para falhas em massa

### **4. API de Consulta**
- Endpoints REST para consultar tabelas
- Dashboard web para visualização

## Conclusão

Esta implementação adiciona capacidades importantes de performance e auditoria ao sistema, permitindo consultas rápidas por empresa e mantendo histórico completo. O design é escalável, eficiente e mantém compatibilidade total com o fluxo existente.

A separação de dados por empresa (partition key) garante ótima performance mesmo com grande volume de dados, e a estrutura de Sort Key permite consultas flexíveis por período.

Os scripts automatizados facilitam a criação das tabelas, e os logs detalhados permitem monitoramento e debugging eficiente.





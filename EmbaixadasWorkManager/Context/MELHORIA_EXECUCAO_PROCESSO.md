# Melhoria do Sistema de Execução - ExecucaoProcesso

## Visão Geral

Esta melhoria implementa um sistema básico de rastreamento para o processo de execução, criando uma nova tabela `ExecucaoProcesso` que registra o número de verificações processadas.

## Problema Identificado

### **Antes da Melhoria:**
- Sistema não rastreava quantas verificações foram processadas
- Falta de visibilidade sobre o volume de trabalho executado

### **Após a Melhoria:**
- Rastreamento básico do número de verificações processadas
- Registro simples do processo de execução

## Nova Arquitetura

### **Fluxo de Processamento Atualizado:**

```
1. Recebe Mensagem SQS de Execução
   ↓
2. Cria Registro na Tabela Execucao (Status: EmProcessamento)
   ↓
3. Processa Verificações e Envia Queries para Fila
   ↓
4. Cria Registro na Tabela ExecucaoProcesso
   ↓
5. Registra Quantidade Real de Verificações Processadas
   ↓
6. Sistema Processa Fila de Processo (fila-execucao-processo-dev)
   ↓
7. Atualiza Contadores e Status do ExecucaoProcesso
   ↓
8. Atualiza Status da Execucao (FinalizadaComSucesso/FinalizadaComErro)
```

## Nova Tabela: ExecucaoProcesso

### **Estrutura da Tabela:**

```csharp
public class ExecucaoProcesso
{
    // Identificação
    public string Id { get; set; }                    // Hash Key
    public string ExecucaoId { get; set; }            // ID da execução associada
    
    // Informações do Processamento
    public int QuantidadeVerificacoes { get; set; }   // Quantidade total de verificações
    public int VerificacoesProcessadas { get; set; }  // Verificações processadas com sucesso
    public int Erros { get; set; }                    // Verificações com erro
    public DateTime DataInicio { get; set; }          // Início do processamento
    public string Status { get; set; }                // Status atual do processo
}
```

### **Status Possíveis:**

```csharp
public static class StatusExecucaoProcesso
{
    public const string AguardandoProcessamento = "AguardandoProcessamento";
    public const string FinalizadoComSucesso = "FinalizadoComSucesso";
    public const string FinalizadoComErro = "FinalizadoComErro";
}
```

## Novo Serviço: ExecucaoProcessoService

### **Funcionalidade Principal:**

#### **Criação de Processos**
```csharp
Task<ExecucaoProcesso> CriarProcessoAsync(string execucaoId, int quantidadeVerificacoes)
```
- Cria novo processo com status "AguardandoProcessamento"
- Registra a quantidade de verificações que serão processadas
- Define data de início do processamento

#### **Atualização de Processos**
```csharp
Task<bool> AtualizarProcessoAsync(string processoId, bool isSuccess)
```
- Atualiza contadores baseado no resultado da verificação
- Incrementa `VerificacoesProcessadas` se `isSuccess = true`
- Incrementa `Erros` se `isSuccess = false`
- Atualiza status para "FinalizadoComSucesso" ou "FinalizadoComErro" quando todas as verificações são processadas

#### **Busca de Processos**
```csharp
Task<ExecucaoProcesso?> BuscarProcessoAsync(string processoId)
```
- Busca um processo específico pelo ID

## Integração com ExecucaoProcessorService

### **Mudanças Implementadas:**

#### **1. Injeção de Dependência**
```csharp
private readonly IExecucaoProcessoService _execucaoProcessoService;

public ExecucaoProcessorService(
    ILogger<ExecucaoProcessorService> logger,
    IDynamoDbService dynamoDbService,
    IVerificacaoProcessorService verificacaoProcessor,
    IExecucaoProcessoService execucaoProcessoService)
```

#### **2. Criação do Processo**
```csharp
// Processar verificações e enviar queries primeiro
var processResult = await ProcessExecucaoAsync(execucao);

// Criar processo de execução APÓS processar as verificações
var processo = await _execucaoProcessoService.CriarProcessoAsync(execucao.Id, processResult.ValidacoesProcessadas);
```

## Nova Fila: fila-execucao-processo-dev

### **Propósito:**
Processar mensagens de resultado das verificações e atualizar o `ExecucaoProcesso` correspondente.

### **Estrutura da Mensagem:**
```json
{
  "IdProcesso": "123e4567-e89b-12d3-a456-426614174000",
  "isSuccess": true
}
```

### **Processamento:**
- **`isSuccess = true`**: Incrementa `VerificacoesProcessadas`
- **`isSuccess = false`**: Incrementa `Erros`
- **Finalização**: Quando `VerificacoesProcessadas + Erros >= QuantidadeVerificacoes`
  - **Sem erros**: Status = "FinalizadoComSucesso"
  - **Com erros**: Status = "FinalizadoComErro"

## Novo Serviço: ProcessoProcessorService

### **Funcionalidade:**
- Processa mensagens da fila `fila-execucao-processo-dev`
- Deserializa mensagens `ProcessoMessage`
- Valida dados obrigatórios
- Delega atualização para `ExecucaoProcessoService`

## Sincronização de Status: Execucao ↔ ExecucaoProcesso

### **Status da Execução:**
```csharp
public static class StatusExecucao
{
    public const string Pendente = "Pendente";
    public const string EmProcessamento = "EmProcessamento";
    public const string FinalizadaComSucesso = "FinalizadaComSucesso";
    public const string FinalizadaComErro = "FinalizadaComErro";
}
```

### **Sincronização Automática:**
- **Início**: Execução criada com status "EmProcessamento" e DataInicio
- **Processamento**: Sistema processa verificações e cria ExecucaoProcesso
- **Finalização**: Quando ExecucaoProcesso é finalizado:
  - **Sucesso**: Execução → "FinalizadaComSucesso" + DataFim
  - **Erro**: Execução → "FinalizadaComErro" + DataFim + Erro

## Configuração

### **1. Arquivos de Configuração Atualizados:**

#### **appsettings.json:**
```json
{
  "SQS": {
    "FilaExecucao": "fila-execucao",
    "FilaExecucaoQuery": "fila-execucao-query",
    "FilaExecucaoProcesso": "fila-execucao-processo",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20,
    "VisibilityTimeoutSeconds": 300
  },
  "DynamoDB": {
    "TableNameExecucao": "Execucao",
    "TableNameVerificacao": "Verificacoes",
    "TableNameExecucaoVerificacao": "ExecucaoVerificacao",
    "TableNameExecucaoProcesso": "ExecucaoProcesso",
    "ServiceUrl": "",
    "UseLocalStack": false
  }
}
```

#### **appsettings.Development.json:**
```json
{
  "SQS": {
    "FilaExecucao": "fila-execucao-dev",
    "FilaExecucaoQuery": "fila-execucao-query-dev",
    "FilaExecucaoProcesso": "fila-execucao-processo-dev",
    "MaxNumberOfMessages": 5,
    "WaitTimeSeconds": 10,
    "VisibilityTimeoutSeconds": 60
  },
  "DynamoDB": {
    "TableNameExecucao": "Execucoes",
    "TableNameVerificacao": "Verificacoes",
    "TableNameExecucaoVerificacao": "ExecucaoVerificacao",
    "TableNameExecucaoProcesso": "ExecucaoProcesso",
    "ServiceUrl": "",
    "UseLocalStack": false
  }
}
```

### **2. Registro de Serviços:**
```csharp
// Program.cs
services.AddSingleton<IExecucaoProcessoService, ExecucaoProcessoService>();
services.AddSingleton<IProcessoProcessorService, ProcessoProcessorService>();
```

## Script de Criação da Tabela

### **Executar o Script:**
```powershell
.\create-table-execucao-processo.ps1
```

### **Parâmetros Opcionais:**
```powershell
.\create-table-execucao-processo.ps1 -TableName "ExecucaoProcesso" -Region "sa-east-1" -ProfileName "default"
```

## Benefícios da Melhoria

### **1. Visibilidade Básica**
- Rastreamento do número **real** de verificações processadas
- Registro do processo após o processamento efetivo das verificações

### **2. Sincronização de Status** ✨ **NOVO**
- **Status automático** da execução sincronizado com o processo
- **Rastreamento completo** do ciclo de vida (Pendente → EmProcessamento → Finalizada)
- **Datas de início e fim** registradas automaticamente
- **Tratamento de erros** com mensagens descritivas

### **3. Base para Expansão**
- Estrutura simples que pode ser expandida no futuro
- Facilita implementação de funcionalidades mais avançadas

## Exemplo de Uso

### **1. Executar o Sistema:**
```bash
dotnet run --environment Development
```

### **2. Enviar Mensagem de Teste:**
```powershell
.\test-send-messages.ps1 -MessageCount 1
```

### **3. Monitorar o Processamento:**
- Logs mostrarão criação do processo
- Quantidade de verificações será registrada
- Processo será criado com status "AguardandoProcessamento"
- Sistema processará mensagens da fila `fila-execucao-processo-dev`

### **4. Verificar Resultados:**
- Tabela `ExecucaoProcesso` conterá registro básico
- Campo `QuantidadeVerificacoes` mostrará o total processado
- Status será "AguardandoProcessamento"
- Contadores `VerificacoesProcessadas` e `Erros` serão atualizados conforme mensagens são processadas
- Status final será "FinalizadoComSucesso" ou "FinalizadoComErro"

### **5. Monitorar Status da Execução:**
- **Tabela Execucao**: Status será atualizado automaticamente
  - `EmProcessamento` → `FinalizadaComSucesso` ou `FinalizadaComErro`
  - `DataInicio` e `DataFim` serão preenchidas
  - Campo `Erro` será preenchido em caso de falha

### **6. Testar Fila de Processo:**
```bash
# Enviar mensagem de sucesso
aws sqs send-message \
  --queue-url "https://sqs.region.amazonaws.com/account/fila-execucao-processo-dev" \
  --message-body '{"IdProcesso":"123e4567-e89b-12d3-a456-426614174000","isSuccess":true}'

# Enviar mensagem de erro
aws sqs send-message \
  --queue-url "https://sqs.region.amazonaws.com/account/fila-execucao-processo-dev" \
  --message-body '{"IdProcesso":"123e4567-e89b-12d3-a456-426614174000","isSuccess":false}'
```

## Próximos Passos

### **1. Melhorias Futuras:**
- [ ] Rastreamento individual de cada verificação
- [ ] Status mais detalhados (EmAndamento, Concluido, etc.)
- [ ] Métricas de performance e tempo de processamento
- [ ] Dashboard para monitoramento

### **2. Funcionalidades Avançadas:**
- [ ] Controle de status em tempo real
- [ ] Rastreamento de falhas por verificação
- [ ] Estatísticas agregadas de performance
- [ ] Alertas automáticos para problemas

## Conclusão

Esta melhoria implementa um sistema básico de rastreamento que:

- **Registra o Volume**: Conta quantas verificações foram processadas
- **Simples e Eficiente**: Implementação minimalista e focada
- **Base para Crescimento**: Estrutura que pode ser expandida no futuro
- **Fácil Manutenção**: Código simples e direto

O sistema agora tem visibilidade básica sobre o processamento de execuções, fornecendo uma base sólida para funcionalidades mais avançadas no futuro.

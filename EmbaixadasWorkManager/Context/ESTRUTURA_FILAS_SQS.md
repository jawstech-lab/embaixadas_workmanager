# Estrutura das Filas SQS - WorkManager

## Visão Geral

Este documento descreve exatamente o que é gravado e lido em cada fila SQS do sistema WorkManager, incluindo a estrutura das mensagens e o fluxo de dados.

## 📥 **fila-execucao-query - O que é GRAVADO**

### **Propósito da Fila**
Esta fila recebe mensagens leves contendo apenas IDs para processamento de queries SQL. O sistema busca os dados completos na tabela `ExecucaoVerificacao` do DynamoDB.

### **Estrutura da Mensagem Gravada**

#### **Modelo: QueryExecutionMessage**
```json
{
  "execucaoVerificacaoId": "uuid-execucao#uuid-verificacao",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

#### **Exemplo Real**
```json
{
  "execucaoVerificacaoId": "92de1003-bec3-4767-985e-6eb500910a5a#a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": "2024-12-19T10:30:00Z"
}
```

### **Campos da Mensagem**

| Campo | Tipo | Descrição | Exemplo |
|-------|------|-----------|---------|
| `execucaoVerificacaoId` | string | ID composto da execução e verificação | "exec123#verif456" |
| `timestamp` | DateTime | Timestamp da criação da mensagem | "2024-12-19T10:30:00Z" |

### **Como é Gravada**
```csharp
// VerificacaoProcessorService.EnviarQueryExecutionAsync()
var mensagem = new QueryExecutionMessage
{
    ExecucaoVerificacaoId = $"{execucao.Id}#{verificacao.Id}",
    Timestamp = DateTime.UtcNow
};

var json = JsonSerializer.Serialize(mensagem);
await _sqsService.SendMessageAsync(filaQuery, json);
```

### **Dados Completos no DynamoDB**
Quando a mensagem é gravada, os dados completos já estão salvos na tabela `ExecucaoVerificacao`:

```json
{
  "Id": "92de1003-bec3-4767-985e-6eb500910a5a#a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "ExecucaoId": "92de1003-bec3-4767-985e-6eb500910a5a",
  "VerificacaoId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Base": "BaseTeste",
  "DataBase": "2024-01-01T00:00:00Z",
  "Empresa": "EmpresaTeste",
  "Usuario": "usuario.teste",
  "QueryId": "query-123",
  "Sql": "SELECT * FROM Tabela WHERE empresa = 'EmpresaTeste'",
  "SqlOriginal": "SELECT * FROM Tabela WHERE empresa = @EMPRESA",
  "Parametros": {"EMPRESA": "string"},
  "ValoresParametros": [{"IdParametro": "param-123", "ValorParametro": "EmpresaTeste"}],
  "Status": "Pendente",
  "TimeoutSegundos": 60,
  "Prioridade": 1,
  "DataCriacao": "2024-12-19T10:30:00Z"
}
```

## 📤 **fila-execucao-processo - O que é LIDO**

### **Propósito da Fila**
Esta fila recebe mensagens com os resultados do processamento das verificações, permitindo atualizar o status e contadores da execução principal.

### **Estrutura da Mensagem Lida**

#### **Modelo: ProcessoMessage**
```json
{
  "execucaoVerificacaoId": "uuid-execucao-verificacao-unico",
  "isSuccess": true,
  "resultado": "dados da verificação processada",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

#### **Exemplo Real - Sucesso**
```json
{
  "execucaoVerificacaoId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "isSuccess": true,
  "resultado": "Verificação concluída com sucesso. 150 registros encontrados.",
  "timestamp": "2024-12-19T10:35:00Z"
}
```

#### **Exemplo Real - Erro**
```json
{
  "execucaoVerificacaoId": "b2c3d4e5-f6g7-8901-bcde-f23456789012",
  "isSuccess": false,
  "resultado": "Timeout na execução da query. Excedeu 60 segundos.",
  "timestamp": "2024-12-19T10:40:00Z"
}
```

### **Campos da Mensagem**

| Campo | Tipo | Descrição | Exemplo |
|-------|------|-----------|---------|
| `execucaoVerificacaoId` | string | GUID único da ExecucaoVerificacao | "a1b2c3d4-e5f6-7890-abcd-ef1234567890" |
| `isSuccess` | boolean | Indica se a verificação foi bem-sucedida | true/false |
| `resultado` | string | Resultado ou erro da verificação | "150 registros encontrados" |
| `timestamp` | DateTime | Timestamp do resultado | "2024-12-19T10:35:00Z" |

### **Como é Processada**
```csharp
// ProcessoProcessorService.ProcessarMensagemProcessoAsync()
var mensagem = JsonSerializer.Deserialize<ProcessoMessage>(messageBody);

// NOVA LÓGICA: Buscar ExecucaoVerificacao pelo GUID único para obter ExecucaoId
var execucaoVerificacao = await _dynamoDbService.GetExecucaoVerificacaoAsync(mensagem.ExecucaoVerificacaoId);

// Buscar execução no DynamoDB usando o ExecucaoId da ExecucaoVerificacao
var execucao = await _dynamoDbService.GetExecucaoAsync(execucaoVerificacao.ExecucaoId);

// Atualizar contadores baseado no resultado
if (mensagem.IsSuccess)
{
    execucao.VerificacoesProcessadas++;
}
else
{
    execucao.VerificacoesComErro++;
}

// Verificar se todas as verificações foram processadas
var totalProcessadas = execucao.VerificacoesProcessadas + execucao.VerificacoesComErro;
if (totalProcessadas >= execucao.QuantidadeVerificacoes)
{
    // Finalizar execução
    if (execucao.VerificacoesComErro == 0)
    {
        execucao.Status = "FinalizadaComSucesso";
    }
    else
    {
        execucao.Status = "FinalizadaComErro";
        execucao.Erro = $"{execucao.VerificacoesComErro} verificações falharam";
    }
    execucao.DataFim = DateTime.UtcNow;
}

// Atualizar no DynamoDB
await _dynamoDbService.UpdateAsync(execucao);
```

## 🔄 **Fluxo Completo de Dados**

### **1. Criação da Execução**
```
Sistema Externo → Cria Execucao no DynamoDB → Status: "Pendente"
```

### **2. Processamento da Execução**
```
Worker → Lê fila-execucao → Processa verificações → Grava fila-execucao-query
```

### **3. Execução das Queries**
```
Worker → Lê fila-execucao-query → Executa SQL → Grava fila-execucao-processo
```

### **4. Atualização do Status**
```
Worker → Lê fila-execucao-processo → Atualiza Execucao → Status: "Finalizada"
```

## 📊 **Exemplos de Uso**

### **Enviar Mensagem para fila-execucao-query**
```bash
aws sqs send-message \
  --queue-url "https://sqs.region.amazonaws.com/account/fila-execucao-query" \
  --message-body '{"execucaoVerificacaoId":"exec123#verif456","timestamp":"2024-12-19T10:30:00Z"}'
```

### **Enviar Mensagem para fila-execucao-processo**
```bash
# Sucesso
aws sqs send-message \
  --queue-url "https://sqs.region.amazonaws.com/account/fila-execucao-processo" \
  --message-body '{"execucaoVerificacaoId":"a1b2c3d4-e5f6-7890-abcd-ef1234567890","isSuccess":true,"resultado":"150 registros encontrados","timestamp":"2024-12-19T10:35:00Z"}'

# Erro
aws sqs send-message \
  --queue-url "https://sqs.region.amazonaws.com/account/fila-execucao-processo" \
  --message-body '{"execucaoVerificacaoId":"b2c3d4e5-f6g7-8901-bcde-f23456789012","isSuccess":false,"resultado":"Timeout na execução","timestamp":"2024-12-19T10:40:00Z"}'
```

## ⚠️ **Importante**

### **fila-execucao-query**
- **SEMPRE** grava mensagens leves (apenas IDs)
- **NUNCA** grava dados completos (SQL, parâmetros, etc.)
- Dados completos ficam na tabela `ExecucaoVerificacao`

### **fila-execucao-processo**
- **SEMPRE** lê mensagens com resultados
- **SEMPRE** atualiza a execução principal no DynamoDB
- **SEMPRE** sincroniza status e contadores

## 🎯 **Benefícios da Arquitetura**

1. **Mensagens Leves**: fila-execucao-query usa apenas IDs (100-200 bytes)
2. **Rastreabilidade**: fila-execucao-processo mantém status atualizado
3. **Performance**: Menos tráfego de rede nas filas
4. **Persistência**: Dados completos salvos no DynamoDB
5. **Sincronização**: Status sempre consistente entre filas e banco

Esta arquitetura garante que o sistema seja **eficiente** (mensagens leves), **rastreável** (status sempre atualizado) e **persistente** (dados salvos no DynamoDB).


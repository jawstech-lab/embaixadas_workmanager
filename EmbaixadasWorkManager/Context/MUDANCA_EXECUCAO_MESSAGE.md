# Mudança no ExecucaoMessage - Recebimento Apenas do ID

## Visão Geral

Esta mudança simplifica o fluxo de processamento de execuções. Ao invés de receber uma mensagem completa com todos os dados da execução e criar um novo processo, o sistema agora recebe apenas o ID da execução e busca o processo já existente na tabela Execucoes.

## Mudanças Implementadas

### 1. **ExecucaoMessage Simplificado**

#### **ANTES:**
```json
{
  "base": "NomeDaBase",
  "dataBase": "2024-01-01T00:00:00Z",
  "dataSolicitacao": "2024-01-01T00:00:00Z",
  "empresa": "NomeDaEmpresa",
  "usuario": "usuario.sistema",
  "validacoes": ["uuid-1", "uuid-2", "uuid-3"],
  "prioridade": 1,
  "maxTentativas": 3,
  "tags": ["tag1", "tag2"],
  "metadata": {
    "origem": "script-teste",
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

#### **DEPOIS:**
```json
{
  "execucaoId": "uuid-execucao-existente"
}
```

### 2. **Fluxo de Processamento Atualizado**

#### **ANTES:**
```
1. Recebe mensagem SQS completa
2. Valida campos obrigatórios (base, empresa, validacoes)
3. Cria nova execução com ToExecucao()
4. Salva execução no DynamoDB
5. Processa verificações
```

#### **DEPOIS:**
```
1. Recebe mensagem SQS com apenas execucaoId
2. Valida se execucaoId não é vazio
3. Busca execução existente no DynamoDB
4. Verifica se status é "Pendente"
5. Atualiza status para "EmProcessamento"
6. Processa verificações
```

## Arquivos Modificados

### 1. **Models/ExecucaoMessage.cs**
- Removidos todos os campos de dados da execução
- Removido método `ToExecucao()`
- Mantido apenas campo `execucaoId`

### 2. **Interfaces/IDynamoDbService.cs**
- Adicionado método `GetExecucaoAsync(string execucaoId)`

### 3. **Services/DynamoDbService.cs**
- Implementado método `GetExecucaoAsync()`

### 4. **Services/ProcessorService.cs**
- Modificado `ProcessExecucaoMessageAsync()` para buscar execução
- Atualizada validação para verificar apenas `execucaoId`
- Adicionada verificação de status da execução
- Alterado de `SaveExecucaoAsync()` para `UpdateAsync()`

### 5. **Scripts de Teste**
- **test-send-messages.ps1**: Modificado para enviar apenas IDs
- **create-test-execucoes.ps1**: Novo script para criar execuções de teste

## Benefícios da Mudança

### 1. **Separação de Responsabilidades**
- **Criação de execuções**: Responsabilidade de outro sistema
- **Processamento de execuções**: Responsabilidade do WorkManager
- **Dados da execução**: Centralizados na tabela Execucoes

### 2. **Simplicidade**
- Mensagens SQS menores e mais simples
- Menos validações de dados
- Processamento mais direto

### 3. **Consistência**
- Dados da execução ficam em um local (DynamoDB)
- Evita duplicação de informações
- Facilita manutenção e auditoria

### 4. **Flexibilidade**
- Execuções podem ser criadas por diferentes sistemas
- WorkManager foca apenas no processamento
- Facilita integração com outros sistemas

## Fluxo de Trabalho Atualizado

### **1. Criação de Execuções**
```bash
# Criar execuções de teste no DynamoDB
.\create-test-execucoes.ps1 -ExecucaoCount 5
```

### **2. Envio de Mensagens**
```bash
# Enviar mensagens com IDs das execuções
.\test-send-messages.ps1 -MessageCount 5
```

### **3. Processamento**
```bash
# Executar o Worker
dotnet run --environment Development
```

## Validações Implementadas

### **1. Validação da Mensagem**
- `execucaoId` não pode ser vazio ou nulo

### **2. Validação da Execução**
- Execução deve existir na tabela Execucoes
- Status deve ser "Pendente" para processamento

### **3. Tratamento de Erros**
- Execução não encontrada: Log de erro e retorno false
- Execução já processada: Log de warning e retorno true
- Falha na atualização: Log de erro e retorno false

## Scripts de Teste

### **create-test-execucoes.ps1**
- Cria execuções de teste no DynamoDB
- Gera dados aleatórios para base, empresa, usuário
- Cria verificações com GUIDs únicos
- Define status inicial como "Pendente"

### **test-send-messages.ps1**
- Envia mensagens SQS com apenas o ID da execução
- Gera GUIDs únicos para cada mensagem
- Mensagens são muito mais simples e leves

## Monitoramento e Logs

### **Logs de Processamento**
```
Processando mensagem de execução: {MessageId}
Execução encontrada: {ExecucaoId}
Status atual: Pendente
Execução processada com sucesso: {ExecucaoId}. Status: EmProcessamento
```

### **Logs de Erro**
```
Execução não encontrada no DynamoDB: {ExecucaoId}
Execução já foi processada: {ExecucaoId}. Status atual: {Status}
Falha ao atualizar execução no DynamoDB: {ExecucaoId}
```

## Próximos Passos

### **1. Testes**
- [ ] Testar criação de execuções com script
- [ ] Testar envio de mensagens simplificadas
- [ ] Verificar processamento correto
- [ ] Validar logs e monitoramento

### **2. Melhorias Futuras**
- [ ] Adicionar validação de formato do GUID
- [ ] Implementar retry para execuções não encontradas
- [ ] Adicionar métricas de execuções processadas
- [ ] Implementar cache de execuções frequentes

## Conclusão

Esta mudança simplifica significativamente o sistema, separando claramente as responsabilidades:

- **Criação de execuções**: Sistema externo
- **Processamento de execuções**: WorkManager
- **Armazenamento de dados**: DynamoDB centralizado

O sistema agora é mais robusto, simples e preparado para integração com outros sistemas que criam execuções.

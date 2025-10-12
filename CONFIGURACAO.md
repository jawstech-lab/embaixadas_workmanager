# 🔧 Configuração do Projeto

## 📋 **Pré-requisitos**

- .NET 8.0 SDK
- AWS CLI configurado
- Acesso à conta AWS (credenciais configuradas)

---

## 🚀 **Setup Inicial**

### **1. Clonar o Repositório**
```bash
git clone <url-do-repositorio>
cd embaixadas_workmanager
```

---

### **2. Configurar AWS Credentials**

Certifique-se de ter o AWS CLI configurado:
```bash
aws configure
```

Ou configure o arquivo `~/.aws/credentials`:
```ini
[default]
aws_access_key_id = YOUR_ACCESS_KEY
aws_secret_access_key = YOUR_SECRET_KEY
region = sa-east-1
```

---

### **3. Copiar e Configurar appsettings.json**

```bash
# Copiar exemplo
cp EmbaixadasWorkManager/appsettings.example.json EmbaixadasWorkManager/appsettings.json

# Editar com suas configurações
notepad EmbaixadasWorkManager/appsettings.json  # Windows
# ou
nano EmbaixadasWorkManager/appsettings.json     # Linux/Mac
```

**Campos obrigatórios para preencher**:
```json
{
  "SQS": {
    "QueueNameExecucao": "nome-da-fila-execucao",
    "QueueNameProcesso": "nome-da-fila-processo"
  },
  "AWS": {
    "Profile": "default",
    "Region": "sa-east-1"
  }
}
```

---

### **4. Criar Tabelas DynamoDB**

#### **Tabelas Principais**:
```bash
# Execute os scripts (crie eles localmente, não versione!)
# Ou crie manualmente via Console AWS
```

**Tabelas necessárias**:
- ✅ `Execucao` - Armazena execuções
- ✅ `Verificacao` - Armazena verificações
- ✅ `ExecucaoVerificacao` - Relacionamento
- ✅ `ExecucaoProcesso` - Consolidação de processos
- ✅ `ExecucaoResumoView` - Visão rápida por embaixada/empresa
- ✅ `ExecucaoEmpresaStatus` - Auditoria detalhada
- ✅ `Resultado` - Apontamentos de erro (com GSI_Agregacao)
- ✅ `ResultadoAgregado` - Agregações (com GSI_EMPRESA)

#### **Índices GSI Necessários**:

**Resultado**:
- `GSI_Agregacao`: 
  - PK: `GSI1_PK` (String)
  - SK: `GSI1_SK` (String)

**ResultadoAgregado**:
- `GSI_EMPRESA`:
  - PK: `GSI2_PK` (String)
  - SK: `GSI2_SK` (String)

---

### **5. Criar Filas SQS**

```bash
# Criar fila de execução
aws sqs create-queue --queue-name nome-da-fila-execucao --region sa-east-1

# Criar fila de processo
aws sqs create-queue --queue-name nome-da-fila-processo --region sa-east-1
```

**Configurações recomendadas**:
- VisibilityTimeout: 60 segundos
- MessageRetentionPeriod: 345600 (4 dias)
- ReceiveMessageWaitTimeSeconds: 20 (long polling)

---

## 🏃 **Executar o Projeto**

### **Modo Desenvolvimento**:
```bash
cd EmbaixadasWorkManager
dotnet run
```

### **Modo Produção**:
```bash
dotnet build -c Release
dotnet EmbaixadasWorkManager/bin/Release/net8.0/EmbaixadasWorkManager.dll
```

---

## 🧪 **Testar**

### **1. Enviar Mensagem de Teste**:
```bash
# Exemplo de mensagem para fila de execução
aws sqs send-message \
  --queue-url https://sqs.sa-east-1.amazonaws.com/123456789/nome-da-fila-execucao \
  --message-body '{
    "Id": "test-123",
    "Empresa": "MA,PI",
    "IdEmbaixadas": ["emb-001", "emb-002"]
  }' \
  --region sa-east-1
```

### **2. Verificar Logs**:
```
[INFO] Worker rodando em: 2025-10-12 00:00:00
[INFO] Buscando mensagens da fila...
[INFO] Processando execução: test-123
[INFO] 🗑️  INICIANDO LIMPEZA DE REGISTROS ANTIGOS
[INFO] ✅ LIMPEZA CONCLUÍDA - Total Deletados: 0
```

---

## 📂 **Estrutura de Configuração**

```
EmbaixadasWorkManager/
├── appsettings.json           ← SEU ARQUIVO (não versionado)
├── appsettings.example.json   ← EXEMPLO (versionado)
├── env.example                ← EXEMPLO (versionado)
└── *.ps1                      ← SCRIPTS (não versionados)
```

---

## ⚠️ **Arquivos Sensíveis**

**NÃO versione estes arquivos** (já estão no `.gitignore`):
- ✅ `appsettings.json`
- ✅ `appsettings.Development.json`
- ✅ `*.ps1` (scripts PowerShell)
- ✅ `Context/DEBUG_*.md`
- ✅ `Context/PROBLEMA_*.md`

**Pode versionar** (exemplos):
- ✅ `appsettings.example.json`
- ✅ `env.example`
- ✅ `*.example.ps1`

---

## 🐛 **Troubleshooting**

### **Erro: "The table does not have the specified index: GSI_EMPRESA"**
**Solução**: Criar o GSI manualmente no Console AWS DynamoDB:
1. Abra a tabela `ResultadoAgregado`
2. Vá em "Indexes" → "Create index"
3. Partition Key: `GSI2_PK` (String)
4. Sort Key: `GSI2_SK` (String)
5. Index Name: `GSI_EMPRESA`
6. Aguarde status = ACTIVE

### **Erro: "Queue does not exist"**
**Solução**: Verificar nome da fila no `appsettings.json` e se ela existe na AWS.

### **Erro: "Unable to get IAM security credentials"**
**Solução**: Configurar AWS credentials (`aws configure`).

---

## 📚 **Documentação Adicional**

- `Context/README.md` - Visão geral da arquitetura
- `Context/STYLE_GUIDE.md` - Guia de estilo de código
- `Context/ARQUITETURA_*.md` - Documentos técnicos
- `DOCUMENTATION.md` - Documentação completa

---

## 🚀 **Performance**

### **Configurações Recomendadas**:

**SQS**:
- `MaxNumberOfMessages`: 10 (buscar 10 mensagens por vez)
- `WaitTimeSeconds`: 20 (long polling - reduz custos)

**DynamoDB**:
- Modo **On-Demand** para facilitar (ou provisioned com auto-scaling)

**PostProcessing**:
- `AgregacaoResultados.BatchSize`: 25 (AWS BatchWriteItem limit)
- `AgregacaoResultados.TimeoutSeconds`: 120

---

## 📊 **Monitoramento**

### **CloudWatch Logs**:
- Grupo: `/aws/ecs/embaixadas-workmanager` (se usar ECS)
- Métricas: Mensagens processadas, erros, latência

### **CloudWatch Metrics**:
- SQS: ApproximateNumberOfMessages
- DynamoDB: ConsumedReadCapacityUnits, ConsumedWriteCapacityUnits

---

**Data**: 12/10/2025


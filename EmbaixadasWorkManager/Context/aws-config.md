# 🔧 Configuração AWS para Resolver Problema da Fila

## 🚨 Problema Identificado
A fila foi criada no AWS mas o sistema não consegue encontrá-la.

## 🔍 Possíveis Causas

### 1. **Região AWS Incorreta**
- Verificar se a fila está na mesma região configurada
- Região atual: `us-east-1`

### 2. **Nome da Fila vs URL da Fila**
- **Nome**: `fila-execucao` (nome lógico)
- **URL**: `https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao`

### 3. **Permissões IAM Insuficientes**
- Necessário: `sqs:ListQueues`, `sqs:GetQueueAttributes`

## 🛠️ Soluções

### **Solução 1: Verificar Região da Fila**
```bash
# No console AWS, verificar em qual região a fila foi criada
# Comparar com a configuração no appsettings.json
```

### **Solução 2: Usar URL Completa da Fila**
```json
{
  "SQS": {
    "FilaExecucao": "https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao",
    "FilaExecucaoQuery": "https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao-query"
  }
}
```

### **Solução 3: Verificar Permissões IAM**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sqs:ListQueues",
        "sqs:GetQueueAttributes",
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage"
      ],
      "Resource": "*"
    }
  ]
}
```

### **Solução 4: Configurar Credenciais AWS**
```bash
# Opção 1: AWS CLI Profile
aws configure --profile default

# Opção 2: Variáveis de Ambiente
export AWS_ACCESS_KEY_ID=sua_access_key
export AWS_SECRET_ACCESS_KEY=sua_secret_key
export AWS_DEFAULT_REGION=us-east-1
```

## 🧪 Teste de Conectividade

### **1. Verificar se o perfil AWS está funcionando:**
```bash
aws sts get-caller-identity --profile default
```

### **2. Listar filas SQS:**
```bash
aws sqs list-queues --region us-east-1 --profile default
```

### **3. Verificar atributos de uma fila específica:**
```bash
aws sqs get-queue-attributes \
  --queue-url https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao \
  --attribute-names All \
  --region us-east-1 \
  --profile default
```

## 📝 Configuração Recomendada

### **appsettings.json:**
```json
{
  "AWS": {
    "Region": "us-east-1",
    "UseProfile": true,
    "ProfileName": "default"
  },
  "SQS": {
    "FilaExecucao": "fila-execucao",
    "FilaExecucaoQuery": "fila-execucao-query"
  }
}
```

### **Ou usar URLs completas:**
```json
{
  "SQS": {
    "FilaExecucao": "https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao",
    "FilaExecucaoQuery": "https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao-query"
  }
}
```

## 🚀 Próximos Passos

1. **Verificar região da fila no console AWS**
2. **Confirmar permissões IAM**
3. **Testar conectividade com AWS CLI**
4. **Executar o projeto com logs detalhados**
5. **Verificar se as filas são encontradas**

## 📊 Logs Esperados

### **Sucesso:**
```
✓ Conectividade SQS OK. Total de filas encontradas: 2
Filas disponíveis:
  - https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao
  - https://sqs.us-east-1.amazonaws.com/123456789012/fila-execucao-query
Health Check - Fila Execução: ✓
Health Check - Fila Query: ✓
```

### **Falha:**
```
✗ Falha na conectividade básica SQS. Verifique permissões IAM.
```


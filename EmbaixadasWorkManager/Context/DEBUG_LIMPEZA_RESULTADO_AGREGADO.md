# Debug: Limpeza de ResultadoAgregado Não Funcionando

## ✅ **Problemas Identificados e Corrigidos**

### **1. Nome da Tabela Hardcoded** ❌
**Problema**: Código usava `"ResultadoAgregado"` em vez de configuração.
**Correção**: Alterado para `_dynamoConfig.TableNameResultadoAgregado`

**Arquivos Afetados**:
- `AgregacaoResultadosStep.cs` (3 ocorrências corrigidas)

---

### **2. Logs Insuficientes** ❌
**Problema**: Não havia logs detalhados para diagnosticar falhas.
**Correção**: Adicionados logs detalhados em cada etapa.

---

## 🔍 **Como Diagnosticar o Problema**

### **Passo 1: Verificar se o GSI_Empresa Existe**

Execute no AWS CLI:
```bash
aws dynamodb describe-table --table-name ResultadoAgregado --region sa-east-1
```

**Procure por**:
```json
{
  "GlobalSecondaryIndexes": [
    {
      "IndexName": "GSI_Empresa",
      "KeySchema": [
        {
          "AttributeName": "GSI2_PK",
          "KeyType": "HASH"
        },
        {
          "AttributeName": "GSI2_SK",
          "KeyType": "RANGE"
        }
      ],
      "IndexStatus": "ACTIVE"  ← Deve estar ACTIVE!
    }
  ]
}
```

**✅ Verificação**:
- [ ] GSI_Empresa existe?
- [ ] IndexStatus = "ACTIVE"?
- [ ] Atributos GSI2_PK e GSI2_SK corretos?

---

### **Passo 2: Verificar se GSI2_PK está Preenchido nos Registros**

Query de teste no DynamoDB:
```bash
aws dynamodb scan \
  --table-name ResultadoAgregado \
  --projection-expression "PK, SK, GSI2_PK, GSI2_SK" \
  --limit 5 \
  --region sa-east-1
```

**✅ Verificação**:
- [ ] GSI2_PK existe nos registros?
- [ ] Formato: `"GSI2_PK": "EMP#MA"` (ou outra sigla)?
- [ ] GSI2_SK existe?
- [ ] Formato: `"GSI2_SK": "EXEC#id#VER#id#..."`?

**❌ Se GSI2_PK NÃO existe ou está vazio**:
→ **PROBLEMA**: Registros antigos foram inseridos SEM o GSI2_PK!
→ **SOLUÇÃO**: Precisa reprocessar ou migrar dados.

---

### **Passo 3: Testar Query no GSI_Empresa**

```bash
# Substitua MA pela empresa que você quer testar
aws dynamodb query \
  --table-name ResultadoAgregado \
  --index-name GSI_Empresa \
  --key-condition-expression "GSI2_PK = :pk" \
  --expression-attribute-values '{":pk":{"S":"EMP#MA"}}' \
  --projection-expression "PK, SK" \
  --region sa-east-1
```

**✅ Verificação**:
- [ ] Query retorna registros?
- [ ] Se sim: GSI funciona ✅
- [ ] Se não: GSI não encontra registros ❌

**❌ Se não retorna registros**:
- **Motivo 1**: GSI2_PK não está preenchido nos registros
- **Motivo 2**: Formato incorreto (ex: `"MA"` em vez de `"EMP#MA"`)
- **Motivo 3**: GSI ainda está sendo criado (IndexStatus = CREATING)

---

### **Passo 4: Verificar Logs da Aplicação**

**Com as novas correções, os logs devem mostrar**:

#### **Log de Início de Limpeza**:
```
[INFO] Iniciando limpeza de registros antigos (Segmentados + Global). Empresas: MA, PI, Embaixadas: 2
```

#### **Log de Limpeza GLOBAL** (para cada empresa):
```
[INFO] Iniciando limpeza GLOBAL para empresa MA. GSI2_PK = EMP#MA
[DEBUG] Query GSI_Empresa: Table=ResultadoAgregado, Index=GSI_Empresa, GSI2_PK=EMP#MA
[INFO] Query GSI_Empresa retornou 5 registros para empresa MA
[INFO] Empresa MA (GLOBAL): 5 registros encontrados, 2 filtrados para GLOBAL
[INFO] Iniciando delecao de 2 registros em lotes de 25 (GLOBAL:MA)
[DEBUG] Executando BatchWriteItem com 2 deletes. Batch 1/1 (GLOBAL:MA)
[INFO] Batch de 2 registros deletados com sucesso (GLOBAL:MA)
[INFO] Delecao concluida. 2 registros deletados (GLOBAL:MA)
```

#### **Log de Limpeza SEGMENTADA** (para cada empresa):
```
[INFO] Iniciando limpeza SEGMENTADA para empresa MA. GSI2_PK = EMP#MA
[DEBUG] Query GSI_Empresa SEGMENTADO: Table=ResultadoAgregado, Index=GSI_Empresa, GSI2_PK=EMP#MA
[INFO] Query GSI_Empresa retornou 5 registros totais para empresa MA
[INFO] Empresa MA (SEGMENTADOS): 5 registros encontrados, 3 filtrados para SEGMENTADOS
[INFO] Iniciando delecao de 3 registros em lotes de 25 (SEGMENTADO:MA)
[DEBUG] Executando BatchWriteItem com 3 deletes. Batch 1/1 (SEGMENTADO:MA)
[INFO] Batch de 3 registros deletados com sucesso (SEGMENTADO:MA)
[INFO] Delecao concluida. 3 registros deletados (SEGMENTADO:MA)
```

#### **Log Final**:
```
[INFO] Limpeza concluida. 5 registros deletados. Empresas: MA, PI
```

---

### **Passo 5: Analisar Possíveis Problemas nos Logs**

#### **❌ Problema 1: Query retorna 0 registros**
```
[INFO] Query GSI_Empresa retornou 0 registros para empresa MA
[DEBUG] Nenhum registro encontrado para empresa MA (GLOBAL)
```

**Diagnóstico**:
- GSI2_PK não está preenchido nos registros antigos
- OU registros não existem para esta empresa
- OU formato incorreto do GSI2_PK

**Solução**:
1. Verificar registros manualmente (Passo 2)
2. Se GSI2_PK estiver vazio: Reprocessar ou migrar dados

---

#### **❌ Problema 2: Erro ao executar Query**
```
[ERROR] Erro ao limpar registros globais da empresa MA
System.Exception: ValidationException: The table does not have the specified index: GSI_Empresa
```

**Diagnóstico**:
- GSI_Empresa NÃO existe ou nome está incorreto

**Solução**:
1. Criar o GSI (ver script abaixo)
2. Aguardar status = ACTIVE

---

#### **❌ Problema 3: BatchWriteItem falha**
```
[ERROR] Erro ao deletar batch de 25 registros (GLOBAL:MA)
Amazon.DynamoDBv2.Model.ProvisionedThroughputExceededException
```

**Diagnóstico**:
- Capacidade do GSI ou tabela esgotada

**Solução**:
1. Aumentar capacidade (ReadCapacityUnits/WriteCapacityUnits)
2. OU ativar modo on-demand

---

#### **❌ Problema 4: Itens não processados**
```
[WARNING] BatchWriteItem tem 5 itens nao processados (GLOBAL:MA)
```

**Diagnóstico**:
- Throttling ou limite de throughput
- Algumas chaves podem não existir

**Solução**:
1. Implementar retry para UnprocessedItems (próxima melhoria)
2. Aumentar capacidade

---

## 🛠️ **Soluções Rápidas**

### **Solução 1: Criar GSI_Empresa** (se não existe)

Execute o script que já existe:
```bash
./EmbaixadasWorkManager/create-gsi-empresa-resultado-agregado.ps1
```

OU manualmente:
```bash
aws dynamodb update-table \
  --table-name ResultadoAgregado \
  --attribute-definitions \
    AttributeName=GSI2_PK,AttributeType=S \
    AttributeName=GSI2_SK,AttributeType=S \
  --global-secondary-index-updates \
    "[{
      \"Create\": {
        \"IndexName\": \"GSI_Empresa\",
        \"KeySchema\": [
          {\"AttributeName\": \"GSI2_PK\", \"KeyType\": \"HASH\"},
          {\"AttributeName\": \"GSI2_SK\", \"KeyType\": \"RANGE\"}
        ],
        \"Projection\": {\"ProjectionType\": \"ALL\"},
        \"ProvisionedThroughput\": {
          \"ReadCapacityUnits\": 5,
          \"WriteCapacityUnits\": 5
        }
      }
    }]" \
  --region sa-east-1
```

**Aguarde alguns minutos** para IndexStatus = ACTIVE.

---

### **Solução 2: Verificar e Corrigir Registros Sem GSI2_PK**

**Script Python para verificar**:
```python
import boto3

dynamodb = boto3.client('dynamodb', region_name='sa-east-1')

# Scan para encontrar registros sem GSI2_PK
response = dynamodb.scan(
    TableName='ResultadoAgregado',
    FilterExpression='attribute_not_exists(GSI2_PK)',
    ProjectionExpression='PK, SK, Empresa'
)

print(f"Registros SEM GSI2_PK: {len(response['Items'])}")
for item in response['Items'][:10]:  # Mostrar 10 primeiros
    print(f"  PK: {item['PK']['S']}, Empresa: {item.get('Empresa', {}).get('S', 'N/A')}")
```

**Se encontrar registros SEM GSI2_PK**:
→ Esses registros foram inseridos com código antigo
→ Você tem 3 opções:

**Opção A: Deletar registros antigos manualmente**
```bash
# Para cada registro, execute:
aws dynamodb delete-item \
  --table-name ResultadoAgregado \
  --key '{"PK":{"S":"EXEC#abc"},"SK":{"S":"EMP#MA#..."}}' \
  --region sa-east-1
```

**Opção B: Atualizar registros com GSI2_PK**
```python
# Para cada item:
dynamodb.update_item(
    TableName='ResultadoAgregado',
    Key={'PK': {'S': pk}, 'SK': {'S': sk}},
    UpdateExpression='SET GSI2_PK = :gsi_pk, GSI2_SK = :gsi_sk',
    ExpressionAttributeValues={
        ':gsi_pk': {'S': f"EMP#{empresa}"},
        ':gsi_sk': {'S': f"EXEC#{exec_id}#VER#{verif_id}#..."}
    }
)
```

**Opção C: Reprocessar execução** (Melhor!)
→ Reenviar mensagem de execução para fila
→ Sistema irá deletar (via Scan se GSI não funcionar) e reinserir com GSI2_PK correto

---

### **Solução 3: Ativar Modo On-Demand** (Para evitar throttling)

```bash
aws dynamodb update-table \
  --table-name ResultadoAgregado \
  --billing-mode PAY_PER_REQUEST \
  --region sa-east-1
```

---

## 📋 **Checklist de Verificação**

Antes de rodar a aplicação novamente:

- [ ] **1. GSI_Empresa existe e está ACTIVE**
- [ ] **2. Registros têm GSI2_PK e GSI2_SK preenchidos**
- [ ] **3. Query de teste no GSI retorna registros**
- [ ] **4. Código compilou com sucesso (Build succeeded)**
- [ ] **5. Aplicação foi reiniciada com novo código**
- [ ] **6. Log Level está em "Debug" ou "Information"**
- [ ] **7. Verificar appsettings.json: `TableNameResultadoAgregado = "ResultadoAgregado"`**

---

## 🎯 **Teste Completo**

### **1. Preparação**:
```bash
# Inserir um registro de teste
aws dynamodb put-item \
  --table-name ResultadoAgregado \
  --item '{
    "PK": {"S": "EXEC#test-123#EMB#GLOBAL"},
    "SK": {"S": "EMP#MA#VER#v1#PIP#TEST#001#ERRO"},
    "GSI2_PK": {"S": "EMP#MA"},
    "GSI2_SK": {"S": "EXEC#test-123#VER#v1#PIP#TEST#001"},
    "QTD": {"N": "99"},
    "Empresa": {"S": "MA"}
  }' \
  --region sa-east-1
```

### **2. Verificar se foi inserido**:
```bash
aws dynamodb query \
  --table-name ResultadoAgregado \
  --index-name GSI_Empresa \
  --key-condition-expression "GSI2_PK = :pk" \
  --expression-attribute-values '{":pk":{"S":"EMP#MA"}}' \
  --region sa-east-1
```

### **3. Executar aplicação com empresa MA**:
- Enviar mensagem de execução solicitando empresa MA
- Verificar logs (devem mostrar "Query GSI_Empresa retornou 1 registro")
- Verificar se deletou o registro de teste

### **4. Confirmar deleção**:
```bash
# Deve retornar 0 itens
aws dynamodb query \
  --table-name ResultadoAgregado \
  --index-name GSI_Empresa \
  --key-condition-expression "GSI2_PK = :pk" \
  --expression-attribute-values '{":pk":{"S":"EMP#MA"}}' \
  --region sa-east-1
```

---

## 📝 **Resumo das Mudanças Aplicadas**

### **Código Corrigido**:
1. ✅ `TableName` usa `_dynamoConfig.TableNameResultadoAgregado` (3 locais)
2. ✅ Logs detalhados em cada etapa da limpeza
3. ✅ Verificação de `response.Items` antes de usar
4. ✅ Logs de erro com contexto completo
5. ✅ Verificação de `UnprocessedItems` no BatchWriteItem
6. ✅ Logs de progresso de batch (X/Y batches)

### **Novos Logs Adicionados**:
- Início de limpeza (GLOBAL e SEGMENTADO) por empresa
- Query GSI com parâmetros
- Contagem de registros retornados
- Contagem de registros filtrados
- Início de deleção com total
- Progresso de batches
- Sucesso/erro de cada batch
- Total deletado por contexto

---

## 🚀 **Próximos Passos**

1. **Parar a aplicação**
2. **Verificar GSI existe e está ACTIVE** (Passo 1)
3. **Verificar registros têm GSI2_PK** (Passo 2)
4. **Reiniciar aplicação com novo código**
5. **Processar uma execução**
6. **Analisar logs detalhados** (Passo 4)
7. **Reportar qual log apareceu** (se problema persistir)

---

**Data**: 11/10/2025
**Versão**: 2.0 (com logs detalhados)


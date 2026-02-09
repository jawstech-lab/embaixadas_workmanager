# Plano de Migração para Produção - Mudança de Nomes de Recursos

## Visão Geral

Este documento detalha o plano de migração dos nomes de recursos AWS (DynamoDB, SQS) do ambiente atual para os novos nomes padronizados de produção.

## Recursos Identificados no Código

### 1. Tabelas DynamoDB

O sistema utiliza as seguintes tabelas DynamoDB configuráveis via `appsettings.json`:

| Nome Atual (Configurável) | Nome Novo (Produção) | Localização no Código |
|---------------------------|----------------------|-----------------------|
| `Execucoes` | `dynamo-embaixadas-execucoes-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameExecucao)<br>`Models/Execucao.cs` (hardcoded) |
| `Verificacoes` | `dynamo-embaixadas-verificacoes-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameVerificacao)<br>`Models/Verificacao.cs` (hardcoded) |
| `Consultas` | `dynamo-embaixadas-consultas-devqa-eqtl-bdgd-us-east-1` | `Models/Consulta.cs` (hardcoded) |
| `Parametros` | `dynamo-embaixadas-parametros-devqa-eqtl-bdgd-us-east-1` | `Models/Parametro.cs` (hardcoded) |
| `ExecucaoVerificacao` | `dynamo-embaixadas-execucao-verificacao-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameExecucaoVerificacao)<br>`Models/ExecucaoVerificacao.cs` (hardcoded) |
| `ExecucaoResumoView` | `dynamo-embaixadas-execucao-resumo-view-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameExecucaoResumoView)<br>`Models/ExecucaoResumoView.cs` (hardcoded) |
| `ExecucaoEmpresaStatus` | `dynamo-embaixadas-execucao-empresa-status-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameExecucaoEmpresaStatus)<br>`Models/ExecucaoEmpresaStatus.cs` (hardcoded) |
| `Resultado` | `dynamo-embaixadas-resultado-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameResultado)<br>`Models/Resultado.cs` (hardcoded)<br>`AgregacaoResultadosStep.cs` (hardcoded linha 138) |
| `ResultadoAgregado` | `dynamo-embaixadas-resultado-agregado-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameResultadoAgregado)<br>`Models/ResultadoAgregado.cs` (hardcoded) |
| `Justificativas` | `dynamo-embaixadas-justificativa-devqa-eqtl-bdgd-us-east-1` | `AwsConfiguration.cs` (TableNameJustificativa)<br>`Models/Justificativa.cs` (hardcoded) |

**Tabelas NÃO usadas diretamente pelo WorkManager (mas mencionadas no depara):**
- `ConsultaParametro` - Não encontrada no código
- `Embaixadas` - Não encontrada no código
- `ExecucaoProcesso` - **CONSOLIDADA** (não mais utilizada)
- `StatusGruposApontamentos` - Não encontrada no código

### 2. Filas SQS

O sistema utiliza as seguintes filas SQS configuráveis via `appsettings.json`:

| Nome Atual | Nome Novo | Localização |
|------------|-----------|-------------|
| `fila-execucao-dev` | `sqs-embaixadas-execucao-dlq-devqa-etl-bdgd-us-east-1` | `AwsConfiguration.cs` (FilaExecucao)<br>`appsettings.json` |
| `fila-execucao-query-dev` | `sqs-embaixadas-execucao-query-devqa-etl-bdgd-us-east-1` | `AwsConfiguration.cs` (FilaExecucaoQuery)<br>`appsettings.json` |
| `fila-execucao-processo-dev` | `sqs-embaixadas-execucao-processo-devqa-etl-bdgd-us-east-1` | `AwsConfiguration.cs` (FilaExecucaoProcesso)<br>`appsettings.json` |

**Filas DLQ mencionadas no depara:**
- `fila-execucao-processo-dev-dlq` → `sqs-embaixadas-execucao-processo-dlq-devqa-etl-bdgd-us-east-1` - Não configurada no código atual

## Problemas Identificados

### 1. Nomes Hardcoded nos Modelos

Os atributos `[DynamoDBTable("Nome")]` nos modelos estão hardcoded e **NÃO** respeitam a configuração do `appsettings.json`. Isso é um problema porque:

- O DynamoDBContext do AWS SDK usa o nome do atributo diretamente
- Mesmo configurando no `appsettings.json`, o modelo sempre usará o nome hardcoded

**Solução necessária:** Remover os atributos hardcoded e usar o nome da tabela via configuração ou contexto.

### 2. Nome Hardcoded no AgregacaoResultadosStep

Na linha 138 do `AgregacaoResultadosStep.cs`, há um nome hardcoded:
```csharp
TableName = "Resultado",  // ← HARDCODED!
```

**Solução:** Usar `_dynamoConfig.TableNameResultado` como já está sendo feito em outras partes do código.

## Plano de Ação

### Fase 1: Preparação (Antes da Migração)

#### 1.1. Remover Hardcoded dos Modelos DynamoDB

**Arquivos a modificar:**
- `Models/Execucao.cs` - Remover `[DynamoDBTable("Execucoes")]`
- `Models/Verificacao.cs` - Remover `[DynamoDBTable("Verificacoes")]`
- `Models/Consulta.cs` - Remover `[DynamoDBTable("Consultas")]`
- `Models/Parametro.cs` - Remover `[DynamoDBTable("Parametros")]`
- `Models/ExecucaoVerificacao.cs` - Remover `[DynamoDBTable("ExecucaoVerificacao")]`
- `Models/ExecucaoResumoView.cs` - Remover `[DynamoDBTable("ExecucaoResumoView")]`
- `Models/ExecucaoEmpresaStatus.cs` - Remover `[DynamoDBTable("ExecucaoEmpresaStatus")]`
- `Models/Resultado.cs` - Remover `[DynamoDBTable("Resultado")]`
- `Models/ResultadoAgregado.cs` - Remover `[DynamoDBTable("ResultadoAgregado")]`
- `Models/Justificativa.cs` - Remover `[DynamoDBTable("Justificativas")]`

**Alternativa:** Manter os atributos mas torná-los dinâmicos via `DynamoDBContext` configurado com nome de tabela.

#### 1.2. Corrigir Hardcoded no AgregacaoResultadosStep

**Arquivo:** `Services/PostProcessing/Steps/AgregacaoResultadosStep.cs`

**Mudança necessária:**
```csharp
// ANTES (linha 138)
TableName = "Resultado",

// DEPOIS
TableName = _dynamoConfig.TableNameResultado,
```

#### 1.3. ~~Adicionar Configuração para Todas as Tabelas~~ ⚠️ **NÃO NECESSÁRIO POR ENQUANTO**

**Observação:** Este passo seria necessário apenas se implementássemos mapeamento dinâmico de tabelas.

**Por que não é necessário agora:**
- As tabelas `Consulta` e `Parametro` são acessadas via `DynamoDBContext` que usa os atributos `[DynamoDBTable]` hardcoded
- Adicionar propriedades `TableNameConsulta` e `TableNameParametro` no `AwsConfiguration.cs` não resolveria o problema porque essas propriedades não seriam usadas
- O `DynamoDBContext` ignora a configuração e usa diretamente o nome do atributo

**Quando seria necessário:**
- Se implementarmos um wrapper ou serviço que substitua os nomes hardcoded antes de usar o `DynamoDBContext`
- Se migrarmos para usar `IAmazonDynamoDB` diretamente em vez de `DynamoDBContext` para essas tabelas

### Fase 2: Atualização dos Arquivos de Configuração

#### 2.1. Criar `appsettings.Production.json`

**Conteúdo sugerido:**
```json
{
  "AWS": {
    "Region": "us-east-1",
    "UseProfile": false,
    "ProfileName": "default",
    "AccessKey": "",
    "SecretKey": "",
    "ServiceUrl": "",
    "UseLocalStack": false
  },
  "SQS": {
    "FilaExecucao": "sqs-embaixadas-execucao-dlq-devqa-etl-bdgd-us-east-1",
    "FilaExecucaoQuery": "sqs-embaixadas-execucao-query-devqa-etl-bdgd-us-east-1",
    "FilaExecucaoProcesso": "sqs-embaixadas-execucao-processo-devqa-etl-bdgd-us-east-1",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20,
    "VisibilityTimeoutSeconds": 300
  },
  "DynamoDB": {
    "TableNameExecucao": "dynamo-embaixadas-execucoes-devqa-eqtl-bdgd-us-east-1",
    "TableNameVerificacao": "dynamo-embaixadas-verificacoes-devqa-eqtl-bdgd-us-east-1",
    "TableNameConsulta": "dynamo-embaixadas-consultas-devqa-eqtl-bdgd-us-east-1",
    "TableNameParametro": "dynamo-embaixadas-parametros-devqa-eqtl-bdgd-us-east-1",
    "TableNameExecucaoVerificacao": "dynamo-embaixadas-execucao-verificacao-devqa-eqtl-bdgd-us-east-1",
    "TableNameExecucaoResumoView": "dynamo-embaixadas-execucao-resumo-view-devqa-eqtl-bdgd-us-east-1",
    "TableNameExecucaoEmpresaStatus": "dynamo-embaixadas-execucao-empresa-status-devqa-eqtl-bdgd-us-east-1",
    "TableNameResultado": "dynamo-embaixadas-resultado-devqa-eqtl-bdgd-us-east-1",
    "TableNameResultadoAgregado": "dynamo-embaixadas-resultado-agregado-devqa-eqtl-bdgd-us-east-1",
    "TableNameJustificativa": "dynamo-embaixadas-justificativa-devqa-eqtl-bdgd-us-east-1",
    "ServiceUrl": "",
    "UseLocalStack": false
  }
}
```

#### 2.2. Atualizar `appsettings.example.json`

Atualizar o exemplo para refletir a estrutura completa e adicionar comentários explicativos.

### Fase 3: Implementação Técnica

#### 3.1. Modificar DynamoDBContext para Usar Nomes Configurados

**Desafio:** O `DynamoDBContext` do AWS SDK usa o nome do atributo `[DynamoDBTable]` diretamente.

**Soluções possíveis:**

**Opção A:** Usar `DynamoDBContext` com configuração de nome de tabela por tipo (complexo)

**Opção B:** Criar wrapper customizado que substitui o nome da tabela antes de fazer operações (recomendado)

**Opção C:** Usar `IAmazonDynamoDB` diretamente com nomes configurados (já usado em alguns lugares)

**Opção D:** Manter atributos `[DynamoDBTable]` mas usar um valor genérico e sobrescrever via configuração do contexto

#### 3.2. Implementar Mapeamento Dinâmico de Tabelas

Criar um serviço ou configuração que mapeie tipos de modelo para nomes de tabela configuráveis.

**Exemplo:**
```csharp
public class TableNameMapper
{
    private readonly Dictionary<Type, string> _tableNames;
    
    public TableNameMapper(DynamoDbConfiguration config)
    {
        _tableNames = new Dictionary<Type, string>
        {
            { typeof(Execucao), config.TableNameExecucao },
            { typeof(Verificacao), config.TableNameVerificacao },
            { typeof(Consulta), config.TableNameConsulta },
            // ... etc
        };
    }
    
    public string GetTableName<T>() => _tableNames[typeof(T)];
}
```

### Fase 4: Testes

#### 4.1. Testes Unitários
- Testar mapeamento de nomes de tabela
- Testar configuração via `appsettings.json`
- Testar fallback para valores padrão

#### 4.2. Testes de Integração
- Testar conexão com DynamoDB usando novos nomes
- Testar operações CRUD em todas as tabelas
- Testar filas SQS com novos nomes

#### 4.3. Testes de Ambiente
- Testar em ambiente de desenvolvimento/staging
- Validar que todos os recursos existem com novos nomes
- Testar migração gradual (compatibilidade com nomes antigos)

### Fase 5: Documentação

#### 5.1. Atualizar Documentação
- Atualizar `appsettings.example.json` com novos nomes
- Atualizar `README.md` com instruções de configuração
- Criar guia de migração para produção

#### 5.2. Scripts de Validação
- Script para validar existência de todas as tabelas
- Script para validar existência de todas as filas
- Script para testar conectividade

## Checklist de Migração

### Pré-Migração
- [ ] Backup de todas as configurações atuais
- [ ] Verificar existência de todos os recursos com novos nomes na AWS
- [ ] Criar novos recursos na AWS se necessário
- [ ] Validar permissões de acesso aos novos recursos
- [ ] Documentar plano de rollback

### Implementação
- [ ] Remover hardcoded dos modelos DynamoDB
- [ ] Corrigir hardcoded no AgregacaoResultadosStep
- [ ] Adicionar configurações faltantes em AwsConfiguration
- [ ] Criar appsettings.Production.json
- [ ] Implementar mapeamento dinâmico de tabelas
- [ ] Atualizar DynamoDbService para usar nomes configurados

### Testes
- [ ] Testes unitários passando
- [ ] Testes de integração passando
- [ ] Teste em ambiente de staging
- [ ] Validação manual de todas as operações

### Deploy
- [ ] Deploy em ambiente de produção
- [ ] Validação pós-deploy
- [ ] Monitoramento de logs
- [ ] Validação de performance

### Pós-Deploy
- [ ] Documentar mudanças aplicadas
- [ ] Remover recursos antigos (se aplicável)
- [ ] Atualizar documentação final

## Observações Importantes

### 1. Nome da Fila Execucao

**ATENÇÃO:** No depara, o nome da fila `fila-execucao-dev` mapeia para `sqs-embaixadas-execucao-dlq-devqa-etl-bdgd-us-east-1`, que parece ser uma **DLQ (Dead Letter Queue)**.

**Pergunta:** Este é o nome correto da fila principal ou é apenas a DLQ? A fila principal pode ter um nome diferente.

### 2. Tabelas Não Usadas

As seguintes tabelas do depara não foram encontradas no código:
- `ConsultaParametro` - Pode ser usada por outro serviço
- `Embaixadas` - Pode ser usada por outro serviço
- `StatusGruposApontamentos` - Pode ser usada por outro serviço

**Ação:** Validar se essas tabelas são realmente necessárias ou se são usadas por outros serviços.

### 3. ExecucaoProcesso

Esta tabela foi **CONSOLIDADA** e não é mais usada. Confirmar se ela ainda existe no ambiente de produção ou se já foi removida.

### 4. Compatibilidade Retrocompatível

**Recomendação:** Manter suporte para nomes antigos por um período de transição, validando ambos os nomes antes de acessar recursos.

## Próximos Passos

1. **Validar nomes corretos** com o time de infraestrutura
2. **Confirmar existência de recursos** na AWS antes da migração
3. **Implementar mudanças** seguindo o plano acima
4. **Testar em ambiente de staging** antes de produção
5. **Documentar e comunicar** mudanças ao time

## Contatos

- **Infraestrutura:** Validar nomes de recursos e existência
- **DevOps:** Ajudar com deploy e rollback
- **Desenvolvedores:** Implementar mudanças conforme plano

---

**Data de Criação:** [Data Atual]  
**Última Atualização:** [Data Atual]  
**Status:** 🔄 Em Análise


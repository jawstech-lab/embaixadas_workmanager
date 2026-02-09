# 📚 Documentação Técnica - Embaixadas Work Manager

## Componente: Work Manager (Worker/Processador)

---

## 1. Visão Geral e Topologia

### 1.1. Papel no Fluxo Global

O **Embaixadas Work Manager** é um componente **Worker Service** responsável pelo processamento assíncrono de verificações de dados de embaixadas. Ele atua como um **processador distribuído** que:

- ✅ Recebe mensagens de execução via **AWS SQS**
- ✅ Processa verificações de dados em lote
- ✅ Executa queries SQL parametrizadas contra bancos PostgreSQL
- ✅ Agrega e consolida resultados de apontamentos de erro
- ✅ Mantém histórico e auditoria de processos no **DynamoDB**
- ✅ Fornece visões otimizadas para consulta (segmentada e global)

### 1.2. Arquitetura de Topologia

```
┌─────────────────┐
│   Front End     │
│   (Back End)    │
└────────┬────────┘
         │ Envia mensagem SQS
         ▼
┌─────────────────────────────────────────────────────┐
│         AWS SQS Queue: fila-execucao-dev            │
│         (Mensagem: ID da Execução)                  │
└────────┬────────────────────────────────────────────┘
         │
         │ Worker Service
         ▼
┌─────────────────────────────────────────────────────┐
│         Embaixadas Work Manager                     │
│  ┌──────────────────────────────────────────────┐  │
│  │  MessageProcessorService                     │  │
│  │  • Recebe mensagens SQS                      │  │
│  │  • Long polling (20s)                        │  │
│  │  • Até 10 mensagens por lote                 │  │
│  └──────────────────────────────────────────────┘  │
│                      │                               │
│                      ▼                               │
│  ┌──────────────────────────────────────────────┐  │
│  │  ProcessorService                            │  │
│  │  • Processa Execução                         │  │
│  │  • Separa verificações                       │  │
│  │  • Envia para fila de processo               │  │
│  └──────────────────────────────────────────────┘  │
│                      │                               │
│                      ▼                               │
│  ┌──────────────────────────────────────────────┐  │
│  │  VerificacaoProcessorService                 │  │
│  │  • Processa cada verificação                 │  │
│  │  • Executa queries SQL                       │  │
│  │  • Salva resultados no DynamoDB              │  │
│  └──────────────────────────────────────────────┘  │
│                      │                               │
│                      ▼                               │
│  ┌──────────────────────────────────────────────┐  │
│  │  PostProcessingPipeline                      │  │
│  │  • AgregacaoResultadosStep                   │  │
│  │  • ProcessamentoJustificativasStep           │  │
│  │  • Limpeza e consolidação                    │  │
│  └──────────────────────────────────────────────┘  │
└────────┬────────────────────────────────────────────┘
         │
         │ Persistência / Consultas
         ▼
┌─────────────────────────────────────────────────────┐
│              AWS DynamoDB Tables                    │
│  • Execucao                                         │
│  • Verificacao                                      │
│  • ExecucaoVerificacao                              │
│  • Resultado                                        │
│  • ResultadoAgregado                                │
│  • ExecucaoResumoView                               │
│  • ExecucaoEmpresaStatus                            │
│  • Justificativas                                   │
└─────────────────────────────────────────────────────┘
         │
         │ Retorno de resultados
         ▼
┌─────────────────┐
│   Front End     │
│   (Consulta)    │
└─────────────────┘
```

### 1.3. Dependências de Infraestrutura

#### **1.3.1. AWS Services**

| Serviço | Configuração | Descrição |
|---------|-------------|-----------|
| **AWS SQS** | `fila-execucao-dev`<br>`fila-execucao-processo-dev`<br>`fila-execucao-query-dev` | Filas de mensagens para processamento assíncrono |
| **AWS DynamoDB** | 9 tabelas configuráveis | Armazenamento de dados estruturados |
| **AWS IAM** | Roles/permissões | Autenticação e autorização |

#### **1.3.2. Portas e Protocolos**

| Item | Valor | Descrição |
|------|-------|-----------|
| **HTTP API** | Porta configurável (padrão: 5000/5001) | API REST para monitoramento de logs |
| **Protocolo** | HTTP/HTTPS | Comunicação com endpoints |
| **SQS Protocol** | AWS SDK .NET | Long polling, mensagens em batch |

#### **1.3.3. Serviços Externos**

| Serviço | Tipo | Descrição |
|---------|------|-----------|
| **PostgreSQL** | Banco de dados externo | Execução de queries SQL para verificação de dados |
| **AWS LocalStack** | Opcional | Ambiente local para testes (desenvolvimento) |

### 1.4. Comunicação com Outros Componentes

#### **1.4.1. Front End ↔ Work Manager**

**Direção: Front End → Work Manager**
- **Mecanismo**: Mensagem SQS (`fila-execucao-dev`)
- **Formato**: String simples contendo o ID da execução
- **Exemplo**: `"99a03893-c760-4c82-8780-2d66a462ec55"`

**Direção: Work Manager → Front End**
- **Mecanismo**: DynamoDB (consulta direta)
- **Tabelas**: `Execucao`, `ExecucaoResumoView`, `ResultadoAgregado`
- **Protocolo**: Consulta direta via AWS SDK

#### **1.4.2. Back End ↔ Work Manager**

**Direção: Back End → Work Manager**
- **Mecanismo**: Mensagem SQS (`fila-execucao-dev`)
- **Payload**: ID da execução criada no DynamoDB
- **Ação**: Work Manager processa e atualiza status

**Direção: Work Manager → Back End**
- **Mecanismo**: Atualização em DynamoDB (`Execucao`)
- **Status**: `Pendente` → `AguardandoProcessamento` → `FinalizadaComSucesso` / `FinalizadaComErro`

#### **1.4.3. Workflow ↔ Work Manager**

**Direção: Workflow → Work Manager**
- **Mecanismo**: Mensagem SQS (`fila-execucao-processo-dev`)
- **Payload**: `{ "execucaoId": "...", "isSuccess": true }`
- **Ação**: Atualização de contadores de processamento

**Direção: Work Manager → Workflow**
- **Mecanismo**: Mensagem SQS (`fila-execucao-query-dev`) - *Opcional*
- **Payload**: Queries SQL parametrizadas para execução externa

#### **1.4.4. Processadores ↔ Work Manager**

**Direção: Processadores → Work Manager**
- **Mecanismo**: DynamoDB (`Resultado` table)
- **Ação**: Processadores externos salvam apontamentos diretamente no DynamoDB

**Direção: Work Manager → Processadores**
- **Mecanismo**: Pós-processamento automático
- **Ação**: Agregação e limpeza de dados após processamento

---

## 2. Especificação Técnica (Campos e Dados)

### 2.1. Entidades Principais (DynamoDB)

#### **2.1.1. Execucao**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `Id` | String (HashKey) | Sim | UUID da execução |
| `Base` | String | Sim | Identificador da base de dados |
| `DataBase` | DateTime | Sim | Data base para verificação |
| `DataSolicitacao` | DateTime | Sim | Data/hora da solicitação |
| `Empresa` | String | Sim | Siglas das empresas (ex: "MA,PI,RS") |
| `Validacoes` | List\<String\> | Sim | Lista de IDs de verificações |
| `IdEmbaixadas` | List\<String\> | Sim | Lista de IDs das embaixadas |
| `Usuario` | String | Sim | Usuário que solicitou |
| `Status` | String | Sim | Status: `Pendente`, `AguardandoProcessamento`, `FinalizadaComSucesso`, `FinalizadaComErro` |
| `DataInicio` | DateTime? | Não | Data/hora de início do processamento |
| `DataFim` | DateTime? | Não | Data/hora de fim do processamento |
| `Erro` | String? | Não | Mensagem de erro (se houver) |
| `Erros` | List\<ErroExecucao\> | Não | Lista detalhada de erros |
| `Resultado` | String? | Não | Mensagem de resultado |
| `QuantidadeVerificacoes` | Int | Sim | Total de verificações a processar |
| `VerificacoesProcessadas` | Int | Sim | Contador de verificações processadas |
| `VerificacoesComErro` | Int | Sim | Contador de verificações com erro |
| `DataInicioProcessamento` | DateTime? | Não | Data/hora de início do processamento |
| `ParametrosExecucao` | List\<ParametroExecucao\> | Não | Parâmetros específicos da execução |
| `TotalApontamentos` | Int | Sim | Total de apontamentos encontrados |

#### **2.1.2. Verificacao**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `Id` | String (HashKey) | Sim | UUID da verificação |
| `Nome` | String | Sim | Nome da verificação (ex: "RS_APONTAMENTO") |
| `Identificador` | String | Sim | Identificador único |
| `Descricao` | String | Não | Descrição da verificação |
| `ConsultaId` | String | Sim | ID da consulta SQL associada |
| `Nivel` | Int | Sim | Nível de criticidade (1-5) |
| `Ativo` | Boolean | Sim | Se a verificação está ativa |

#### **2.1.3. ExecucaoVerificacao**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `Id` | String (HashKey) | Sim | UUID composto: `{ExecucaoId}#{VerificacaoId}` |
| `ExecucaoId` | String | Sim | ID da execução |
| `VerificacaoId` | String | Sim | ID da verificação |
| `Status` | String | Sim | `Pendente`, `Processando`, `Concluida`, `Erro` |
| `Empresa` | String | Sim | Siglas das empresas |
| `Nivel` | Int | Sim | Nível de criticidade |
| `DataInicio` | DateTime? | Não | Data/hora de início |
| `DataFim` | DateTime? | Não | Data/hora de fim |
| `Erro` | String? | Não | Mensagem de erro |

#### **2.1.4. Resultado**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `PK` | String (HashKey) | Sim | `VER#{ExecucaoId}#{VerificacaoId}` |
| `SK` | String (RangeKey) | Sim | `RES#{CodId}` |
| `GSI1_PK` | String | Sim | `EXEC#{ExecucaoId}` (para agregação) |
| `GSI1_SK` | String | Sim | `VER#{VerifId}#EMP#{Empresa}#TAB#{Tabela}#CAMPO#{Campo}` |
| `ExecucaoId` | String | Sim | ID da execução |
| `VerificacaoId` | String | Sim | ID da verificação |
| `IdEmbaixada` | String | Não | ID da embaixada (deprecated) |
| `IdEmbaixadas` | List\<String\> | Sim | Lista de IDs das embaixadas |
| `Empresa` | String | Sim | Sigla da empresa (ex: "MA") |
| `Tabela` | String | Sim | Nome da tabela do banco |
| `Campo` | String | Sim | Nome do campo com erro |
| `Referencia` | String | Sim | Referência do registro |
| `TipoApontamento` | String | Sim | Tipo (ex: "INCONSISTENCIA") |
| `Nivel` | Int | Sim | Nível de criticidade |
| `DetalheErro` | String | Sim | Descrição detalhada do erro |
| `ValorEncontrado` | String? | Não | Valor encontrado |
| `ValorEsperado` | String? | Não | Valor esperado |
| `DataApontamento` | DateTime | Sim | Data/hora do apontamento |
| `NomeVerificacao` | String? | Não | Nome da verificação |

#### **2.1.5. ResultadoAgregado**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `PK` | String (HashKey) | Sim | Segmentado: `EXEC#{ExecucaoId}#EMB#{IdEmbaixada}`<br>Global: `EXEC#{ExecucaoId}#EMB#GLOBAL` |
| `SK` | String (RangeKey) | Sim | Chave de agrupamento: `{Empresa}\|{VerifId}\|{Tabela}\|{Campo}\|{Referencia}\|{Tipo}\|{Nivel}` |
| `GSI2_PK` | String | Sim | `EMP#{Empresa}` (para busca por empresa) |
| `GSI2_SK` | String | Sim | `EXEC#{ExecucaoId}#EMB#{IdEmbaixada}` |
| `ExecucaoId` | String | Sim | ID da execução |
| `VerificacaoId` | String | Sim | ID da verificação |
| `IdEmbaixada` | String | Sim | ID da embaixada (vazio para global) |
| `Empresa` | String | Sim | Sigla da empresa |
| `Tabela` | String | Sim | Nome da tabela |
| `Campo` | String | Sim | Nome do campo |
| `Referencia` | String | Sim | Referência do registro |
| `TipoApontamento` | String | Sim | Tipo de apontamento |
| `Nivel` | Int | Sim | Nível de criticidade |
| `Quantidade` | Int | Sim | Quantidade de apontamentos no grupo |
| `DescricaoErro` | String? | Não | Descrição exemplo do erro |
| `DataAgregacao` | DateTime | Sim | Data/hora da agregação |

#### **2.1.6. Consulta**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `Id` | String (HashKey) | Sim | ID da consulta |
| `QuerySql` | String | Sim | Query SQL com parâmetros (ex: `SELECT * FROM @SCHEMA.TABELA`) |
| `Identificador` | String | Sim | Identificador único |
| `Descricao` | String | Não | Descrição da consulta |

#### **2.1.7. Parametro**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `Id` | String (HashKey) | Sim | ID do parâmetro |
| `Alias` | String | Sim | Alias para substituição (ex: "@SCHEMA", "@Texto") |
| `ValorPadrao` | String | Não | Valor padrão |
| `Tipo` | String | Sim | Tipo do parâmetro |

#### **2.1.8. ExecucaoResumoView**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `PK` | String (HashKey) | Sim | `EMB#{IdEmbaixada}#EMP#{Empresa}` |
| `SK` | String (RangeKey) | Sim | `EXEC#{ExecucaoId}` |
| `ExecucaoId` | String | Sim | ID da execução |
| `IdEmbaixada` | String | Sim | ID da embaixada |
| `Empresa` | String | Sim | Sigla da empresa |
| `Status` | String | Sim | Status da execução |
| `TotalApontamentos` | Int | Sim | Total de apontamentos |
| `DataInicio` | DateTime? | Não | Data/hora de início |
| `DataFim` | DateTime? | Não | Data/hora de fim |

#### **2.1.9. ExecucaoEmpresaStatus**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `PK` | String (HashKey) | Sim | `EXEC#{ExecucaoId}` |
| `SK` | String (RangeKey) | Sim | `EMP#{Empresa}#EMB#{IdEmbaixada}` |
| `ExecucaoId` | String | Sim | ID da execução |
| `Empresa` | String | Sim | Sigla da empresa |
| `IdEmbaixada` | String | Sim | ID da embaixada |
| `Status` | String | Sim | Status da execução |
| `VerificacoesProcessadas` | Int | Sim | Contador de verificações |
| `VerificacoesComErro` | Int | Sim | Contador de erros |
| `DataInicio` | DateTime? | Não | Data/hora de início |
| `DataFim` | DateTime? | Não | Data/hora de fim |

#### **2.1.10. Justificativa**

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `PK` | String (HashKey) | Sim | `JUST#{Id}` |
| `SK` | String (RangeKey) | Sim | `EXEC#{ExecucaoId}#VER#{VerificacaoId}` |
| `VerificacaoId` | String | Sim | ID da verificação |
| `Empresa` | String | Sim | Sigla da empresa |
| `Tabela` | String | Sim | Nome da tabela |
| `Campo` | String | Sim | Nome do campo |
| `Justificativa` | String | Sim | Texto da justificativa |
| `Usuario` | String | Sim | Usuário que criou |
| `DataCriacao` | DateTime | Sim | Data/hora de criação |

### 2.2. Mensagens SQS

#### **2.2.1. Mensagem de Execução**

**Fila**: `fila-execucao-dev`

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `MessageBody` | String | Sim | ID da execução (UUID) |
| `MessageAttributes` | Map | Não | Atributos adicionais (opcional) |

**Exemplo**:
```json
"99a03893-c760-4c82-8780-2d66a462ec55"
```

#### **2.2.2. Mensagem de Processo**

**Fila**: `fila-execucao-processo-dev`

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `execucaoId` | String | Sim | ID da execução |
| `isSuccess` | Boolean | Sim | Se o processamento foi bem-sucedido |

**Exemplo**:
```json
{
  "execucaoId": "d16bb8e6-4086-4bd1-9fbf-0d8b9f6ddf3a",
  "isSuccess": true
}
```

---

## 3. Guia de Interface/Operação (Endpoints e Monitoramento)

### 3.1. Endpoints da API REST

O Work Manager expõe uma **API REST** para monitoramento de logs e saúde do sistema.

#### **3.1.1. GET /api/logs**

Obtém logs com filtros opcionais.

**Parâmetros Query**:
- `level` (opcional): `Information`, `Warning`, `Error`, `Debug`
- `source` (opcional): Nome da categoria (ex: `ProcessorService`)
- `limit` (opcional): 1-1000 (padrão: 100)
- `offset` (opcional): >= 0 (padrão: 0)

**Resposta**: `200 OK`
```json
[
  {
    "id": "guid-unico",
    "timestamp": "2024-01-01T10:30:00Z",
    "level": "Information",
    "category": "ProcessorService",
    "message": "Processando execução: 99a03893-c760-4c82-8780-2d66a462ec55",
    "properties": {
      "ExecucaoId": "99a03893-c760-4c82-8780-2d66a462ec55"
    },
    "exception": null
  }
]
```

#### **3.1.2. GET /api/logs/recent**

Obtém logs recentes.

**Parâmetros Query**:
- `count` (opcional): 1-500 (padrão: 50)

**Resposta**: `200 OK` - Lista de logs mais recentes

#### **3.1.3. GET /api/logs/statistics**

Obtém estatísticas dos logs.

**Resposta**: `200 OK`
```json
{
  "totalLogs": 1250,
  "logsByLevel": {
    "Information": 800,
    "Warning": 300,
    "Error": 150
  },
  "logsByCategory": {
    "ProcessorService": 400,
    "VerificacaoProcessorService": 250
  }
}
```

#### **3.1.4. GET /api/logs/view**

Interface web HTML para visualização de logs em tempo real.

**Resposta**: `200 OK` - HTML com dashboard interativo

**Funcionalidades**:
- Visualização colorida por nível de log
- Filtros interativos
- Auto-refresh configurável (30s)
- Estatísticas em tempo real

#### **3.1.5. GET /api/logs/level/{level}**

Obtém logs por nível específico.

**Parâmetros Path**:
- `level`: `Information`, `Warning`, `Error`, `Debug`

**Parâmetros Query**:
- `limit` (opcional): 1-1000 (padrão: 100)

#### **3.1.6. GET /api/logs/source/{source}**

Obtém logs por fonte/categoria.

**Parâmetros Path**:
- `source`: Nome da categoria

**Parâmetros Query**:
- `limit` (opcional): 1-1000 (padrão: 100)

#### **3.1.7. DELETE /api/logs**

Limpa todos os logs da memória.

**Resposta**: `200 OK`
```json
{
  "message": "Logs limpos com sucesso"
}
```

### 3.2. Gatilhos de Processamento

#### **3.2.1. Mensagem SQS de Execução**

**Gatilho**: Mensagem recebida em `fila-execucao-dev`

**Processo**:
1. **Worker** recebe mensagem via `MessageProcessorService`
2. **ProcessorService** busca `Execucao` no DynamoDB
3. Para cada `Verificacao`, cria `ExecucaoVerificacao`
4. Envia mensagens para `fila-execucao-processo-dev`
5. Atualiza status: `Pendente` → `AguardandoProcessamento`

**Logs de Monitoramento**:
```
info: ProcessorService[0] Processando execução: {ExecucaoId}
info: ProcessorService[0] {QueriesEnviadas} verificações enviadas para processamento
```

#### **3.2.2. Mensagem SQS de Processo**

**Gatilho**: Mensagem recebida em `fila-execucao-processo-dev`

**Processo**:
1. **Worker** recebe mensagem via `MessageProcessorService`
2. **ProcessoProcessorService** atualiza contadores em `Execucao`
3. Verifica se todas as verificações foram processadas
4. Se sim, atualiza status: `FinalizadaComSucesso` / `FinalizadaComErro`
5. Inicia **PostProcessingPipeline** (se habilitado)

**Logs de Monitoramento**:
```
info: ProcessoProcessorService[0] Atualizando contadores para execução: {ExecucaoId}
info: ProcessoProcessorService[0] Execução finalizada. Status: {Status}
```

#### **3.2.3. Pipeline de Pós-Processamento**

**Gatilho**: Execução finalizada com sucesso

**Steps Configuráveis**:
1. **AgregacaoResultadosStep** (Order: 10) ✅ Habilitado
   - Busca apontamentos via GSI (`GSI_Agregacao`)
   - Agrupa por chave de agrupamento
   - Insere em `ResultadoAgregado` (segmentado e global)
2. **ProcessamentoJustificativasStep** (Order: 5) ✅ Habilitado
   - Remove apontamentos justificados de `Resultado`
   - Remove grupos justificados de `ResultadoAgregado`
3. **AgrupamentoStep** (Order: 1) ❌ Desabilitado
4. **ExclusaoRegistrosStep** (Order: 2) ❌ Desabilitado

**Logs de Monitoramento**:
```
info: PostProcessingPipeline[0] Iniciando pipeline de pós-processamento para execução: {ExecucaoId}
info: AgregacaoResultadosStep[0] Agregacao concluida. {TotalApontamentos} apontamentos. Segmentados: {GruposSegmentados}, Globais: {GruposGlobais}
info: PostProcessingPipeline[0] Pipeline concluído. Tempo total: {TempoMs}ms
```

### 3.3. Monitoramento de Sucesso/Erro

#### **3.3.1. Logs de Sucesso**

**Padrões de Log**:
- `info:` - Operações bem-sucedidas
- `Processando execução: {ExecucaoId}`
- `Execução finalizada. Status: FinalizadaComSucesso`
- `{Count} mensagens processadas`
- `Agregacao concluida. {TotalApontamentos} apontamentos`

**Verificação no DynamoDB**:
```sql
-- Consultar status da execução
SELECT Status, VerificacoesProcessadas, QuantidadeVerificacoes 
FROM Execucao 
WHERE Id = '{ExecucaoId}'
```

**Valores Esperados**:
- `Status` = `FinalizadaComSucesso`
- `VerificacoesProcessadas` = `QuantidadeVerificacoes`
- `DataFim` != `null`

#### **3.3.2. Logs de Erro**

**Padrões de Log**:
- `error:` - Erros fatais
- `warning:` - Avisos (não bloqueantes)
- `Falha ao processar execução: {ExecucaoId}`
- `Erro ao buscar execução: {ExecucaoId}`
- `Amazon.DynamoDBv2.Model.ResourceNotFoundException`

**Verificação no DynamoDB**:
```sql
-- Consultar erros
SELECT Status, Erro, Erros 
FROM Execucao 
WHERE Id = '{ExecucaoId}'
```

**Valores Esperados em Erro**:
- `Status` = `FinalizadaComErro`
- `Erro` != `null` ou `Erros.Count > 0`
- `VerificacoesComErro` > 0

#### **3.3.3. Health Checks Automáticos**

**Frequência**: A cada 5 minutos

**Verificações**:
1. **Conectividade SQS**: Testa acesso às filas
2. **Saúde das Filas**: Verifica mensagens pendentes e DLQ
3. **Conectividade DynamoDB**: Testa leitura/escrita

**Logs**:
```
info: QueueHealthService[0] Verificando saúde das filas...
info: QueueHealthService[0] Todas as filas estão saudáveis
```

#### **3.3.4. Métricas de Performance**

**Métricas Capturadas nos Logs**:
- Tempo de processamento de execução (ms)
- Quantidade de mensagens processadas por ciclo
- Tempo de agregação de resultados (ms)
- Quantidade de apontamentos agregados

**Exemplo de Log**:
```
info: AgregacaoResultadosStep[0] Step AgregacaoResultados executado com sucesso. Tempo: 72701.6345ms
```

### 3.4. Troubleshooting

#### **3.4.1. Execução Não Processada**

**Sintomas**: Status permanece `Pendente` ou `AguardandoProcessamento`

**Verificações**:
1. Logs: `error: ProcessorService[0] Erro ao processar execução`
2. SQS: Verificar mensagens na DLQ (Dead Letter Queue)
3. DynamoDB: Verificar se `Execucao` existe com ID correto

#### **3.4.2. Resultados Não Agregados**

**Sintomas**: `ResultadoAgregado` vazio após processamento

**Verificações**:
1. Logs: `AgregacaoResultadosStep[0] Insercao SEGMENTADA concluida. 0 registros inseridos`
2. DynamoDB: Verificar se `Resultado` tem dados com `GSI1_PK = "EXEC#{ExecucaoId}"`
3. Configuração: Verificar `PostProcessing.AgregacaoResultados.Enabled = true`

#### **3.4.3. Erro de Tabela Não Encontrada**

**Sintomas**: `ResourceNotFoundException: Table: {TableName} not found`

**Verificações**:
1. Configuração: Verificar `appsettings.json` → `DynamoDB.TableName{Entity}`
2. AWS: Verificar se tabelas existem no DynamoDB com nomes configurados
3. Permissões: Verificar IAM Role tem permissões de leitura/escrita

---

## 4. Configuração

### 4.1. Arquivo appsettings.json

```json
{
  "AWS": {
    "Region": "sa-east-1",
    "UseLocalStack": false
  },
  "SQS": {
    "FilaExecucao": "fila-execucao-dev",
    "FilaExecucaoProcesso": "fila-execucao-processo-dev",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20,
    "VisibilityTimeoutSeconds": 60
  },
  "DynamoDB": {
    "TableNameExecucao": "dynamo-embaixadas-execucoes-devqa-eqtl-bdgd-us-east-1",
    "TableNameVerificacao": "dynamo-embaixadas-verificacoes-devqa-eqtl-bdgd-us-east-1",
    "TableNameExecucaoVerificacao": "dynamo-embaixadas-execucao-verificacao-devqa-eqtl-bdgd-us-east-1",
    "TableNameResultado": "dynamo-embaixadas-resultado-devqa-eqtl-bdgd-us-east-1",
    "TableNameResultadoAgregado": "dynamo-embaixadas-resultado-agregado-devqa-eqtl-bdgd-us-east-1",
    "TableNameExecucaoResumoView": "dynamo-embaixadas-execucao-resumo-view-devqa-eqtl-bdgd-us-east-1",
    "TableNameExecucaoEmpresaStatus": "dynamo-embaixadas-execucao-empresa-status-devqa-eqtl-bdgd-us-east-1",
    "TableNameJustificativa": "dynamo-embaixadas-justificativa-devqa-eqtl-bdgd-us-east-1",
    "TableNameConsulta": "dynamo-embaixadas-consultas-devqa-eqtl-bdgd-us-east-1",
    "TableNameParametro": "dynamo-embaixadas-parametros-devqa-eqtl-bdgd-us-east-1"
  },
  "PostProcessing": {
    "Enabled": true,
    "AgregacaoResultados": {
      "Enabled": true,
      "Order": 10,
      "TimeoutSeconds": 120,
      "BatchSize": 25
    },
    "ProcessamentoJustificativas": {
      "Enabled": true,
      "Order": 5,
      "TimeoutSeconds": 60
    }
  }
}
```

---

**Última Atualização**: Janeiro 2025  
**Versão do Documento**: 1.0

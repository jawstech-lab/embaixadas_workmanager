# Embaixadas WorkManager

Serviço Worker Service em C# para .NET 8+ que utiliza AWS DynamoDB e SQS para gerenciamento de execuções, verificações e consultas.

## Diretrizes de Estilo

**IMPORTANTE**: Este projeto não utiliza símbolos especiais (✓, ✗, ⚠, etc.) ou emojis nos logs, mensagens e documentação. Mantenha o estilo limpo e profissional usando apenas texto simples.

### Exemplos:
- **CORRETO**: "Operação concluída com sucesso"
- **INCORRETO**: "✓ Operação concluída com sucesso"

- **CORRETO**: "Ambas as filas estão saudáveis"
- **INCORRETO**: "✓ Ambas as filas estão saudáveis"

## Documentação

**Toda a documentação detalhada está na pasta `Context/`**:
- **README.md** (este arquivo): Visão geral e início rápido
- **Context/README.md**: Organização da documentação
- **Context/STYLE_GUIDE.md**: Diretrizes de estilo e padrões
- **Context/REFATORACAO_WORKER.md**: Arquitetura e refatorações
- **Context/CORRECAO_ARGUMENT_NULL_EXCEPTION.md**: Correções de bugs
- **Context/teste-correcao-sqs.md**: Problemas e soluções SQS
- **Context/aws-config.md**: Configuração AWS
- **Context/MUDANCA_EXECUCAO_MESSAGE.md**: Mudança no ExecucaoMessage
- **Context/CONSOLIDACAO_EXECUCAO_PROCESSO.md**: Plano de consolidação Execucao + ExecucaoProcesso

## Resumo Executivo

O **Embaixadas WorkManager** é um sistema de processamento assíncrono que gerencia execuções de validações de dados. Atualmente, o sistema:

### O que o programa faz:

1. **Recebe mensagens de execução** via SQS contendo:
   - **Apenas o ID da execução como string pura** (ex: "92de1003-bec3-4767-985e-6eb500910a5a")

2. **Processa cada execução** buscando no DynamoDB:
   - Busca a execução existente na tabela Execucoes
   - Verifica se o status é "Pendente"
   - Para cada verificação na lista, busca os dados no DynamoDB
   - Carrega a consulta SQL associada à verificação
   - **Processa parâmetros dinâmicos** usando Alias e valores configurados
   - Prepara mensagem de query para processamento posterior

3. **Arquitetura modular** com separação de responsabilidades:
   - `ExecucaoProcessorService`: Orquestra o processamento de execuções
   - `VerificacaoProcessorService`: Processa verificações individuais
   - `ConsultaService`: Gerencia acesso às consultas SQL e processamento de parâmetros
   - `DynamoDbService`: Operações genéricas do banco
   - `ResilientSqsService`: Operações SQS com resiliência

### Fluxo atual de processamento:

```
Mensagem SQS → ExecucaoProcessor → VerificacaoProcessor → ConsultaService → QueryMessage (simulado)
```

### Status atual:
- Recebimento de mensagens SQS funcionando
- Processamento de execuções implementado
- Busca de verificações no DynamoDB
- Busca de consultas SQL associadas
- **Processamento de parâmetros dinâmicos por Alias**
- Criação de QueryMessage para processamento
- **Envio real de QueryMessage para fila SQS**
- Logs estruturados e monitoramento
- Arquitetura modular e extensível

### Próximas implementações:
- Worker Service para processamento de queries
- Execução real de SQL queries
- Salvamento de resultados no DynamoDB

### Melhorias planejadas:
- **Consolidação Execucao + ExecucaoProcesso**: Eliminar duplicação entre objetos, consolidando todas as informações em um único objeto Execucao expandido
- Manter processamento assíncrono das verificações via fila-execucao-processo
- Simplificar arquitetura e melhorar performance

## Funcionalidades

- **Worker Service**: Serviço em background para processamento contínuo
- **DynamoDB**: Armazenamento NoSQL para dados de execução, verificação e consulta
- **SQS**: Filas para processamento assíncrono de mensagens
- **Processamento Real**: Processamento efetivo de mensagens SQS com persistência no DynamoDB
- **Arquitetura Modular**: Separação clara de responsabilidades entre serviços
- **Resiliência**: Circuit breaker e retry policies para operações SQS
- **Logging**: Sistema de logs estruturado para monitoramento
- **Configuração**: Configuração flexível via appsettings.json
- **Parâmetros Dinâmicos**: Substituição de parâmetros SQL usando Alias e valores configurados

## Pré-requisitos

- .NET 8.0 SDK ou superior
- Conta AWS com acesso a DynamoDB e SQS
- Credenciais AWS configuradas

## Configuração

### 1. Configuração AWS

Edite o arquivo `appsettings.json` ou `appsettings.Development.json`:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "UseProfile": true,
    "ProfileName": "default",
    "AccessKey": "",
    "SecretKey": "",
    "ServiceUrl": "",
    "UseLocalStack": false
  }
}
```

#### Opções de Configuração:

- **UseProfile**: `true` para usar perfil AWS configurado, `false` para usar AccessKey/SecretKey
- **ProfileName**: Nome do perfil AWS (padrão: "default")
- **AccessKey/SecretKey**: Credenciais AWS (quando UseProfile = false)
- **Region**: Região AWS onde estão os recursos
- **UseLocalStack**: `true` para usar LocalStack local

### 2. Configuração SQS

```json
{
  "SQS": {
    "FilaExecucao": "fila-execucao",
    "FilaExecucaoQuery": "fila-execucao-query",
    "MaxNumberOfMessages": 10,
    "WaitTimeSeconds": 20,
    "VisibilityTimeoutSeconds": 300
  }
}
```

### 3. Configuração DynamoDB

```json
{
  "DynamoDB": {
    "TableNameExecucao": "Execucao",
    "TableNameVerificacao": "Verificacao",
    "TableNameExecucaoVerificacao": "ExecucaoVerificacao",
    "ServiceUrl": "",
    "UseLocalStack": false
  }
}
```

## Estrutura do Projeto

```
EmbaixadasWorkManager/
├── Context/               # Documentação completa do projeto
│   ├── README.md         # Organização da documentação
│   ├── STYLE_GUIDE.md    # Diretrizes de estilo
│   ├── REFATORACAO_WORKER.md # Arquitetura e refatorações
│   ├── CORRECAO_ARGUMENT_NULL_EXCEPTION.md # Correções de bugs
│   ├── teste-correcao-sqs.md # Problemas e soluções SQS
│   └── aws-config.md     # Configuração AWS
├── Configuration/         # Classes de configuração
│   └── AwsConfiguration.cs
├── Interfaces/            # Interfaces dos serviços
│   ├── IDynamoDbService.cs
│   ├── ISqsService.cs
│   ├── IResilientSqsService.cs
│   ├── IExecucaoProcessorService.cs
│   ├── IVerificacaoProcessorService.cs
│   └── IConsultaService.cs
├── Models/                # Modelos de dados (POCOs)
│   ├── Execucao.cs
│   ├── ExecucaoMessage.cs
│   ├── Verificacao.cs
│   ├── VerificacaoParametroValor.cs
│   ├── Parametro.cs
│   ├── Consulta.cs
│   ├── QueryMessage.cs
│   └── ExecucaoVerificacao.cs
├── Services/              # Implementação dos serviços
│   ├── DynamoDbService.cs
│   ├── SqsService.cs
│   ├── ResilientSqsService.cs
│   ├── ExecucaoProcessorService.cs
│   ├── VerificacaoProcessorService.cs
│   └── ConsultaService.cs
├── Program.cs             # Configuração da aplicação
├── Worker.cs              # Serviço Worker principal
├── test-send-messages.ps1 # Script para enviar mensagens de teste
└── appsettings.json       # Configurações
```

## Modelos de Dados

### Execucao
- **Id**: Chave primária (Hash Key) - Gerado automaticamente
- **Base**: Nome da base de dados
- **DataBase**: Data da base de dados
- **DataSolicitacao**: Data da solicitação da validação
- **Empresa**: Nome da empresa
- **Usuario**: Usuário que solicitou a execução
- **Validacoes**: Array de IDs de validação (GUIDs)
- **Status**: Status da execução (Pendente, EmProcessamento, FinalizadaComSucesso, FinalizadaComErro)
- **DataInicio**: Data de início do processamento
- **DataFim**: Data de fim do processamento
- **Erro**: Erro ocorrido (se houver)
- **Resultado**: Resultado da execução (opcional)

### Verificacao
- **Id**: Chave primária (Hash Key)
- **CreationTime**: Timestamp de criação
- **Entidade**: Nome da entidade/tabela
- **IdConsulta**: ID da consulta associada
- **IdEmbaixadas**: Lista de IDs de embaixadas
- **IdentificadorConsulta**: Identificador da consulta
- **IdTipo**: Tipo da verificação (ex: DPI)
- **ModificationTime**: Timestamp de modificação
- **ModifiedBy**: Usuário que modificou
- **Nivel**: Nível da verificação
- **NomeVerificacao**: Nome da verificação
- **Ordem**: Ordem de execução
- **ValoresParametros**: Lista de valores de parâmetros para substituição

### VerificacaoParametroValor
- **IdParametro**: ID do parâmetro (referência à tabela Parametros)
- **ValorParametro**: Valor específico para esta verificação

### Parametro
- **Id**: Chave primária (Hash Key)
- **Alias**: Nome do parâmetro usado no SQL (ex: "EMPRESA", "NUMERO")
- **TipoParametro**: Tipo do parâmetro (código numérico)
- **TipoValor**: Tipo do valor (1=string, 2=number, 3=date, 4=boolean)

### Consulta
- **Id**: Chave primária (Hash Key)
- **CreatedAt**: Data de criação
- **CreatedBy**: Usuário que criou
- **CreationTime**: Timestamp de criação
- **Descricao**: Descrição da consulta
- **Identificador**: Identificador da consulta
- **ModificationTime**: Timestamp de modificação
- **ModifiedAt**: Data de modificação
- **ModifiedBy**: Usuário que modificou
- **QuerySql**: SQL da consulta (pode conter parâmetros @Alias)
- **SearchText**: Texto de busca

### QueryMessage
- **ExecucaoId**: ID da execução
- **VerificacaoId**: ID da verificação
- **QueryId**: ID da consulta
- **Base**: Nome da base de dados
- **DataBase**: Data da base
- **Empresa**: Nome da empresa
- **Usuario**: Usuário
- **Sql**: SQL a ser executado (com parâmetros substituídos)
- **Parametros**: Parâmetros da query
- **TimeoutSegundos**: Timeout em segundos
- **Prioridade**: Prioridade de execução
- **DataSolicitacao**: Data da solicitação
- **Metadata**: Metadados adicionais (inclui SQL original e parâmetros substituídos)

## Arquitetura de Processamento

### Fluxo de Processamento

1. **Worker Service** recebe mensagens da fila SQS de execução
2. **ExecucaoProcessorService** processa a mensagem de execução:
   - Valida a mensagem
   - Salva a execução no DynamoDB
   - Para cada verificação na execução, chama o VerificacaoProcessorService
3. **VerificacaoProcessorService** processa cada verificação:
   - Carrega a verificação do DynamoDB
   - Usa o ConsultaService para buscar a consulta associada
   - **Processa parâmetros dinâmicos** usando ValoresParametros e Alias
   - Cria QueryMessage e envia para fila de processamento
4. **ConsultaService** gerencia o acesso às consultas no DynamoDB e processamento de parâmetros

### Separação de Responsabilidades

- **ExecucaoProcessorService**: Orquestra o processamento de execuções
- **VerificacaoProcessorService**: Processa verificações individuais
- **ConsultaService**: Gerencia acesso às consultas e processamento de parâmetros
- **DynamoDbService**: Operações genéricas do DynamoDB
- **ResilientSqsService**: Operações SQS com resiliência

## Funcionalidade de Parâmetros Dinâmicos

### Como funciona:

O sistema suporta parâmetros dinâmicos nas consultas SQL usando a sintaxe `@Alias`. Por exemplo:

```sql
SELECT * FROM Tabela WHERE 1=@NUMERO AND empresa = @EMPRESA
```

### Processo de substituição:

1. **Verificação** contém `ValoresParametros` com `IdParametro` e `ValorParametro`
2. **Consulta** contém SQL com placeholders `@Alias`
3. **Parametro** define o `Alias` e `TipoValor` para formatação
4. **Sistema** substitui `@Alias` pelo valor correspondente formatado

### Tipos de parâmetros suportados (TipoValor):

- **1 (string/text)**: `'valor'` (com escape de aspas)
- **2 (number)**: `123` (sem aspas)
- **3 (date/datetime)**: `'2024-01-01'` (com aspas)
- **4 (boolean)**: `1` ou `0` (para true/false)

### Exemplo prático:

**SQL Original:**
```sql
SELECT * FROM Clientes WHERE ativo = @ATIVO AND idade > @IDADE AND empresa = @EMPRESA
```

**Tabela Parametros:**
```json
{
  "Id": "386e5928-1d05-44e9-b0e0-c5b4f7319655",
  "Alias": "EMPRESA",
  "TipoParametro": 2,
  "TipoValor": 1
}
```

**Verificacao.ValoresParametros:**
```json
[
  {
    "IdParametro": "386e5928-1d05-44e9-b0e0-c5b4f7319655",
    "ValorParametro": "ma"
  }
]
```

**SQL Processado:**
```sql
SELECT * FROM Clientes WHERE ativo = 1 AND idade > 18 AND empresa = 'ma'
```

### Logs e monitoramento:

- Logs detalhados de cada substituição com Alias e ParametroId
- SQL original preservado nos metadados
- Lista de parâmetros substituídos registrada
- Tratamento de erros com fallback para SQL original

## Execução

### Desenvolvimento
```bash
dotnet run --environment Development
```

### Produção
```bash
dotnet run --environment Production
```

### Build
```bash
dotnet build
dotnet publish -c Release
```

## Logs

O sistema gera logs estruturados para:
- Inicialização dos serviços
- Conexões com AWS
- Operações DynamoDB
- Operações SQS
- Processamento de mensagens
- Processamento de verificações
- Busca de consultas
- Processamento de parâmetros dinâmicos
- Erros e exceções

## Configuração de Credenciais AWS

### Opção 1: Perfil AWS (Recomendado)
```bash
aws configure --profile default
```

### Opção 2: Variáveis de Ambiente
```bash
export AWS_ACCESS_KEY_ID=sua_access_key
export AWS_SECRET_ACCESS_KEY=sua_secret_key
export AWS_DEFAULT_REGION=us-east-1
```

### Opção 3: Arquivo de Configuração
```bash
# ~/.aws/credentials
[default]
aws_access_key_id = sua_access_key
aws_secret_access_key = sua_secret_key

# ~/.aws/config
[default]
region = us-east-1
```

## Testes

### Teste de Conectividade
1. Execute o projeto
2. Verifique os logs de inicialização
3. Confirme as mensagens de teste de conexão
4. Monitore o processamento contínuo

### Teste de Processamento de Mensagens
1. **Crie execuções de teste no DynamoDB**:
   ```powershell
   .\create-test-execucoes.ps1 -ExecucaoCount 5
   ```

2. **Envie mensagens de teste** (apenas IDs das execuções):
   ```powershell
   .\test-send-messages.ps1 -MessageCount 5
   ```

3. **Execute o Worker**:
   ```bash
   dotnet run --environment Development
   ```

4. **Monitore os logs** para ver o processamento das mensagens

5. **Verifique o DynamoDB** para confirmar que as execuções foram processadas

### Estrutura da Mensagem SQS de Execução
```
92de1003-bec3-4767-985e-6eb500910a5a
```

**IMPORTANTE**: A mensagem SQS contém apenas o ID da execução como string pura (GUID). A execução deve existir previamente na tabela `Execucoes` do DynamoDB com status "Pendente".

### Estrutura da Mensagem SQS de Query
```
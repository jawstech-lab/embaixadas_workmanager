# Arquitetura de Pós-Processamento - Pipeline Extensível

## Visão Geral

Sistema de pós-processamento genérico e extensível que executa automaticamente após a finalização de uma execução. Implementado usando o padrão **Pipeline/Chain of Responsibility**, permitindo adicionar novos steps de forma independente e configurável.

## Problema Resolvido

### **Antes**
- Lógica de pós-processamento hardcoded no ProcessoProcessorService
- Difícil adicionar novos processos
- Código acoplado e difícil de testar
- Sem controle granular de execução

### **Depois**
- Arquitetura extensível baseada em pipeline
- Steps independentes e reutilizáveis
- Fácil adicionar/remover processos via configuração
- Controle individual de cada step (habilitado, ordem, timeout)
- Logs detalhados de cada etapa

## Arquitetura

### **Componentes**

```
PostProcessingContext
  ├─ Dados compartilhados entre steps
  ├─ Metadados da execução
  └─ Controle de fluxo (ShouldStop, ErrorMessage)

IPostProcessingStep (Interface)
  ├─ StepName: Nome identificador
  ├─ Order: Ordem de execução
  ├─ IsEnabled: Habilitado/Desabilitado
  ├─ CanExecuteAsync(): Validação pré-execução
  └─ ExecuteAsync(): Lógica principal

PostProcessingPipeline (Executor)
  ├─ Gerencia ordem de execução
  ├─ Executa steps sequencialmente
  ├─ Coleta resultados
  └─ Gera relatório final

Steps Concretos
  ├─ AgrupamentoStep (habilitado)
  ├─ ExclusaoRegistrosStep (desabilitado)
  └─ ... (futuros steps)
```

### **Fluxo Completo**

```
1. Execução Finalizada (todas verificações processadas)
   ↓
2. ProcessoProcessorService detecta finalização
   ↓
3. Cria PostProcessingContext com dados da execução
   ↓
4. Chama PostProcessingPipeline.ExecuteAsync()
   ↓
5. Pipeline ordena steps habilitados (Order crescente)
   ↓
6. Para cada step:
   ├─ Valida se pode executar (CanExecuteAsync)
   ├─ Executa step (ExecuteAsync)
   ├─ Coleta resultado
   └─ Verifica se deve continuar
   ↓
7. Retorna PostProcessingResult com resumo
   ↓
8. Logs detalhados de execução
   ↓
9. Continua fluxo normal (salvar execução)
```

## Ponto de Integração

### **ProcessoProcessorService.cs**

**Local**: Linhas 99-121 (após todas as verificações serem processadas)

```csharp
// Verificar se todas as verificações foram processadas
var totalProcessadas = execucao.VerificacoesProcessadas + execucao.VerificacoesComErro;
if (totalProcessadas >= execucao.QuantidadeVerificacoes)
{
    // Definir status final
    var finalizadaComSucesso = execucao.VerificacoesComErro == 0;
    execucao.Status = finalizadaComSucesso 
        ? StatusExecucao.FinalizadaComSucesso 
        : StatusExecucao.FinalizadaComErro;
    execucao.DataFim = DateTime.UtcNow;

    // NOVO: Executar pipeline de pós-processamento
    await ExecutarPosProcessamentoAsync(execucao, finalizadaComSucesso);
}
```

## Modelos

### **PostProcessingContext**

Contexto compartilhado entre todos os steps.

```csharp
public class PostProcessingContext
{
    public Execucao Execucao { get; set; }                    // Execução finalizada
    public bool FinalizadaComSucesso { get; set; }            // Status de finalização
    public Dictionary<string, object> Metadata { get; set; }  // Metadados compartilhados
    public bool ShouldStop { get; set; }                      // Parar pipeline
    public string? ErrorMessage { get; set; }                 // Mensagem de erro
    public DateTime StartedAt { get; set; }                   // Timestamp de início
    public List<string> ExecutedSteps { get; set; }           // Steps executados
    public List<string> FailedSteps { get; set; }             // Steps que falharam
}
```

### **PostProcessingStepResult**

Resultado da execução de um step individual.

```csharp
public class PostProcessingStepResult
{
    public bool Success { get; set; }                         // Sucesso/Falha
    public string Message { get; set; }                       // Mensagem descritiva
    public Dictionary<string, object>? Data { get; set; }     // Dados adicionais
    public bool ShouldStopPipeline { get; set; }              // Parar pipeline
    public TimeSpan ExecutionTime { get; set; }               // Tempo de execução

    // Helpers
    public static PostProcessingStepResult Ok(string message, Dictionary<string, object>? data = null);
    public static PostProcessingStepResult Fail(string message, bool shouldStop = false);
}
```

### **PostProcessingResult**

Resultado completo do pipeline.

```csharp
public class PostProcessingResult
{
    public bool Success { get; set; }                         // Sucesso geral
    public int TotalStepsExecuted { get; set; }               // Total executados
    public int TotalStepsFailed { get; set; }                 // Total falharam
    public List<string> Messages { get; set; }                // Mensagens de cada step
    public TimeSpan TotalExecutionTime { get; set; }          // Tempo total
    public PostProcessingContext? Context { get; set; }       // Contexto final
}
```

## Interface IPostProcessingStep

Interface genérica que todos os steps devem implementar.

```csharp
public interface IPostProcessingStep
{
    string StepName { get; }           // Nome identificador
    int Order { get; }                 // Ordem de execução (menor = primeiro)
    bool IsEnabled { get; }            // Habilitado/Desabilitado

    // Valida se pode executar no contexto atual
    Task<bool> CanExecuteAsync(PostProcessingContext context);

    // Executa o pós-processamento
    Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context);
}
```

## Steps Implementados

### **1. AgrupamentoStep**

**Propósito**: Agrupa dados da execução por critérios configuráveis.

**Configuração**:
```json
{
  "PostProcessing": {
    "Agrupamento": {
      "Enabled": true,
      "Order": 1,
      "TimeoutSeconds": 60,
      "GroupByFields": ["Empresa", "Status"],
      "SaveResult": true
    }
  }
}
```

**Funcionalidades**:
- Busca todas as ExecucaoVerificacao da execução
- Agrupa por Status (sempre)
- Agrupa por Empresa (se configurado)
- Calcula estatísticas por grupo
- Opcionalmente salva resultado

**Exemplo de Saída**:
```csharp
{
  "Status:Concluida": {
    "Criterio": "Status",
    "Valor": "Concluida",
    "Quantidade": 15,
    "TotalRegistros": 1500,
    "Verificacoes": ["id1", "id2", ...]
  },
  "Empresa:MA": {
    "Criterio": "Empresa",
    "Valor": "MA",
    "Quantidade": 8,
    "TotalRegistros": 800,
    "Verificacoes": ["id1", "id3", ...]
  }
}
```

**CanExecuteAsync**:
- Só executa se finalizada com sucesso
- Só executa se tem apontamentos para agrupar

### **2. ExclusaoRegistrosStep**

**Propósito**: Exclui registros antigos (exemplo de step adicional).

**Configuração**:
```json
{
  "PostProcessing": {
    "ExclusaoRegistros": {
      "Enabled": false,
      "Order": 2,
      "TimeoutSeconds": 30,
      "MinimumAgeInDays": 30
    }
  }
}
```

**Funcionalidades**:
- Exclui registros mais antigos que N dias
- Implementação placeholder (pode ser expandida)
- Desabilitado por padrão

## Configuração

### **PostProcessingConfiguration**

```csharp
public class PostProcessingConfiguration
{
    public bool Enabled { get; set; } = true;                     // Pipeline habilitado
    public int TimeoutSeconds { get; set; } = 300;                // Timeout global (5 min)
    public bool ContinueOnStepFailure { get; set; } = true;       // Continuar se step falhar
    
    public AgrupamentoStepConfiguration Agrupamento { get; set; }
    public ExclusaoRegistrosStepConfiguration ExclusaoRegistros { get; set; }
}
```

### **appsettings.json**

```json
{
  "PostProcessing": {
    "Enabled": true,
    "TimeoutSeconds": 300,
    "ContinueOnStepFailure": true,
    "Agrupamento": {
      "Enabled": true,
      "Order": 1,
      "TimeoutSeconds": 60,
      "GroupByFields": ["Empresa", "Status"],
      "SaveResult": true
    },
    "ExclusaoRegistros": {
      "Enabled": false,
      "Order": 2,
      "TimeoutSeconds": 30,
      "MinimumAgeInDays": 30
    }
  }
}
```

## PostProcessingPipeline

Executor que gerencia a execução ordenada de todos os steps.

### **Responsabilidades**

1. **Ordenação**: Ordena steps por `Order` (crescente)
2. **Filtragem**: Executa apenas steps habilitados (`IsEnabled = true`)
3. **Validação**: Chama `CanExecuteAsync()` antes de executar
4. **Execução**: Executa cada step sequencialmente
5. **Coleta**: Agrupa resultados de todos os steps
6. **Tratamento de Erros**: Decide se continua ou para após falha
7. **Logs**: Registra detalhes de cada etapa

### **Lógica de Controle**

```csharp
foreach (var step in orderedSteps)
{
    if (context.ShouldStop) break;  // Parar se solicitado
    
    if (!await step.CanExecuteAsync(context)) continue;  // Pular se não pode executar
    
    var result = await step.ExecuteAsync(context);
    
    if (!result.Success && !_config.ContinueOnStepFailure) break;  // Parar em falha
}
```

## Logs Gerados

### **Início do Pipeline**
```
[INFO] Iniciando pos-processamento para execucao {ExecucaoId}
[INFO] Pipeline configurado com 2 steps habilitados: Agrupamento (Order: 1), ExclusaoRegistros (Order: 2)
```

### **Execução de Step**
```
[INFO] Executando step: Agrupamento (Order: 1)
[INFO] Encontradas 15 verificacoes para agrupar
[INFO] Agrupamento concluido. 15 verificacoes agrupadas em 5 grupos. Total de apontamentos: 1500
[INFO] Step Agrupamento executado com sucesso. Tempo: 120ms. Mensagem: Agrupamento concluido...
```

### **Step Pulado**
```
[INFO] Step ExclusaoRegistros pulado. CanExecute retornou false
```

### **Conclusão**
```
[INFO] Pos-processamento concluido com sucesso para execucao {ExecucaoId}. Steps executados: 1, Tempo: 125ms
```

### **Falha**
```
[ERROR] Step Agrupamento falhou. Tempo: 50ms. Mensagem: Erro ao buscar verificacoes
[WARNING] Pos-processamento concluido com falhas para execucao {ExecucaoId}. Steps executados: 2, Falhas: 1, Tempo: 180ms
```

## Criando um Novo Step

### **1. Criar Classe do Step**

```csharp
public class MeuNovoStep : IPostProcessingStep
{
    private readonly ILogger<MeuNovoStep> _logger;
    private readonly MeuNovoStepConfiguration _config;

    public string StepName => "MeuNovoStep";
    public int Order => _config.Order;
    public bool IsEnabled => _config.Enabled;

    public MeuNovoStep(
        ILogger<MeuNovoStep> logger,
        IOptions<PostProcessingConfiguration> config)
    {
        _logger = logger;
        _config = config.Value.MeuNovoStep;
    }

    public async Task<bool> CanExecuteAsync(PostProcessingContext context)
    {
        // Validar se pode executar
        return context.FinalizadaComSucesso;
    }

    public async Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context)
    {
        try
        {
            _logger.LogInformation("Executando MeuNovoStep para execucao {ExecucaoId}", 
                context.Execucao.Id);

            // Sua lógica aqui
            await MinhaLogicaAsync(context);

            return PostProcessingStepResult.Ok("Step concluido com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar MeuNovoStep");
            return PostProcessingStepResult.Fail($"Erro: {ex.Message}");
        }
    }

    private async Task MinhaLogicaAsync(PostProcessingContext context)
    {
        // Implementar lógica específica
        await Task.CompletedTask;
    }
}
```

### **2. Adicionar Configuração**

```csharp
// Em PostProcessingConfiguration.cs
public class PostProcessingConfiguration
{
    // ... existentes ...
    public MeuNovoStepConfiguration MeuNovoStep { get; set; } = new();
}

public class MeuNovoStepConfiguration
{
    public bool Enabled { get; set; } = true;
    public int Order { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 60;
    // Suas propriedades específicas
}
```

### **3. Configurar appsettings.json**

```json
{
  "PostProcessing": {
    "MeuNovoStep": {
      "Enabled": true,
      "Order": 3,
      "TimeoutSeconds": 60
    }
  }
}
```

### **4. Registrar no Program.cs**

```csharp
services.AddSingleton<IPostProcessingStep, MeuNovoStep>();
```

**Pronto!** O step será executado automaticamente no pipeline.

## Casos de Uso

### **Agrupamento de Dados**
```
Finalização → Pipeline → AgrupamentoStep
  ├─ Busca verificações
  ├─ Agrupa por status
  ├─ Agrupa por empresa
  └─ Salva resultado
```

### **Notificações**
```
Finalização → Pipeline → NotificacaoStep
  ├─ Verifica tipo de execução
  ├─ Prepara mensagem
  ├─ Envia email/SMS
  └─ Registra envio
```

### **Geração de Relatórios**
```
Finalização → Pipeline → RelatorioStep
  ├─ Coleta métricas
  ├─ Gera relatório PDF
  ├─ Salva em S3
  └─ Atualiza execução
```

### **Limpeza de Dados**
```
Finalização → Pipeline → LimpezaStep
  ├─ Identifica registros temporários
  ├─ Remove do DynamoDB
  ├─ Limpa cache
  └─ Registra quantidade removida
```

## Benefícios da Arquitetura

### **1. Extensibilidade**
- Adicionar novos steps sem modificar código existente
- Steps independentes e reutilizáveis
- Fácil manutenção

### **2. Configurabilidade**
- Habilitar/desabilitar steps via config
- Controlar ordem de execução
- Timeouts individuais

### **3. Testabilidade**
- Steps podem ser testados isoladamente
- Mocks simples de dependências
- Testes de integração do pipeline

### **4. Observabilidade**
- Logs detalhados de cada step
- Métricas de performance
- Rastreamento de falhas

### **5. Resiliência**
- Falha em um step não interrompe outros
- Controle de propagação de erros
- Tratamento gracioso de exceções

## Limitações e Considerações

### **1. Execução Sequencial**
- Steps executam em ordem, não em paralelo
- Para processamento paralelo, use Task.WhenAll dentro do step

### **2. Timeout Global**
- Pipeline tem timeout de 5 minutos (configurável)
- Steps individuais têm seus próprios timeouts

### **3. Não Interrompe Fluxo Principal**
- Falha no pós-processamento não impede salvamento da execução
- Sistema continua funcionando mesmo se pipeline falhar

### **4. Performance**
- Adiciona tempo ao processamento final
- Considerar impacto em execuções frequentes
- Usar steps assíncronos para operações longas

## Métricas e Monitoramento

### **Métricas Disponíveis**
- Total de steps executados
- Total de steps que falharam
- Tempo de execução de cada step
- Tempo total do pipeline
- Taxa de sucesso/falha

### **Logs para Análise**
```
- Steps executados por execução
- Tempo médio de cada step
- Steps que mais falham
- Execuções que pularam steps
```

## Próximos Passos Sugeridos

### **1. Steps Adicionais**
- NotificacaoStep: Enviar notificações
- RelatorioStep: Gerar relatórios
- ArquivamentoStep: Arquivar dados antigos
- ValidacaoStep: Validações pós-execução

### **2. Melhorias**
- Processamento paralelo de steps independentes
- Cache de resultados entre steps
- Retry automático para steps falhados
- Dashboard de monitoramento

### **3. Integrações**
- Webhook para notificar sistemas externos
- Métricas para CloudWatch/Prometheus
- Alertas automáticos para falhas

## Conclusão

A arquitetura de pós-processamento implementada é **extensível**, **configurável** e **resiliente**. Permite adicionar novos processos facilmente sem modificar código existente, mantendo o sistema organizado e manutenível.

O padrão Pipeline/Chain of Responsibility garante que cada step seja independente e testável, enquanto o executor central gerencia a ordem e tratamento de erros de forma consistente.

**Status**: ✅ **IMPLEMENTAÇÃO COMPLETA** - Pronto para uso e expansão!

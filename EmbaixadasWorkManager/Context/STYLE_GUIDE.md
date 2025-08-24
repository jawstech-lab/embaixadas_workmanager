# Diretrizes de Estilo - Embaixadas WorkManager

## Visão Geral

Este documento define as diretrizes de estilo para o projeto Embaixadas WorkManager, garantindo consistência e profissionalismo em toda a documentação, logs e mensagens.

## Regras Principais

### 1. **Sem Símbolos Especiais ou Emojis**

**REGRAS OBRIGATÓRIAS:**
- ❌ **NÃO USE** símbolos especiais como: ✓, ✗, ⚠, →, ←, ↑, ↓
- ❌ **NÃO USE** emojis: 🚀, 📋, 🎯, 🔧, etc.
- ❌ **NÃO USE** caracteres Unicode especiais para indicar status

**EXCEÇÕES PERMITIDAS:**
- ✅ **PERMITIDO**: Seta simples (→) para fluxos de processamento em diagramas
- ✅ **PERMITIDO**: Caracteres básicos de texto: -, *, +, =, etc.

### 2. **Estilo de Logs e Mensagens**

#### **ANTES (INCORRETO):**
```csharp
_logger.LogInformation("✓ Mensagem processada com sucesso");
_logger.LogWarning("⚠ Problema detectado na fila");
_logger.LogError("✗ Falha na operação");
```

#### **DEPOIS (CORRETO):**
```csharp
_logger.LogInformation("Mensagem processada com sucesso");
_logger.LogWarning("Problema detectado na fila");
_logger.LogError("Falha na operação");
```

### 3. **Estilo de Documentação**

#### **ANTES (INCORRETO):**
```markdown
## 🚀 Funcionalidades
- ✅ Recebimento de mensagens SQS funcionando
- ✅ Processamento de execuções implementado
```

#### **DEPOIS (CORRETO):**
```markdown
## Funcionalidades
- Recebimento de mensagens SQS funcionando
- Processamento de execuções implementado
```

## Exemplos de Aplicação

### **Logs de Status**
```csharp
// INCORRETO
_logger.LogInformation("✓ Ambas as filas estão saudáveis");
_logger.LogInformation("Health Check - Fila Execução: {Healthy}", execucaoHealthy ? "✓" : "✗");

// CORRETO
_logger.LogInformation("Ambas as filas estão saudáveis");
_logger.LogInformation("Health Check - Fila Execução: {Healthy}", execucaoHealthy ? "Sim" : "Não");
```

### **Mensagens de Sucesso/Erro**
```csharp
// INCORRETO
_logger.LogInformation("✓ {ProcessedCount} mensagens processadas e removidas da fila", processedCount);
_logger.LogWarning("⚠ {FailedCount} mensagens falharam no processamento", failedCount);

// CORRETO
_logger.LogInformation("{ProcessedCount} mensagens processadas e removidas da fila", processedCount);
_logger.LogWarning("{FailedCount} mensagens falharam no processamento", failedCount);
```

### **Documentação de Arquitetura**
```markdown
// INCORRETO
### 🔄 **Fluxo atual de processamento:**
```
Mensagem SQS → ExecucaoProcessor → VerificacaoProcessor → ConsultaService → QueryMessage
```

// CORRETO
### Fluxo atual de processamento:
```
Mensagem SQS → ExecucaoProcessor → VerificacaoProcessor → ConsultaService → QueryMessage
```
```

## Benefícios do Estilo Limpo

1. **Profissionalismo**: Apresenta o projeto de forma séria e corporativa
2. **Compatibilidade**: Funciona em todos os sistemas e terminais
3. **Legibilidade**: Facilita a leitura em diferentes dispositivos
4. **Manutenibilidade**: Código mais limpo e fácil de manter
5. **Padrão Corporativo**: Segue padrões de empresas e organizações

## Checklist de Verificação

Antes de fazer commit, verifique se:

- [ ] Não há símbolos especiais (✓, ✗, ⚠, etc.) nos logs
- [ ] Não há emojis na documentação
- [ ] Mensagens de status usam texto simples
- [ ] Títulos de seções não têm emojis
- [ ] Listas de itens não têm símbolos de status
- [ ] Fluxos de processamento usam apenas setas simples (→)

## Exemplos de Arquivos Corrigidos

### **Worker.cs**
```csharp
// ANTES
_logger.LogInformation("✓ {ProcessedCount} mensagens processadas e removidas da fila", processedCount);

// DEPOIS
_logger.LogInformation("{ProcessedCount} mensagens processadas e removidas da fila", processedCount);
```

### **README.md**
```markdown
// ANTES
## 🚀 Funcionalidades
- ✅ Worker Service funcionando
- ✅ DynamoDB configurado

// DEPOIS
## Funcionalidades
- Worker Service funcionando
- DynamoDB configurado
```

## Conclusão

Manter o estilo limpo e profissional é essencial para a credibilidade do projeto. Siga sempre estas diretrizes para garantir consistência em todo o código e documentação.

**Lembre-se**: Texto simples e claro é sempre melhor que símbolos e emojis para projetos corporativos.

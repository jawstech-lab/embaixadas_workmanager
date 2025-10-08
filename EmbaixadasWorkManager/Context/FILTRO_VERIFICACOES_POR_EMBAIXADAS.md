# Filtro de Verificações por Embaixadas

## Visão Geral

Esta funcionalidade adiciona suporte para filtrar verificações com base em uma lista de IDs de embaixadas especificadas na Execução. Quando uma execução não tem validações específicas mas possui uma lista de embaixadas, o sistema busca apenas as verificações relacionadas a essas embaixadas.

## Problema Resolvido

**ANTES**: Quando uma execução não tinha validações específicas, o sistema buscava TODAS as verificações disponíveis, mesmo que nem todas fossem relevantes para as embaixadas desejadas.

**DEPOIS**: O sistema filtra as verificações, retornando apenas aquelas que têm pelo menos uma embaixada em comum com a lista fornecida na execução.

## Mudanças Implementadas

### 1. Modelo Execucao

**Arquivo**: `Models/Execucao.cs`

```csharp
[DynamoDBProperty("IdEmbaixadas")]
public List<string> IdEmbaixadas { get; set; } = new();
```

**Campo adicionado**:
- `IdEmbaixadas`: Lista de GUIDs de embaixadas para filtrar as verificações

**Exemplo de dados no DynamoDB**:
```json
{
  "IdEmbaixadas": {
    "SS": [
      "7530416f-b46f-4048-8d46-0c3702226bea",
      "7f09e5e3-2a0f-4a07-8320-a894c2bb04dd",
      "8977b9f0-4e31-49c0-b628-e00aaaf5bcbb",
      "df044ecc-51e6-47e7-99f5-dc151e5979d8"
    ]
  }
}
```

### 2. Interface IDynamoDbService

**Arquivo**: `Interfaces/IDynamoDbService.cs`

```csharp
Task<List<Verificacao>> GetVerificacoesPorEmbaixadasAsync(List<string> idEmbaixadas);
```

**Novo método**:
- `GetVerificacoesPorEmbaixadasAsync`: Busca verificações filtradas por lista de embaixadas

### 3. DynamoDbService

**Arquivo**: `Services/DynamoDbService.cs`

**Novo método implementado**:
```csharp
public async Task<List<Verificacao>> GetVerificacoesPorEmbaixadasAsync(List<string> idEmbaixadas)
{
    // Buscar todas as verificações
    var todasVerificacoes = await GetAllAsync<Verificacao>();
    
    // Filtrar verificações que têm pelo menos uma embaixada em comum
    var verificacoesFiltradas = todasVerificacoes
        .Where(v => v.IdEmbaixadas != null && 
                   v.IdEmbaixadas.Any() && 
                   v.IdEmbaixadas.Any(idEmb => idEmbaixadas.Contains(idEmb)))
        .ToList();
    
    return verificacoesFiltradas;
}
```

**Lógica de filtragem**:
- Busca todas as verificações (SCAN)
- Filtra localmente as verificações que têm `IdEmbaixadas` preenchido
- Retorna apenas verificações que têm **pelo menos uma embaixada em comum** com a lista fornecida

### 4. ProcessorService

**Arquivo**: `Services/ProcessorService.cs`

**Lógica atualizada**:
```csharp
if (execucao.Validacoes != null && execucao.Validacoes.Any())
{
    // Usar validações específicas
    validacoesParaProcessar = execucao.Validacoes;
}
else if (_processamentoConfig.BuscarTodasVerificacoesSeVazio)
{
    if (execucao.IdEmbaixadas != null && execucao.IdEmbaixadas.Any())
    {
        // Buscar verificações filtradas por embaixadas
        todasVerificacoes = await _dynamoDbService.GetVerificacoesPorEmbaixadasAsync(execucao.IdEmbaixadas);
    }
    else
    {
        // Buscar todas as verificações
        todasVerificacoes = await _dynamoDbService.GetTodasVerificacoesAsync();
    }
    
    validacoesParaProcessar = todasVerificacoes.Select(v => v.Id).ToList();
}
```

## Fluxo de Decisão

```
1. Execução recebida
   ↓
2. Tem validações específicas?
   ├─ SIM → Usar validações da execução
   └─ NÃO → Continua...
   ↓
3. Configuração permite buscar verificações?
   ├─ NÃO → Não processa nada
   └─ SIM → Continua...
   ↓
4. Tem lista de embaixadas?
   ├─ SIM → Buscar verificações filtradas por embaixadas
   └─ NÃO → Buscar todas as verificações
   ↓
5. Processar verificações encontradas
```

## Cenários de Uso

### Cenário 1: Validações Específicas (Prioridade)
```json
{
  "Validacoes": ["verif-1", "verif-2", "verif-3"],
  "IdEmbaixadas": ["emb-1", "emb-2"]
}
```
**Resultado**: Processa apenas `verif-1`, `verif-2`, `verif-3` (ignora IdEmbaixadas)

### Cenário 2: Filtro por Embaixadas
```json
{
  "Validacoes": [],
  "IdEmbaixadas": ["emb-1", "emb-2", "emb-3"]
}
```
**Resultado**: Busca todas as verificações que têm pelo menos uma das embaixadas `emb-1`, `emb-2` ou `emb-3`

### Cenário 3: Sem Filtros
```json
{
  "Validacoes": [],
  "IdEmbaixadas": []
}
```
**Resultado**: Busca TODAS as verificações disponíveis

### Cenário 4: Sem Validações + Sem Configuração
```json
{
  "Validacoes": [],
  "IdEmbaixadas": ["emb-1"]
}
```
**Configuração**: `BuscarTodasVerificacoesSeVazio = false`
**Resultado**: Não processa nada

## Logs Gerados

### Filtro por Embaixadas Ativo
```
[INFO] Execução sem validações específicas. Buscando verificações filtradas por 4 embaixadas
[DEBUG] Embaixadas da execução: 7530416f-b46f-4048-8d46-0c3702226bea, 7f09e5e3-2a0f-4a07-8320-a894c2bb04dd, ...
[INFO] Buscando verificações filtradas por 4 embaixadas
[DEBUG] IDs de Embaixadas: 7530416f-b46f-4048-8d46-0c3702226bea, 7f09e5e3-2a0f-4a07-8320-a894c2bb04dd, ...
[INFO] Encontradas 25 verificações relacionadas às embaixadas especificadas (de 150 verificações totais)
[DEBUG] Verificações encontradas:
[DEBUG] - ID: verif-123 | Nome: Verificação A | Embaixadas em comum: 2
[DEBUG] - ID: verif-456 | Nome: Verificação B | Embaixadas em comum: 1
[DEBUG] ... e mais 23 verificações
```

### Todas as Verificações (Sem Filtro)
```
[INFO] Execução sem validações específicas e sem filtro de embaixadas. Buscando todas as verificações disponíveis
[INFO] Encontradas 150 verificações no total
```

### Nenhuma Verificação Encontrada
```
[INFO] Buscando verificações filtradas por 2 embaixadas
[WARNING] Nenhuma verificação encontrada para as embaixadas especificadas
```

## Lógica de Filtragem

### Critério de Matching

Uma verificação é incluída no resultado se:
1. Campo `IdEmbaixadas` da verificação **não é nulo**
2. Campo `IdEmbaixadas` da verificação **não está vazio**
3. **Pelo menos um** ID de embaixada da verificação está na lista da execução

### Exemplo Prático

**Execução**:
```json
{
  "IdEmbaixadas": ["emb-A", "emb-B", "emb-C"]
}
```

**Verificações na tabela**:
```json
// Verificação 1 - INCLUÍDA (tem emb-A)
{
  "Id": "verif-1",
  "IdEmbaixadas": ["emb-A", "emb-X"]
}

// Verificação 2 - INCLUÍDA (tem emb-B e emb-C)
{
  "Id": "verif-2",
  "IdEmbaixadas": ["emb-B", "emb-C", "emb-Y"]
}

// Verificação 3 - EXCLUÍDA (não tem nenhuma em comum)
{
  "Id": "verif-3",
  "IdEmbaixadas": ["emb-X", "emb-Y", "emb-Z"]
}

// Verificação 4 - EXCLUÍDA (campo vazio)
{
  "Id": "verif-4",
  "IdEmbaixadas": []
}
```

**Resultado**: `verif-1` e `verif-2` serão processadas

## Performance

### Operação no DynamoDB
- **Tipo**: SCAN completo da tabela `Verificacoes`
- **Filtro**: Aplicado em memória (lado da aplicação)

### Considerações
1. **SCAN é lento**: Varre toda a tabela
2. **Filtro local**: Aplicado após buscar todos os dados
3. **Otimização futura**: Considerar índice secundário se o volume for grande

### Recomendações
- Para **poucos registros** (< 1000): Abordagem atual é adequada
- Para **muitos registros** (> 1000): Considerar criar GSI (Global Secondary Index) em `IdEmbaixadas`

## Compatibilidade

### Backwards Compatible
Esta mudança é **100% compatível** com execuções existentes:
- Execuções sem campo `IdEmbaixadas` continuam funcionando normalmente
- Campo `IdEmbaixadas` vazio é tratado como "sem filtro"
- Lógica anterior de buscar todas as verificações continua disponível

### Prioridade de Filtros
1. **Prioridade ALTA**: `Validacoes` (lista específica)
2. **Prioridade MÉDIA**: `IdEmbaixadas` (filtro por embaixadas)
3. **Prioridade BAIXA**: Buscar todas (sem filtros)

## Testes

### Teste 1: Filtro por Embaixadas
```json
{
  "Id": "exec-123",
  "Validacoes": [],
  "IdEmbaixadas": ["7530416f-b46f-4048-8d46-0c3702226bea"]
}
```
**Resultado Esperado**: Apenas verificações relacionadas à embaixada especificada

### Teste 2: Múltiplas Embaixadas
```json
{
  "Id": "exec-456",
  "Validacoes": [],
  "IdEmbaixadas": [
    "7530416f-b46f-4048-8d46-0c3702226bea",
    "7f09e5e3-2a0f-4a07-8320-a894c2bb04dd"
  ]
}
```
**Resultado Esperado**: Verificações que têm qualquer uma das embaixadas

### Teste 3: Validações Específicas Prevalece
```json
{
  "Id": "exec-789",
  "Validacoes": ["verif-specific"],
  "IdEmbaixadas": ["emb-1", "emb-2"]
}
```
**Resultado Esperado**: Apenas `verif-specific` (ignora IdEmbaixadas)

## Benefícios

1. **Processamento Otimizado**: Processa apenas verificações relevantes
2. **Flexibilidade**: Permite filtrar por embaixadas sem especificar todas as verificações
3. **Performance**: Reduz quantidade de verificações processadas
4. **Manutenibilidade**: Não precisa atualizar lista de validações quando novas são adicionadas

## Próximos Passos (Futuro)

### Otimizações Possíveis
1. **GSI no DynamoDB**: Criar índice secundário em `IdEmbaixadas`
2. **Cache**: Implementar cache de verificações por embaixada
3. **Query em vez de SCAN**: Se GSI for criado
4. **Filtros Adicionais**: Adicionar filtros por tipo, nível, etc.

### Melhorias Futuras
1. Suporte a operadores lógicos (AND/OR) para embaixadas
2. Exclusão de embaixadas específicas
3. Combinação de múltiplos filtros
4. API para consultar verificações disponíveis por embaixada

## Conclusão

Esta funcionalidade adiciona flexibilidade ao sistema, permitindo que execuções processem apenas verificações relevantes para embaixadas específicas, sem necessidade de especificar manualmente cada verificação. O sistema mantém compatibilidade com comportamentos anteriores enquanto oferece uma opção de filtragem mais inteligente.




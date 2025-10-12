# Correção: Nivel = 0 no ResultadoAgregado

## Problema Identificado

Registros no `ResultadoAgregado` apareciam com `Nivel = 0`, mas Nivel 0 não é válido no sistema (válidos: 1-5).

## Causa Raiz

### **Código Problemático**

```csharp
// ❌ ERRO 1: Estava usando .N (Number) mas Nivel é String no DynamoDB
Nivel = item.ContainsKey("Nivel") && int.TryParse(item["Nivel"].N, out var nivel) ? nivel : 0
                                                                 ↑                         ↑
                                                            ERRO TIPO                ERRO DEFAULT
```

**Problemas**:
1. ❌ Campo `Nivel` é **String (S)** no DynamoDB, não Number (N)
2. ❌ Parse sempre falhava porque tentava acessar `.N` em um campo String
3. ❌ Retornava `0` como padrão (valor inválido)

**Cenários que causavam Nivel = 0**:
1. Parse falhava porque campo é String mas código esperava Number
2. Campo `Nivel` não existe no item
3. Valor padrão era 0 (inválido)

---

## Solução Implementada

### **Código Corrigido**

```csharp
// ✅ Parsear Nivel com validação (campo é String no DynamoDB)
int nivelParsed = 1; // Padrão: Nivel 1

if (item.ContainsKey("Nivel"))
{
    // ✅ CORREÇÃO: Usar .S (String) ao invés de .N (Number)
    if (int.TryParse(item["Nivel"].S, out var nivelTemp))
    {
        nivelParsed = nivelTemp;
        
        // ✅ VALIDAÇÃO: Nivel deve estar entre 1 e 5
        if (nivelParsed < 1 || nivelParsed > 5)
        {
            _logger.LogWarning(
                "Nivel fora do range valido (1-5): {Nivel}. Usando Nivel=1 como padrao. PK={PK}",
                nivelParsed, item["PK"].S);
            nivelParsed = 1;  // Corrige para valor válido
        }
    }
    else
    {
        _logger.LogWarning(
            "Falha ao parsear Nivel (valor: {Valor}). Usando Nivel=1 como padrao. PK={PK}",
            item["Nivel"].S, item["PK"].S);
    }
}
else
{
    _logger.LogDebug(
        "Campo Nivel nao encontrado no item. Usando Nivel=1 como padrao. PK={PK}",
        item["PK"].S);
}

var resultado = new Resultado
{
    // ...
    Nivel = nivelParsed,  // ← VALIDADO (sempre entre 1-5)
    // ...
};
```

### **Mudanças Principais**

1. ✅ **Tipo correto**: `.S` (String) ao invés de `.N` (Number)
2. ✅ **Validação de range**: 1 a 5 (ao invés de apenas > 0)
3. ✅ **Log melhorado**: Mostra valor que falhou no parse
4. ✅ **Padrão válido**: 1 (nível mais baixo)

---

## Lógica de Validação

```
1. Campo Nivel existe?
   ├─ NÃO → Nivel = 1 (padrão) + Log DEBUG
   └─ SIM → Continua...

2. Parse bem-sucedido (de String para int)?
   ├─ NÃO → Nivel = 1 (padrão) + Log WARNING (mostra valor)
   └─ SIM → Continua...

3. Valor está no range válido (1-5)?
   ├─ NÃO → Nivel = 1 (padrão) + Log WARNING (mostra valor)
   └─ SIM → Usa valor parseado ✅
```

### **Diferença Importante**

**Antes**: Tentava ler como Number (`.N`) → **Parse sempre falhava**
**Depois**: Lê como String (`.S`) → **Parse funciona corretamente**

---

## Logs Gerados

### **Nivel Fora do Range (< 1 ou > 5)**
```
[WARNING] Nivel fora do range valido (1-5): 0. Usando Nivel=1 como padrao. PK=EXEC#abc-123#VERIF#...
[WARNING] Nivel fora do range valido (1-5): 7. Usando Nivel=1 como padrao. PK=EXEC#abc-123#VERIF#...
```

### **Parse Falhou (não é número)**
```
[WARNING] Falha ao parsear Nivel (valor: "abc"). Usando Nivel=1 como padrao. PK=EXEC#abc-123#VERIF#...
[WARNING] Falha ao parsear Nivel (valor: ""). Usando Nivel=1 como padrao. PK=EXEC#abc-123#VERIF#...
```

### **Campo Não Existe**
```
[DEBUG] Campo Nivel nao encontrado no item. Usando Nivel=1 como padrao. PK=EXEC#abc-123#VERIF#...
```

---

## Valores Padrão

### **Nivel Padrão**
```
Valor: 1 (Nível mais baixo/básico)
Motivo: Valor seguro e válido
```

### **Outros Valores Possíveis**
Se quiser usar outro padrão:
```csharp
int nivelParsed = 3;  // Ou 2, ou qualquer outro valor válido
```

---

## Exemplo de Correção

### **Item da Tabela Resultado**

#### **Cenário 1: Nivel = 0 (inválido)**
```json
{
  "PK": "EXEC#abc-123#VERIF#verif-456",
  "Nivel": 0  ← Inválido
}
```

**Resultado**:
- Log WARNING
- Nivel corrigido para 1
- ResultadoAgregado salvo com Nivel = 1

#### **Cenário 2: Nivel ausente**
```json
{
  "PK": "EXEC#abc-123#VERIF#verif-456"
  // Nivel: não existe
}
```

**Resultado**:
- Log DEBUG
- Nivel definido como 1
- ResultadoAgregado salvo com Nivel = 1

#### **Cenário 3: Nivel válido**
```json
{
  "PK": "EXEC#abc-123#VERIF#verif-456",
  "Nivel": 2  ← Válido
}
```

**Resultado**:
- Sem log
- Nivel = 2 preservado
- ResultadoAgregado salvo com Nivel = 2

---

## Benefícios

### **1. Dados Consistentes**
- ✅ Nunca teremos Nivel = 0 no ResultadoAgregado
- ✅ Sempre valores válidos

### **2. Rastreabilidade**
- ✅ Logs WARNING quando Nivel inválido é encontrado
- ✅ Logs DEBUG quando Nivel está ausente
- ✅ Fácil identificar registros problemáticos

### **3. Resiliência**
- ✅ Sistema não quebra com dados inválidos
- ✅ Correção automática para valor válido
- ✅ Processamento continua normalmente

---

## Verificação de Dados

### **Na Tabela Resultado (Fonte)**

Se encontrar registros com Nivel = 0 ou ausente:
```
Ação recomendada:
1. Investigar por que o sistema externo não está preenchendo
2. Corrigir o sistema externo
3. Backfill dos dados (atualizar Nivel para valor correto)
```

### **No ResultadoAgregado**

Com a correção:
```
✅ Todos os registros terão Nivel >= 1
✅ Nivel = 1 indica valor padrão (pode ter vindo de dado inválido)
✅ Logs indicam quais registros tiveram valor corrigido
```

---

## Conclusão

A correção garante que:
- ✅ `Nivel` nunca será 0 no `ResultadoAgregado`
- ✅ Valor padrão válido (1) é usado quando necessário
- ✅ Logs detalhados para rastreabilidade
- ✅ Sistema resiliente a dados inválidos

**Status**: ✅ **CORREÇÃO IMPLEMENTADA**


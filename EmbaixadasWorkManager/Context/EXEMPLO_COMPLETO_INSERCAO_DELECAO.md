# Exemplo Completo: Inserção e Deleção no ResultadoAgregado

Este documento mostra **passo a passo** como funciona o fluxo de agregação, limpeza e inserção com exemplos práticos.

---

## 📋 **Cenário: Duas Execuções da Mesma Empresa**

### **Setup Inicial**:
- **Empresa MA** tem 2 embaixadas: `emb-001` e `emb-002`
- **Empresa PI** tem 1 embaixada: `emb-001`
- **Empresa RS** tem 1 embaixada: `emb-003`

---

## 🕐 **EXECUÇÃO 1 - Primeira Vez**

### **📄 Dados da Execução**:
```json
{
  "Id": "exec-abc-123",
  "Empresa": "MA,PI",           ← Solicitou MA e PI
  "IdEmbaixadas": ["emb-001", "emb-002"],
  "DataSolicitacao": "2025-10-11T10:00:00Z",
  "Status": "Cadastrado"
}
```

---

### **🔍 ETAPA 1: Buscar Apontamentos**

**Query GSI_Agregacao**:
```
GSI1_PK = "EXEC#exec-abc-123"
```

**Resultados** (5 apontamentos):
```
Apontamento 1:
  Empresa: MA
  IdEmbaixadas: ["emb-001"]
  Tabela: PIP, Campo: FAS_CON, Referencia: 123
  
Apontamento 2:
  Empresa: MA
  IdEmbaixadas: ["emb-001"]
  Tabela: PIP, Campo: FAS_CON, Referencia: 456
  
Apontamento 3:
  Empresa: PI
  IdEmbaixadas: ["emb-001", "emb-002"]  ← Tem 2 embaixadas!
  Tabela: PES, Campo: CPF, Referencia: 789
  
Apontamento 4:
  Empresa: RS  ← NÃO SOLICITADA!
  IdEmbaixadas: ["emb-003"]
  Tabela: CON, Campo: DTA, Referencia: 001
  
Apontamento 5:
  Empresa: RS  ← NÃO SOLICITADA!
  IdEmbaixadas: ["emb-003"]
  Tabela: CON, Campo: DTA, Referencia: 002
```

---

### **🔄 ETAPA 2: Agrupamento em Memória**

**Grupos Segmentados** (com coleta de IdEmbaixadas):
```csharp
gruposSegmentados = {
    "MA|verif-1|PIP|FAS_CON|123|ERRO|1": {
        Quantidade: 1,
        IdEmbaixadas: ["emb-001"]  ← Coletou de apontamento 1
    },
    "MA|verif-1|PIP|FAS_CON|456|ERRO|1": {
        Quantidade: 1,
        IdEmbaixadas: ["emb-001"]  ← Coletou de apontamento 2
    },
    "PI|verif-2|PES|CPF|789|AVISO|2": {
        Quantidade: 1,
        IdEmbaixadas: ["emb-001", "emb-002"]  ← Union! Coletou ambas
    },
    "RS|verif-3|CON|DTA|001|ERRO|1": {  ← Será IGNORADO
        Quantidade: 1,
        IdEmbaixadas: ["emb-003"]
    },
    "RS|verif-3|CON|DTA|002|ERRO|1": {  ← Será IGNORADO
        Quantidade: 1,
        IdEmbaixadas: ["emb-003"]
    }
}

Total: 5 grupos (3 de MA/PI + 2 de RS que serão ignorados)
```

**Grupos Globais** (apenas contagem):
```csharp
gruposGlobais = {
    "MA|verif-1|PIP|FAS_CON|123|ERRO|1": { Quantidade: 1 },
    "MA|verif-1|PIP|FAS_CON|456|ERRO|1": { Quantidade: 1 },
    "PI|verif-2|PES|CPF|789|AVISO|2": { Quantidade: 1 },
    "RS|verif-3|CON|DTA|001|ERRO|1": { Quantidade: 1 },  ← Será IGNORADO
    "RS|verif-3|CON|DTA|002|ERRO|1": { Quantidade: 1 }   ← Será IGNORADO
}

Total: 5 grupos
```

**LOG**:
```
[INFO] Agrupamento concluido. Segmentados: 5 grupos, Globais: 5 grupos, Total: 5 apontamentos, Empresas: 3, Embaixadas: 3
```

---

### **🗑️ ETAPA 2.5: Limpeza**

**Empresas solicitadas para limpar**: `["MA", "PI"]`

#### **Limpeza de Registros GLOBAIS**:

**Para Empresa MA**:
```
Query GSI_Empresa:
  IndexName: "GSI_Empresa"
  GSI2_PK = "EMP#MA"
  FilterExpression: PK ends_with "#EMB#GLOBAL"

Resultado: 0 registros (tabela vazia - primeira execução)

LOG: [DEBUG] Empresa MA (GLOBAL): Encontrados 0 registros para deletar
```

**Para Empresa PI**:
```
Query GSI_Empresa:
  GSI2_PK = "EMP#PI"
  FilterExpression: PK ends_with "#EMB#GLOBAL"

Resultado: 0 registros

LOG: [DEBUG] Empresa PI (GLOBAL): Encontrados 0 registros para deletar
```

#### **Limpeza de Registros SEGMENTADOS**:

**Para Empresa MA**:
```
Query GSI_Empresa:
  IndexName: "GSI_Empresa"
  GSI2_PK = "EMP#MA"
  Filter: PK contains "#EMB#" AND NOT ends_with "#EMB#GLOBAL"

Resultado: 0 registros

LOG: [DEBUG] Empresa MA (SEGMENTADOS): Encontrados 0 registros para deletar
```

**Para Empresa PI**:
```
Query GSI_Empresa:
  GSI2_PK = "EMP#PI"

Resultado: 0 registros

LOG: [DEBUG] Empresa PI (SEGMENTADOS): Encontrados 0 registros para deletar
```

**LOG FINAL**:
```
[INFO] Limpeza concluida. 0 registros deletados. Empresas: MA, PI
```

---

### **➕ ETAPA 3: Inserção SEGMENTADA**

**Empresas solicitadas**: `["MA", "PI"]`

#### **Grupo 1: MA|PIP|FAS_CON|123**
```
IdEmbaixadas: ["emb-001"]
Empresa: MA (solicitada ✅)

REPLICAÇÃO para emb-001:
→ Cria 1 registro:
  {
    "PK": "EXEC#exec-abc-123#EMB#emb-001",
    "SK": "EMP#MA#VER#verif-1#PIP#FAS_CON#123#ERRO",
    "GSI2_PK": "EMP#MA",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-1#PIP#FAS_CON#123",
    "QTD": 1,
    "Empresa": "MA"
  }
```

#### **Grupo 2: MA|PIP|FAS_CON|456**
```
IdEmbaixadas: ["emb-001"]
Empresa: MA (solicitada ✅)

REPLICAÇÃO para emb-001:
→ Cria 1 registro:
  {
    "PK": "EXEC#exec-abc-123#EMB#emb-001",
    "SK": "EMP#MA#VER#verif-1#PIP#FAS_CON#456#ERRO",
    "GSI2_PK": "EMP#MA",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-1#PIP#FAS_CON#456",
    "QTD": 1,
    "Empresa": "MA"
  }
```

#### **Grupo 3: PI|PES|CPF|789**
```
IdEmbaixadas: ["emb-001", "emb-002"]  ← 2 embaixadas!
Empresa: PI (solicitada ✅)

REPLICAÇÃO para emb-001:
→ Cria 1 registro:
  {
    "PK": "EXEC#exec-abc-123#EMB#emb-001",
    "SK": "EMP#PI#VER#verif-2#PES#CPF#789#AVISO",
    "GSI2_PK": "EMP#PI",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-2#PES#CPF#789",
    "QTD": 1,
    "Empresa": "PI"
  }

REPLICAÇÃO para emb-002:
→ Cria 1 registro:
  {
    "PK": "EXEC#exec-abc-123#EMB#emb-002",  ← Embaixada diferente!
    "SK": "EMP#PI#VER#verif-2#PES#CPF#789#AVISO",
    "GSI2_PK": "EMP#PI",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-2#PES#CPF#789",
    "QTD": 1,
    "Empresa": "PI"
  }
```

#### **Grupos 4 e 5: RS** (IGNORADOS)
```
Empresa: RS (NÃO solicitada ❌)

FILTRO: empresaFoiSolicitada = false
→ continue (PULA)

LOG: [DEBUG] Grupo IGNORADO (empresa nao solicitada): Empresa=RS
LOG: [DEBUG] Grupo IGNORADO (empresa nao solicitada): Empresa=RS
```

**Total SEGMENTADO**: 4 registros inseridos (MA=2, PI=2)

**LOG**:
```
[INFO] Insercao SEGMENTADA concluida. 4 registros inseridos, 2 grupos ignorados (de 5 grupos base, replicados 4 vezes)
```

---

### **➕ ETAPA 4: Inserção GLOBAL**

#### **Grupo 1: MA|PIP|FAS_CON|123**
```
Empresa: MA (solicitada ✅)

Cria 1 registro GLOBAL:
  {
    "PK": "EXEC#exec-abc-123#EMB#GLOBAL",  ← GLOBAL
    "SK": "EMP#MA#VER#verif-1#PIP#FAS_CON#123#ERRO",
    "GSI2_PK": "EMP#MA",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-1#PIP#FAS_CON#123",
    "QTD": 1,
    "Empresa": "MA"
  }
```

#### **Grupo 2: MA|PIP|FAS_CON|456**
```
Empresa: MA (solicitada ✅)

Cria 1 registro GLOBAL:
  {
    "PK": "EXEC#exec-abc-123#EMB#GLOBAL",
    "SK": "EMP#MA#VER#verif-1#PIP#FAS_CON#456#ERRO",
    "GSI2_PK": "EMP#MA",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-1#PIP#FAS_CON#456",
    "QTD": 1
  }
```

#### **Grupo 3: PI|PES|CPF|789**
```
Empresa: PI (solicitada ✅)

Cria 1 registro GLOBAL (SEM replicação por embaixada):
  {
    "PK": "EXEC#exec-abc-123#EMB#GLOBAL",
    "SK": "EMP#PI#VER#verif-2#PES#CPF#789#AVISO",
    "GSI2_PK": "EMP#PI",
    "GSI2_SK": "EXEC#exec-abc-123#VER#verif-2#PES#CPF#789",
    "QTD": 1
  }
```

#### **Grupos 4 e 5: RS** (IGNORADOS)
```
Empresa: RS (NÃO solicitada ❌)
→ PULA

LOG: [DEBUG] Grupo GLOBAL IGNORADO (empresa nao solicitada): Empresa=RS
```

**Total GLOBAL**: 3 registros inseridos

**LOG**:
```
[INFO] Insercao GLOBAL concluida. 3 registros inseridos, 2 grupos ignorados (de 5 grupos base)
```

---

### **📊 Estado da Tabela ResultadoAgregado (Final T1)**

```
╔════════════════════════════════════════════════════════════════════════════╗
║                          REGISTROS SEGMENTADOS                             ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-001                                         ║
║ SK: EMP#MA#VER#verif-1#PIP#FAS_CON#123#ERRO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-001                                         ║
║ SK: EMP#MA#VER#verif-1#PIP#FAS_CON#456#ERRO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-001                                         ║
║ SK: EMP#PI#VER#verif-2#PES#CPF#789#AVISO                                 ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-002                                         ║
║ SK: EMP#PI#VER#verif-2#PES#CPF#789#AVISO                                 ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╚════════════════════════════════════════════════════════════════════════════╝

╔════════════════════════════════════════════════════════════════════════════╗
║                           REGISTROS GLOBAIS (Admin)                        ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#GLOBAL                                          ║
║ SK: EMP#MA#VER#verif-1#PIP#FAS_CON#123#ERRO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#GLOBAL                                          ║
║ SK: EMP#MA#VER#verif-1#PIP#FAS_CON#456#ERRO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#GLOBAL                                          ║
║ SK: EMP#PI#VER#verif-2#PES#CPF#789#AVISO                                 ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╚════════════════════════════════════════════════════════════════════════════╝

TOTAL: 7 registros
- 4 SEGMENTADOS (MA emb-001: 2, PI emb-001: 1, PI emb-002: 1)
- 3 GLOBAIS (MA: 2, PI: 1)
- RS: 0 (ignorado - não solicitado)
```

---

## 🕑 **EXECUÇÃO 1 - REPROCESSAMENTO** (Dados Completamente Diferentes!)

### **📄 Dados da Execução**:
```json
{
  "Id": "exec-abc-123",          ← MESMO ID!
  "Empresa": "MA,PI",            ← Mesmas empresas
  "IdEmbaixadas": ["emb-001", "emb-002"],
  "DataSolicitacao": "2025-10-11T10:00:00Z",
  "Status": "Cadastrado"
}
```

---

### **🔍 ETAPA 1: Buscar Apontamentos** (NOVOS!)

**Resultados** (3 apontamentos - DIFERENTES da primeira vez!):
```
Apontamento 1:
  Empresa: MA
  IdEmbaixadas: ["emb-002"]  ← Agora só emb-002!
  Tabela: CON, Campo: NUM, Referencia: 999  ← Tabela/Campo DIFERENTES!
  TipoApontamento: CRITICO  ← Tipo DIFERENTE!
  
Apontamento 2:
  Empresa: PI
  IdEmbaixadas: ["emb-002"]
  Tabela: PES, Campo: RG, Referencia: 888  ← Campo DIFERENTE (era CPF)!
  TipoApontamento: BLOQUEIO  ← Tipo DIFERENTE!
  
Apontamento 3:
  Empresa: AL  ← NÃO SOLICITADA!
  IdEmbaixadas: ["emb-004"]
  Tabela: XXX, Campo: YYY, Referencia: 777
```

---

### **🔄 ETAPA 2: Agrupamento**

**Grupos Segmentados**:
```csharp
gruposSegmentados = {
    "MA|verif-10|CON|NUM|999|CRITICO|3": {  ← Novo grupo (tipo diferente!)
        Quantidade: 1,
        IdEmbaixadas: ["emb-002"]  ← Só emb-002 agora
    },
    "PI|verif-11|PES|RG|888|BLOQUEIO|2": {  ← Novo grupo (campo diferente!)
        Quantidade: 1,
        IdEmbaixadas: ["emb-002"]
    },
    "AL|verif-12|XXX|YYY|777|ERRO|1": {  ← Será IGNORADO
        Quantidade: 1,
        IdEmbaixadas: ["emb-004"]
    }
}
```

**Grupos Globais**:
```csharp
gruposGlobais = {
    "MA|verif-10|CON|NUM|999|CRITICO|3": { Quantidade: 1 },
    "PI|verif-11|PES|RG|888|BLOQUEIO|2": { Quantidade: 1 },
    "AL|verif-12|XXX|YYY|777|ERRO|1": { Quantidade: 1 }  ← Será IGNORADO
}
```

**LOG**:
```
[INFO] Agrupamento concluido. Segmentados: 3 grupos, Globais: 3 grupos, Total: 3 apontamentos
```

---

### **🗑️ ETAPA 2.5: Limpeza** ✅ **CRÍTICO AQUI!**

**Empresas solicitadas**: `["MA", "PI"]`

#### **Limpeza de Registros GLOBAIS**:

**Para Empresa MA**:
```
Query GSI_Empresa:
  GSI2_PK = "EMP#MA"
  Filter: PK ends_with "#EMB#GLOBAL"

Resultado: 2 registros ANTIGOS (da execução T1):
  1. PK=EXEC#exec-abc-123#EMB#GLOBAL, SK=EMP#MA#...#PIP#FAS_CON#123#ERRO
  2. PK=EXEC#exec-abc-123#EMB#GLOBAL, SK=EMP#MA#...#PIP#FAS_CON#456#ERRO

BatchWriteItem (DeleteRequest):
  → DELETE 2 registros

LOG: [DEBUG] Batch de 2 registros deletados (GLOBAL:MA)
```

**Para Empresa PI**:
```
Query GSI_Empresa:
  GSI2_PK = "EMP#PI"
  Filter: PK ends_with "#EMB#GLOBAL"

Resultado: 1 registro ANTIGO:
  1. PK=EXEC#exec-abc-123#EMB#GLOBAL, SK=EMP#PI#...#PES#CPF#789#AVISO

BatchWriteItem (DeleteRequest):
  → DELETE 1 registro

LOG: [DEBUG] Batch de 1 registros deletados (GLOBAL:PI)
```

#### **Limpeza de Registros SEGMENTADOS**:

**Para Empresa MA**:
```
Query GSI_Empresa:
  GSI2_PK = "EMP#MA"
  Filter: PK contains "#EMB#" AND NOT ends_with "#GLOBAL"

Resultado: 2 registros ANTIGOS:
  1. PK=EXEC#exec-abc-123#EMB#emb-001, SK=EMP#MA#...#PIP#FAS_CON#123#ERRO
  2. PK=EXEC#exec-abc-123#EMB#emb-001, SK=EMP#MA#...#PIP#FAS_CON#456#ERRO

BatchWriteItem (DeleteRequest):
  → DELETE 2 registros

LOG: [DEBUG] Batch de 2 registros deletados (SEGMENTADO:MA)
```

**Para Empresa PI**:
```
Query GSI_Empresa:
  GSI2_PK = "EMP#PI"

Resultado: 2 registros ANTIGOS:
  1. PK=EXEC#exec-abc-123#EMB#emb-001, SK=EMP#PI#...#PES#CPF#789#AVISO
  2. PK=EXEC#exec-abc-123#EMB#emb-002, SK=EMP#PI#...#PES#CPF#789#AVISO

BatchWriteItem (DeleteRequest):
  → DELETE 2 registros

LOG: [DEBUG] Batch de 2 registros deletados (SEGMENTADO:PI)
```

**LOG FINAL**:
```
[INFO] Limpeza concluida. 7 registros deletados. Empresas: MA, PI
```

**📊 Estado da Tabela Após Limpeza**:
```
VAZIA! (Todos os registros de MA e PI deletados)
```

---

### **➕ ETAPA 3: Inserção SEGMENTADA** (Dados Novos)

#### **Grupo 1: MA|CON|NUM|999|CRITICO**
```
IdEmbaixadas: ["emb-002"]
Empresa: MA (solicitada ✅)

REPLICAÇÃO para emb-002:
→ Cria 1 registro:
  {
    "PK": "EXEC#exec-abc-123#EMB#emb-002",  ← emb-002 (era emb-001 antes!)
    "SK": "EMP#MA#VER#verif-10#CON#NUM#999#CRITICO",  ← Tipo diferente!
    "GSI2_PK": "EMP#MA",
    "QTD": 1
  }
```

#### **Grupo 2: PI|PES|RG|888|BLOQUEIO**
```
IdEmbaixadas: ["emb-002"]
Empresa: PI (solicitada ✅)

REPLICAÇÃO para emb-002:
→ Cria 1 registro:
  {
    "PK": "EXEC#exec-abc-123#EMB#emb-002",
    "SK": "EMP#PI#VER#verif-11#PES#RG#888#BLOQUEIO",  ← Campo diferente!
    "GSI2_PK": "EMP#PI",
    "QTD": 1
  }
```

#### **Grupo 3: AL** (IGNORADO)
```
Empresa: AL (NÃO solicitada ❌)
→ PULA

LOG: [DEBUG] Grupo IGNORADO (empresa nao solicitada): Empresa=AL
```

**Total SEGMENTADO**: 2 registros inseridos

**LOG**:
```
[INFO] Insercao SEGMENTADA concluida. 2 registros inseridos, 1 grupo ignorado
```

---

### **➕ ETAPA 4: Inserção GLOBAL**

#### **Grupo 1: MA|CON|NUM|999|CRITICO**
```
Cria 1 registro GLOBAL:
  {
    "PK": "EXEC#exec-abc-123#EMB#GLOBAL",
    "SK": "EMP#MA#VER#verif-10#CON#NUM#999#CRITICO",
    "GSI2_PK": "EMP#MA",
    "QTD": 1
  }
```

#### **Grupo 2: PI|PES|RG|888|BLOQUEIO**
```
Cria 1 registro GLOBAL:
  {
    "PK": "EXEC#exec-abc-123#EMB#GLOBAL",
    "SK": "EMP#PI#VER#verif-11#PES#RG#888#BLOQUEIO",
    "GSI2_PK": "EMP#PI",
    "QTD": 1
  }
```

**Total GLOBAL**: 2 registros inseridos

**LOG**:
```
[INFO] Insercao GLOBAL concluida. 2 registros inseridos, 1 grupo ignorado
```

---

### **📊 Estado da Tabela ResultadoAgregado (Final T2)**

```
╔════════════════════════════════════════════════════════════════════════════╗
║                          REGISTROS SEGMENTADOS                             ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-002  ← Mudou de emb-001 para emb-002!      ║
║ SK: EMP#MA#VER#verif-10#CON#NUM#999#CRITICO  ← Tipo diferente!           ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-002                                         ║
║ SK: EMP#PI#VER#verif-11#PES#RG#888#BLOQUEIO  ← Campo diferente!          ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╚════════════════════════════════════════════════════════════════════════════╝

╔════════════════════════════════════════════════════════════════════════════╗
║                           REGISTROS GLOBAIS (Admin)                        ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#GLOBAL                                          ║
║ SK: EMP#MA#VER#verif-10#CON#NUM#999#CRITICO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#GLOBAL                                          ║
║ SK: EMP#PI#VER#verif-11#PES#RG#888#BLOQUEIO                              ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╚════════════════════════════════════════════════════════════════════════════╝

TOTAL: 4 registros (2 segmentados + 2 globais)
- Dados ANTIGOS (PIP#FAS_CON, PES#CPF): DELETADOS ✅
- Dados NOVOS (CON#NUM, PES#RG): INSERIDOS ✅
```

---

## 🕒 **EXECUÇÃO 2 - Outras Empresas**

### **📄 Dados da Execução**:
```json
{
  "Id": "exec-xyz-789",          ← ID DIFERENTE!
  "Empresa": "PI,RS",            ← Solicitou PI e RS
  "IdEmbaixadas": ["emb-001", "emb-003"],
  "DataSolicitacao": "2025-10-11T12:00:00Z"
}
```

---

### **🔍 ETAPA 1: Buscar Apontamentos**

**Resultados** (4 apontamentos):
```
Apontamento 1:
  Empresa: PI
  IdEmbaixadas: ["emb-001"]
  Tabela: PIP, Campo: STATUS, Referencia: AAA
  
Apontamento 2:
  Empresa: RS
  IdEmbaixadas: ["emb-003"]
  Tabela: PES, Campo: NOME, Referencia: BBB
  
Apontamento 3:
  Empresa: RS
  IdEmbaixadas: ["emb-003"]
  Tabela: PES, Campo: NOME, Referencia: CCC
  
Apontamento 4:
  Empresa: MA  ← NÃO SOLICITADA (exec2 solicitou PI e RS)!
  IdEmbaixadas: ["emb-001"]
  Tabela: XXX, Campo: YYY, Referencia: DDD
```

---

### **🔄 ETAPA 2: Agrupamento**

**Grupos Segmentados**:
```csharp
gruposSegmentados = {
    "PI|verif-20|PIP|STATUS|AAA|ERRO|1": {
        Quantidade: 1,
        IdEmbaixadas: ["emb-001"]
    },
    "RS|verif-21|PES|NOME|BBB|AVISO|2": {
        Quantidade: 1,
        IdEmbaixadas: ["emb-003"]
    },
    "RS|verif-21|PES|NOME|CCC|AVISO|2": {
        Quantidade: 1,
        IdEmbaixadas: ["emb-003"]
    },
    "MA|verif-22|XXX|YYY|DDD|ERRO|1": {  ← Será IGNORADO
        Quantidade: 1,
        IdEmbaixadas: ["emb-001"]
    }
}
```

**LOG**:
```
[INFO] Agrupamento concluido. Segmentados: 4 grupos, Globais: 4 grupos, Total: 4 apontamentos
```

---

### **🗑️ ETAPA 2.5: Limpeza**

**Empresas solicitadas**: `["PI", "RS"]`

#### **Limpeza GLOBAL**:

**Para PI**:
```
Query GSI_Empresa: GSI2_PK = "EMP#PI"
Filter: PK ends_with "#EMB#GLOBAL"

Resultado: 1 registro ANTIGO (da exec-abc-123):
  PK=EXEC#exec-abc-123#EMB#GLOBAL, SK=EMP#PI#...#PES#RG#888#BLOQUEIO

DELETE: 1 registro

LOG: [DEBUG] Empresa PI (GLOBAL): Encontrados 1 registro para deletar
LOG: [DEBUG] Batch de 1 registros deletados (GLOBAL:PI)
```

**Para RS**:
```
Query GSI_Empresa: GSI2_PK = "EMP#RS"
Filter: PK ends_with "#EMB#GLOBAL"

Resultado: 0 registros (RS nunca foi processada)

LOG: [DEBUG] Empresa RS (GLOBAL): Encontrados 0 registros para deletar
```

#### **Limpeza SEGMENTADA**:

**Para PI**:
```
Query GSI_Empresa: GSI2_PK = "EMP#PI"
Filter: PK contains "#EMB#" AND NOT ends_with "#GLOBAL"

Resultado: 1 registro ANTIGO (da exec-abc-123):
  PK=EXEC#exec-abc-123#EMB#emb-002, SK=EMP#PI#...#PES#RG#888#BLOQUEIO

DELETE: 1 registro

LOG: [DEBUG] Empresa PI (SEGMENTADOS): Encontrados 1 registro para deletar
LOG: [DEBUG] Batch de 1 registros deletados (SEGMENTADO:PI)
```

**Para RS**:
```
Query GSI_Empresa: GSI2_PK = "EMP#RS"

Resultado: 0 registros

LOG: [DEBUG] Empresa RS (SEGMENTADOS): Encontrados 0 registros para deletar
```

**LOG FINAL**:
```
[INFO] Limpeza concluida. 2 registros deletados. Empresas: PI, RS
```

**📊 Estado Após Limpeza**:
```
Registros de PI (exec-abc-123): DELETADOS ✅
Registros de MA (exec-abc-123): MANTIDOS ✅ (MA não foi solicitada na exec2)
Registros de RS: 0 (nunca existiu)
```

---

### **➕ ETAPA 3: Inserção SEGMENTADA**

**Empresas solicitadas**: `["PI", "RS"]`

#### **Grupo 1: PI|PIP|STATUS|AAA**
```
Empresa: PI (solicitada ✅)
IdEmbaixadas: ["emb-001"]

→ Cria 1 registro:
  {
    "PK": "EXEC#exec-xyz-789#EMB#emb-001",  ← Nova execução!
    "SK": "EMP#PI#VER#verif-20#PIP#STATUS#AAA#ERRO",
    "GSI2_PK": "EMP#PI",
    "QTD": 1
  }
```

#### **Grupo 2: RS|PES|NOME|BBB**
```
Empresa: RS (solicitada ✅)
IdEmbaixadas: ["emb-003"]

→ Cria 1 registro:
  {
    "PK": "EXEC#exec-xyz-789#EMB#emb-003",
    "SK": "EMP#RS#VER#verif-21#PES#NOME#BBB#AVISO",
    "GSI2_PK": "EMP#RS",
    "QTD": 1
  }
```

#### **Grupo 3: RS|PES|NOME|CCC**
```
Empresa: RS (solicitada ✅)
IdEmbaixadas: ["emb-003"]

→ Cria 1 registro:
  {
    "PK": "EXEC#exec-xyz-789#EMB#emb-003",
    "SK": "EMP#RS#VER#verif-21#PES#NOME#CCC#AVISO",
    "GSI2_PK": "EMP#RS",
    "QTD": 1
  }
```

#### **Grupo 4: MA** (IGNORADO)
```
Empresa: MA (NÃO solicitada ❌)
→ PULA

LOG: [DEBUG] Grupo IGNORADO (empresa nao solicitada): Empresa=MA
```

**Total SEGMENTADO**: 3 registros inseridos

---

### **➕ ETAPA 4: Inserção GLOBAL**

```
Cria 3 registros GLOBAIS:

1. PK=EXEC#exec-xyz-789#EMB#GLOBAL, SK=EMP#PI#...#PIP#STATUS#AAA#ERRO, QTD=1
2. PK=EXEC#exec-xyz-789#EMB#GLOBAL, SK=EMP#RS#...#PES#NOME#BBB#AVISO, QTD=1
3. PK=EXEC#exec-xyz-789#EMB#GLOBAL, SK=EMP#RS#...#PES#NOME#CCC#AVISO, QTD=1

MA: IGNORADO

LOG: [INFO] Insercao GLOBAL concluida. 3 registros inseridos, 1 grupo ignorado
```

---

### **📊 Estado FINAL da Tabela ResultadoAgregado**

```
╔════════════════════════════════════════════════════════════════════════════╗
║              EXECUÇÃO 1 (exec-abc-123) - REPROCESSADA                      ║
╠════════════════════════════════════════════════════════════════════════════╣
║                          SEGMENTADOS                                       ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#emb-002                                         ║
║ SK: EMP#MA#VER#verif-10#CON#NUM#999#CRITICO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║                           GLOBAIS                                          ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-abc-123#EMB#GLOBAL                                          ║
║ SK: EMP#MA#VER#verif-10#CON#NUM#999#CRITICO                              ║
║ GSI2_PK: EMP#MA                                                            ║
║ QTD: 1                                                                     ║
╚════════════════════════════════════════════════════════════════════════════╝

╔════════════════════════════════════════════════════════════════════════════╗
║              EXECUÇÃO 2 (exec-xyz-789) - NOVA                              ║
╠════════════════════════════════════════════════════════════════════════════╣
║                          SEGMENTADOS                                       ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-xyz-789#EMB#emb-001                                         ║
║ SK: EMP#PI#VER#verif-20#PIP#STATUS#AAA#ERRO                              ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-xyz-789#EMB#emb-003                                         ║
║ SK: EMP#RS#VER#verif-21#PES#NOME#BBB#AVISO                               ║
║ GSI2_PK: EMP#RS                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-xyz-789#EMB#emb-003                                         ║
║ SK: EMP#RS#VER#verif-21#PES#NOME#CCC#AVISO                               ║
║ GSI2_PK: EMP#RS                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║                           GLOBAIS                                          ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-xyz-789#EMB#GLOBAL                                          ║
║ SK: EMP#PI#VER#verif-20#PIP#STATUS#AAA#ERRO                              ║
║ GSI2_PK: EMP#PI                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-xyz-789#EMB#GLOBAL                                          ║
║ SK: EMP#RS#VER#verif-21#PES#NOME#BBB#AVISO                               ║
║ GSI2_PK: EMP#RS                                                            ║
║ QTD: 1                                                                     ║
╠════════════════════════════════════════════════════════════════════════════╣
║ PK: EXEC#exec-xyz-789#EMB#GLOBAL                                          ║
║ SK: EMP#RS#VER#verif-21#PES#NOME#CCC#AVISO                               ║
║ GSI2_PK: EMP#RS                                                            ║
║ QTD: 1                                                                     ║
╚════════════════════════════════════════════════════════════════════════════╝

TOTAL FINAL: 8 registros
- Exec1 (reprocessada): 2 registros (1 segmentado MA + 1 global MA)
- Exec2 (nova): 6 registros (3 segmentados PI/RS + 3 globais PI/RS)

Empresas no ResultadoAgregado:
✅ MA: Exec1 (última execução que solicitou MA)
✅ PI: Exec2 (última execução que solicitou PI - substituiu exec1)
✅ RS: Exec2 (primeira execução que solicitou RS)
```

---

## 🎯 **Queries e Resultados**

### **Query 1: Usuário de emb-001**
```
Query: PK = "EXEC#exec-xyz-789#EMB#emb-001"

Resultado:
- 1 registro de PI (STATUS#AAA)
- RS não aparece (emb-001 não tem RS)
- MA não aparece (exec-xyz não solicitou MA)
```

### **Query 2: Usuário de emb-002**
```
Query: PK = "EXEC#exec-abc-123#EMB#emb-002"

Resultado:
- 1 registro de MA (CON#NUM)
- PI não aparece (no reprocessamento, PI só tem emb-002 na exec2, não na exec1)
```

### **Query 3: Usuário de emb-003**
```
Query: PK = "EXEC#exec-xyz-789#EMB#emb-003"

Resultado:
- 2 registros de RS (NOME#BBB, NOME#CCC)
```

### **Query 4: Admin da Exec1**
```
Query: PK = "EXEC#exec-abc-123#EMB#GLOBAL"

Resultado:
- 1 registro de MA (CON#NUM)
- PI não aparece (foi deletado e substituído pela exec2)
```

### **Query 5: Admin da Exec2**
```
Query: PK = "EXEC#exec-xyz-789#EMB#GLOBAL"

Resultado:
- 1 registro de PI (STATUS#AAA)
- 2 registros de RS (NOME#BBB, NOME#CCC)
- MA não aparece (não foi solicitado)
```

### **Query 6: Todos os Registros de MA** (Limpeza)
```
Query GSI_Empresa: GSI2_PK = "EMP#MA"

Resultado:
- 1 SEGMENTADO: EXEC#exec-abc-123#EMB#emb-002
- 1 GLOBAL: EXEC#exec-abc-123#EMB#GLOBAL

Total: 2 registros de MA (última execução que solicitou MA)
```

---

## 📈 **Resumo da Lógica**

### **Inserção**:
```
✅ Replicar por embaixada (Segmentado)
✅ Um registro por grupo (Global)
✅ Filtrar apenas empresas solicitadas
✅ Desnormalizar empresas múltiplas (MA,PI → MA + PI)
```

### **Deleção**:
```
✅ Deletar TODOS os registros daquela empresa (qualquer execução)
✅ Deletar GLOBAL (PK=#EMB#GLOBAL)
✅ Deletar SEGMENTADOS (PK=#EMB#embaixada)
✅ Usar GSI_Empresa para eficiência
```

### **Resultado**:
```
✅ Cada empresa sempre mostra sua ÚLTIMA execução
✅ Dados antigos são removidos
✅ Sem duplicatas
✅ Empresas não solicitadas são ignoradas
✅ Usuários veem apenas seus dados (por embaixada)
✅ Admin vê consolidado
```

---

## 🎉 **Fim do Exemplo**

Este fluxo garante que:
- ✅ **Dados sempre frescos** (delete antes de insert)
- ✅ **Segmentação por embaixada** (isolamento de usuários)
- ✅ **Visão global para admin** (consolidado)
- ✅ **Filtro de segurança** (apenas empresas autorizadas)
- ✅ **Performance otimizada** (GSI_Empresa para limpeza rápida)


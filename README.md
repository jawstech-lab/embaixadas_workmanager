# 🔧 Embaixadas Work Manager

Worker Service em .NET 8 para processamento assíncrono de verificações de dados de embaixadas, integrado com AWS SQS e DynamoDB.

---

## 📋 **Visão Geral**

Sistema de processamento distribuído que:
- ✅ Processa execuções de verificações de dados
- ✅ Gerencia verificações por empresa e embaixada
- ✅ Agrega resultados de apontamentos de erro
- ✅ Mantém histórico e auditoria de processos
- ✅ Fornece visões otimizadas para consulta

---

## 🏗️ **Arquitetura**

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│             │         │             │         │             │
│  SQS Queue  │────────▶│   Worker    │────────▶│  DynamoDB   │
│  Execução   │         │   Service   │         │   Tables    │
│             │         │             │         │             │
└─────────────┘         └─────────────┘         └─────────────┘
                               │
                               │
                               ▼
                        ┌─────────────┐
                        │  SQS Queue  │
                        │  Processo   │
                        └─────────────┘
```

### **Fluxo de Processamento**:
1. **Mensagem de Execução** → Queue Execução
2. **Worker processa** → Separa verificações
3. **Envia para Queue Processo** → Uma mensagem por verificação
4. **Worker processa verificações** → Consolida resultados
5. **Pós-processamento** → Agregação e limpeza

---

## 🚀 **Quick Start**

```bash
# 1. Clonar repositório
git clone <url-do-repositorio>
cd embaixadas_workmanager

# 2. Configurar appsettings.json
cp EmbaixadasWorkManager/appsettings.example.json EmbaixadasWorkManager/appsettings.json
# Editar com suas configurações AWS

# 3. Restaurar dependências
dotnet restore

# 4. Executar
cd EmbaixadasWorkManager
dotnet run
```

📖 **Documentação completa**: [CONFIGURACAO.md](./CONFIGURACAO.md)

---

## 🛠️ **Tecnologias**

- **Runtime**: .NET 8.0
- **Cloud**: AWS (SQS, DynamoDB)
- **Padrões**: Worker Service, SOLID, Pipeline Pattern
- **Resiliência**: Polly (Circuit Breaker, Retry)

---

## 📊 **Tabelas DynamoDB**

### **Principais**:
| Tabela | Descrição |
|--------|-----------|
| `Execucao` | Execuções solicitadas |
| `Verificacao` | Tipos de verificações |
| `ExecucaoVerificacao` | Relacionamento execução-verificação |
| `ExecucaoProcesso` | Consolidação de processos |

### **Performance**:
| Tabela | Descrição | GSI |
|--------|-----------|-----|
| `ExecucaoResumoView` | Última execução por embaixada/empresa | - |
| `ExecucaoEmpresaStatus` | Auditoria detalhada por empresa | - |
| `Resultado` | Apontamentos de erro detalhados | `GSI_Agregacao` |
| `ResultadoAgregado` | Agregações (QTD por grupo) | `GSI_EMPRESA` |

---

## 🔄 **Pós-Processamento**

Pipeline configurável com steps:

### **1. Agregação de Resultados** ✅
```csharp
// Busca otimizada usando GSI
Query GSI_Agregacao WHERE GSI1_PK = "EXEC#<id>"

// Agrupamento em memória
Group by (Empresa, Verificação, Tabela, Campo, Referência, Tipo, Nível)

// Limpeza (DELETE ALL BY COMPANY)
Query GSI_EMPRESA WHERE GSI2_PK = "EMP#<empresa>"
DELETE registros antigos

// Inserção
INSERT grupos SEGMENTADOS (por embaixada)
INSERT grupos GLOBAIS (admin)
```

**Funcionalidades**:
- ✅ Limpeza automática de dados antigos por empresa
- ✅ Visão segmentada (por embaixada para usuários)
- ✅ Visão global (consolidada para admin)
- ✅ Desnormalização de empresas múltiplas
- ✅ Filtro de empresas solicitadas

### **2. Agrupamento** (Desabilitado)
### **3. Exclusão de Registros** (Desabilitado)

---

## 📈 **Performance**

### **Otimizações Implementadas**:
- ✅ Long polling no SQS (20s) - Reduz custos
- ✅ Busca em lote (10 mensagens por vez)
- ✅ GSI para queries otimizadas
- ✅ BatchWriteItem (25 items por vez)
- ✅ Processamento em memória
- ✅ Circuit breaker e retry policies

### **Métricas** (exemplo):
- 📊 **291.950 apontamentos** processados em **~2 minutos**
- 📊 **17 grupos** agregados
- 📊 **3 empresas** processadas simultaneamente

---

## 🔒 **Segurança**

### **Arquivos Sensíveis** (`.gitignore`):
```bash
# NÃO versionados
appsettings.json
appsettings.Development.json
*.ps1
Context/DEBUG_*.md
Context/PROBLEMA_*.md
```

### **AWS Credentials**:
- ✅ Usar IAM Roles (ECS/EC2)
- ✅ Ou `~/.aws/credentials` (local)
- ❌ **NUNCA** hardcode credentials no código

---

## 📚 **Documentação**

| Documento | Descrição |
|-----------|-----------|
| [CONFIGURACAO.md](./CONFIGURACAO.md) | Setup e configuração completa |
| [DOCUMENTATION.md](./EmbaixadasWorkManager/DOCUMENTATION.md) | Documentação técnica |
| [Context/README.md](./EmbaixadasWorkManager/Context/README.md) | Visão geral da arquitetura |
| [Context/STYLE_GUIDE.md](./EmbaixadasWorkManager/Context/STYLE_GUIDE.md) | Guia de estilo |
| [Context/ARQUITETURA_*.md](./EmbaixadasWorkManager/Context/) | Documentos arquiteturais |

---

## 🐛 **Troubleshooting**

### **Problema: Registros duplicados**
**Causa**: GSI não criado ou aplicação não reiniciada após correções.
**Solução**: Ver [Context/PROBLEMA_REGISTROS_DUPLICADOS.md](./EmbaixadasWorkManager/Context/PROBLEMA_REGISTROS_DUPLICADOS.md)

### **Problema: "The table does not have the specified index: GSI_EMPRESA"**
**Solução**: Criar GSI manualmente no Console AWS DynamoDB.

### **Problema: Performance lenta**
**Solução**: 
- Verificar `WaitTimeSeconds` = 20 (long polling)
- Verificar `MaxNumberOfMessages` = 10
- Verificar GSIs criados e ACTIVE

---

## 🤝 **Contribuindo**

1. Fork o projeto
2. Crie uma branch para sua feature (`git checkout -b feature/MinhaFeature`)
3. Commit suas mudanças (`git commit -m 'feat: Adiciona MinhaFeature'`)
4. Push para a branch (`git push origin feature/MinhaFeature`)
5. Abra um Pull Request

**Padrões**:
- ✅ Seguir [STYLE_GUIDE.md](./EmbaixadasWorkManager/Context/STYLE_GUIDE.md)
- ✅ Adicionar testes quando aplicável
- ✅ Documentar mudanças significativas

---

## 📝 **License**

[Especificar licença]

---

## 👥 **Autores**

[Seus nomes/equipe]

---

## 📞 **Suporte**

Para dúvidas ou problemas:
- 📧 Email: [seu-email]
- 🐛 Issues: [GitHub Issues]
- 📖 Docs: [Link para docs]

---

**Última atualização**: 12/10/2025


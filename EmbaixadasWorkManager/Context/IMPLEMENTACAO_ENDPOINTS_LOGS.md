# Implementação de Endpoints para Visualização de Logs

## Visão Geral

Esta implementação adiciona funcionalidade completa para visualizar logs do sistema via API REST, permitindo monitoramento em tempo real e análise dos logs gerados pelo Embaixadas WorkManager.

## Arquitetura da Solução

### Componentes Implementados

```
Sistema de Logs
├── ILogService (Interface)
├── LogService (Implementação)
├── LogEntry (Modelo de dados)
├── LogStatistics (Estatísticas)
├── LogsController (API REST)
├── LogCaptureMiddleware (Captura de requisições HTTP)
└── InMemoryLoggerProvider (Captura de logs do sistema)
```

### Fluxo de Captura de Logs

```
1. Sistema gera logs → InMemoryLoggerProvider → LogService
2. Requisições HTTP → LogCaptureMiddleware → LogService
3. Logs armazenados em memória (até 10.000 registros)
4. API REST expõe logs via endpoints
```

## Arquivos Criados/Modificados

### Novos Arquivos

#### 1. **Interfaces/ILogService.cs**
```csharp
public interface ILogService
{
    void AddLog(LogEntry logEntry);
    List<LogEntry> GetLogs(string? level = null, string? source = null, int limit = 100, int offset = 0);
    LogStatistics GetStatistics();
    void ClearLogs();
    List<LogEntry> GetRecentLogs(int count = 50);
}
```

#### 2. **Models/LogEntry.cs**
```csharp
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; }
    public string Category { get; set; }
    public string Message { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public string? Exception { get; set; }
    public string Id { get; set; }
}

public class LogStatistics
{
    public int TotalLogs { get; set; }
    public Dictionary<string, int> LogsByLevel { get; set; }
    public Dictionary<string, int> LogsByCategory { get; set; }
    public int LogsLast24Hours { get; set; }
    public int LogsLastHour { get; set; }
    public DateTime? OldestLog { get; set; }
    public DateTime? NewestLog { get; set; }
}
```

#### 3. **Services/LogService.cs**
- Implementação do `ILogService`
- Armazenamento em memória usando `ConcurrentQueue<LogEntry>`
- Limite máximo de 10.000 logs
- Filtros por nível, fonte, paginação
- Estatísticas em tempo real

#### 4. **Controllers/LogsController.cs**
- Controller REST com 6 endpoints
- Validação de parâmetros
- Tratamento de erros
- Logs estruturados

#### 5. **Middleware/LogCaptureMiddleware.cs**
- Middleware para capturar logs de requisições HTTP
- Logs de entrada, resposta e erros
- Informações de performance (duração)
- Dados de contexto (IP, User-Agent, etc.)

#### 6. **Logging/InMemoryLoggerProvider.cs**
- Provider personalizado para capturar logs do sistema
- Integração com `ILogger<T>`
- Captura logs de todas as categorias
- Filtro por nível (Information e acima)

### Arquivos Modificados

#### 1. **Program.cs**
- Convertido de Worker Service para Web API
- SDK alterado: `Microsoft.NET.Sdk.Worker` → `Microsoft.NET.Sdk.Web`
- Configuração de controllers e middleware
- Registro do `ILogService`

#### 2. **EmbaixadasWorkManager.csproj**
- SDK alterado para suporte a Web API
- Mantidas todas as dependências existentes

## Endpoints da API

### 1. **GET /api/logs**
Lista logs com filtros opcionais.

**Parâmetros:**
- `level` (opcional): Nível do log (Information, Warning, Error, Debug)
- `source` (opcional): Fonte/categoria do log
- `limit` (opcional): Limite de registros (padrão: 100, máximo: 1000)
- `offset` (opcional): Offset para paginação (padrão: 0)

**Exemplo:**
```bash
GET /api/logs?level=Error&source=ProcessorService&limit=50&offset=0
```

**Resposta:**
```json
[
  {
    "id": "guid-unico",
    "timestamp": "2024-01-01T10:30:00Z",
    "level": "Error",
    "category": "ProcessorService",
    "message": "Falha ao processar execução",
    "properties": {
      "ExecucaoId": "execucao123",
      "Erro": "Timeout na execução"
    },
    "exception": "System.TimeoutException: ..."
  }
]
```

### 2. **GET /api/logs/recent**
Obtém logs recentes.

**Parâmetros:**
- `count` (opcional): Número de logs (padrão: 50, máximo: 500)

**Exemplo:**
```bash
GET /api/logs/recent?count=100
```

### 3. **GET /api/logs/statistics**
Obtém estatísticas dos logs.

**Resposta:**
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
    "Request": 300,
    "Response": 300,
    "VerificacaoProcessorService": 250
  },
  "logsLast24Hours": 1200,
  "logsLastHour": 50,
  "oldestLog": "2024-01-01T08:00:00Z",
  "newestLog": "2024-01-01T10:30:00Z"
}
```

### 4. **DELETE /api/logs**
Limpa todos os logs da memória.

**Resposta:**
```json
{
  "message": "Logs limpos com sucesso"
}
```

### 5. **GET /api/logs/level/{level}**
Obtém logs por nível específico.

**Parâmetros:**
- `level`: Nível do log (Information, Warning, Error, Debug)
- `limit` (opcional): Limite de registros

**Exemplo:**
```bash
GET /api/logs/level/Error?limit=100
```

### 6. **GET /api/logs/source/{source}**
Obtém logs por fonte/categoria.

**Parâmetros:**
- `source`: Fonte do log
- `limit` (opcional): Limite de registros

**Exemplo:**
```bash
GET /api/logs/source/ProcessorService?limit=100
```

### 7. **GET /api/logs/view** 🌐 **INTERFACE WEB**
Retorna uma interface web moderna e amigável para visualização dos logs.

**Características:**
- Interface HTML responsiva e moderna
- Dashboard com estatísticas em tempo real
- Filtros interativos por nível de log
- Auto-refresh configurável (30 segundos)
- Cores diferenciadas por nível de log
- Controles para limpar logs e acessar dados JSON
- **Filtro automático**: Remove logs do sistema (Request, Response, Microsoft.AspNetCore, etc.) para interface mais limpa

**Funcionalidades da Interface:**

#### 📊 **Dashboard de Estatísticas**
- Total de logs capturados
- Número de níveis diferentes
- Quantidade de categorias
- Contador específico de erros

#### 🎨 **Design Moderno**
- Interface responsiva (desktop, tablet, mobile)
- Gradiente no cabeçalho
- Cards com estatísticas
- Cores diferenciadas por nível:
  - 🔵 **Information**: Azul claro
  - 🟡 **Warning**: Amarelo
  - 🔴 **Error**: Vermelho
  - 🟢 **Debug**: Verde

#### 🔧 **Controles Interativos**
- **🔄 Atualizar**: Recarrega a página
- **📈 JSON Stats**: Abre estatísticas em JSON (nova aba)
- **📄 JSON Logs**: Abre logs em JSON (nova aba)
- **🗑️ Limpar Logs**: Remove todos os logs com confirmação
- **Filtro por Nível**: Dropdown para filtrar por Information, Warning, Error, Debug
- **Auto-refresh**: Checkbox para atualização automática a cada 30 segundos

#### 📝 **Visualização dos Logs**
- **Timestamp**: Formato HH:mm:ss.fff
- **Nível**: Badge colorido com o nível do log
- **Categoria**: Nome da classe em negrito
- **Mensagem**: Conteúdo do log
- **Exceções**: Caixa destacada com stack trace (quando aplicável)

#### 🚫 **Logs Filtrados Automaticamente**
A interface web filtra automaticamente os seguintes tipos de logs para manter a visualização limpa:
- **Request**: Logs de requisições HTTP (`GET /api/logs/view`)
- **Response**: Logs de respostas HTTP
- **Microsoft.AspNetCore**: Logs do framework ASP.NET Core
- **ControllerActionInvoker**: Logs de invocação de controllers
- **Routing**: Logs de roteamento
- **Hosting**: Logs de hospedagem

**Nota**: Os logs filtrados ainda estão disponíveis via endpoints JSON (`/api/logs`, `/api/logs/statistics`).

**URL de Acesso:**
```
http://localhost:5000/api/logs/view
```

**Exemplo de Uso:**
```bash
# Acessar interface web via curl
curl http://localhost:5000/api/logs/view

# Ou abrir diretamente no navegador
http://localhost:5000/api/logs/view
```

**Benefícios da Interface Web:**
- ✅ **Muito mais fácil de ler** que JSON puro
- ✅ **Visualização em tempo real** dos logs
- ✅ **Filtros interativos** para encontrar problemas rapidamente
- ✅ **Interface profissional** e moderna
- ✅ **Funcionalidades úteis** como auto-refresh e limpeza
- ✅ **Responsiva** para uso em qualquer dispositivo

## Tipos de Logs Capturados

### 1. **Logs do Sistema**
- **Categoria**: Nome da classe (ex: ProcessorService, VerificacaoProcessorService)
- **Níveis**: Information, Warning, Error, Debug
- **Conteúdo**: Mensagens de log do sistema
- **Propriedades**: Dados estruturados (IDs, contadores, etc.)

### 2. **Logs de Requisições HTTP**
- **Categoria**: "Request"
- **Conteúdo**: Método, path, query string
- **Propriedades**: User-Agent, IP, timestamp

### 3. **Logs de Respostas HTTP**
- **Categoria**: "Response"
- **Conteúdo**: Status code, duração
- **Propriedades**: Content-Type, tempo de processamento

### 4. **Logs de Erros HTTP**
- **Categoria**: "Exception"
- **Nível**: Error
- **Conteúdo**: Mensagem da exceção
- **Propriedades**: Tipo da exceção, stack trace

## Configuração e Uso

### 1. **Executar a Aplicação**
```bash
dotnet run --environment Development
```

### 2. **Acessar os Endpoints**
```bash
# Listar logs
curl http://localhost:5000/api/logs

# Logs de erro
curl http://localhost:5000/api/logs/level/Error

# Estatísticas
curl http://localhost:5000/api/logs/statistics

# Logs recentes
curl http://localhost:5000/api/logs/recent?count=20
```

### 3. **Monitoramento em Tempo Real**
```bash
# Logs das últimas 10 requisições
curl http://localhost:5000/api/logs/source/Request?limit=10

# Logs de processamento
curl http://localhost:5000/api/logs/source/ProcessorService?limit=20
```

### 4. **Usando a Interface Web** 🌐
A interface web oferece a melhor experiência para visualizar logs:

#### **Acesso Rápido**
1. Abra o navegador
2. Navegue para: `http://localhost:5000/api/logs/view`
3. Visualize logs em tempo real com interface amigável

#### **Funcionalidades Principais**
- **Dashboard**: Veja estatísticas no topo da página
- **Filtros**: Use o dropdown para filtrar por nível (Information, Warning, Error, Debug)
- **Auto-refresh**: Ative para atualização automática a cada 30 segundos
- **Controles**: Use os botões para atualizar, limpar logs ou acessar dados JSON

#### **Navegação**
- **Logs mais recentes**: Aparecem no topo
- **Cores por nível**: Fácil identificação visual
- **Scroll**: Navegue pelos logs com scroll
- **Responsivo**: Funciona em desktop, tablet e mobile

#### **Exemplo de Uso Prático**
1. **Monitoramento Geral**: Acesse a interface e veja o dashboard
2. **Debug de Erros**: Filtre por "Error" para ver apenas erros
3. **Análise de Performance**: Filtre por "Information" para ver logs normais
4. **Limpeza**: Use o botão "Limpar Logs" quando necessário
5. **Exportação**: Use "JSON Logs" para exportar dados

## Benefícios da Implementação

### 1. **Monitoramento em Tempo Real**
- Visualização imediata dos logs
- Filtros por nível e fonte
- Estatísticas atualizadas
- Interface web com auto-refresh

### 2. **Debugging Facilitado**
- Logs estruturados com propriedades
- Rastreamento de requisições HTTP
- Informações de performance
- Interface visual amigável

### 3. **Análise de Performance**
- Tempo de processamento das requisições
- Contagem de erros por categoria
- Métricas de uso do sistema
- Dashboard com estatísticas visuais

### 4. **Integração com Ferramentas**
- API REST padrão
- Formato JSON
- Fácil integração com dashboards
- Interface web integrada

### 5. **Flexibilidade**
- Filtros múltiplos
- Paginação
- Limpeza de logs
- Configuração de limites
- Interface responsiva

### 6. **Experiência do Usuário** 🌐
- **Interface Web Moderna**: Design profissional e responsivo
- **Visualização Intuitiva**: Cores diferenciadas por nível de log
- **Controles Interativos**: Filtros, auto-refresh, limpeza
- **Acesso Fácil**: URL simples para acesso direto
- **Multiplataforma**: Funciona em qualquer navegador e dispositivo

## Limitações e Considerações

### 1. **Armazenamento em Memória**
- **Limite**: 10.000 logs máximo
- **Persistência**: Logs são perdidos ao reiniciar a aplicação
- **Performance**: Rápido acesso, mas limitado por memória

### 2. **Escalabilidade**
- **Uso**: Adequado para desenvolvimento e ambientes pequenos
- **Produção**: Para ambientes grandes, considerar persistência em banco

### 3. **Segurança**
- **Acesso**: Endpoints públicos (sem autenticação)
- **Dados**: Logs podem conter informações sensíveis
- **Recomendação**: Implementar autenticação para produção

## Próximos Passos Sugeridos

### 1. **Melhorias de Segurança**
- Implementar autenticação/autorização
- Filtrar informações sensíveis dos logs
- Rate limiting nos endpoints

### 2. **Persistência**
- Salvar logs em banco de dados
- Rotação automática de logs
- Backup e recuperação

### 3. **Funcionalidades Avançadas**
- Busca por texto nos logs
- Exportação de logs
- Alertas automáticos
- Dashboard web

### 4. **Integração**
- Webhooks para notificações
- Integração com sistemas de monitoramento
- Métricas para Prometheus/Grafana

## Exemplos de Uso

### 1. **Monitoramento de Erros**
```bash
# Verificar erros recentes
curl http://localhost:5000/api/logs/level/Error?limit=10

# Estatísticas de erros
curl http://localhost:5000/api/logs/statistics | jq '.logsByLevel.Error'
```

### 2. **Análise de Performance**
```bash
# Logs de requisições com tempo de processamento
curl http://localhost:5000/api/logs/source/Response | jq '.[] | select(.properties.Duration > 1000)'
```

### 3. **Debugging de Processamento**
```bash
# Logs de uma execução específica
curl http://localhost:5000/api/logs/source/ProcessorService | jq '.[] | select(.properties.ExecucaoId == "execucao123")'
```

### 4. **Monitoramento de Saúde**
```bash
# Verificar atividade recente
curl http://localhost:5000/api/logs/recent?count=50 | jq '.[] | select(.level == "Error" or .level == "Warning")'
```

## Conclusão

A implementação dos endpoints de logs fornece uma solução completa para monitoramento e debugging do Embaixadas WorkManager. Com API REST flexível, captura automática de logs e estatísticas em tempo real, o sistema agora oferece visibilidade completa sobre seu funcionamento.

A solução é adequada para desenvolvimento e ambientes pequenos, com possibilidade de expansão para funcionalidades mais avançadas conforme necessário.


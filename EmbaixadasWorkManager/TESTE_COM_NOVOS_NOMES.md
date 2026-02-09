# Guia de Teste com Novos Nomes de Recursos AWS

## Configuração Criada

Foi criado o arquivo `appsettings.Test.json` com os novos nomes de recursos AWS e região `us-east-2`.

## Arquivos Criados

1. **appsettings.Test.json** - Arquivo de configuração de teste
   - Região: `us-east-2`
   - Novos nomes de tabelas DynamoDB
   - Novos nomes de filas SQS

2. **test-aws-connection-new-names.ps1** - Script para validar conexão AWS
   - Verifica credenciais AWS
   - Verifica existência de todas as tabelas DynamoDB
   - Verifica existência de todas as filas SQS

3. **run-test.ps1** - Script para executar o WorkManager em modo teste

## Como Testar

### Opção 1: Usando o Script PowerShell (Recomendado)

```powershell
# Validar conexão e recursos AWS
.\test-aws-connection-new-names.ps1

# Executar o WorkManager
.\run-test.ps1
```

### Opção 2: Usando dotnet run diretamente

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Test"
$env:DOTNET_ENVIRONMENT = "Test"
dotnet run
```

### Opção 3: Usando Visual Studio

1. Abra o projeto no Visual Studio
2. Selecione o perfil "EmbaixadasWorkManager.Test" no dropdown de execução
3. Execute o projeto (F5)

### Opção 4: Modificar launchSettings.json

Se você quiser executar usando o perfil criado:

```powershell
dotnet run --launch-profile "EmbaixadasWorkManager.Test"
```

## Verificação de Recursos

Antes de executar, é recomendado verificar se todos os recursos existem na AWS na região `us-east-2`:

```powershell
# Executar script de validação
.\test-aws-connection-new-names.ps1
```

Este script verifica:
- ✅ Credenciais AWS configuradas
- ✅ Conexão com AWS na região us-east-2
- ✅ Existência de todas as tabelas DynamoDB
- ✅ Existência de todas as filas SQS

## Recursos Esperados

### Tabelas DynamoDB (região us-east-2)
- `dynamo-embaixadas-execucoes-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-verificacoes-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-execucao-verificacao-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-execucao-resumo-view-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-execucao-empresa-status-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-resultado-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-resultado-agregado-devqa-eqtl-bdgd-us-east-1`
- `dynamo-embaixadas-justificativa-devqa-eqtl-bdgd-us-east-1`

### Filas SQS (região us-east-2)
- `sqs-embaixadas-execucao-dlq-devqa-etl-bdgd-us-east-1`
- `sqs-embaixadas-execucao-query-devqa-etl-bdgd-us-east-1`
- `sqs-embaixadas-execucao-processo-devqa-etl-bdgd-us-east-1`

## Configuração de Credenciais

Certifique-se de que as credenciais AWS estão configuradas:

```powershell
# Opção 1: AWS CLI
aws configure

# Opção 2: Variáveis de ambiente
$env:AWS_ACCESS_KEY_ID = "sua-access-key"
$env:AWS_SECRET_ACCESS_KEY = "sua-secret-key"
$env:AWS_DEFAULT_REGION = "us-east-2"

# Opção 3: Perfil AWS
$env:AWS_PROFILE = "seu-perfil"
```

## Troubleshooting

### Erro: "Configuração AWS não encontrada"
- Verifique se o arquivo `appsettings.Test.json` existe
- Verifique se a variável de ambiente `ASPNETCORE_ENVIRONMENT=Test` está definida

### Erro: "Tabela não encontrada"
- Execute o script `test-aws-connection-new-names.ps1` para verificar quais recursos estão faltando
- Verifique se os recursos foram criados na região `us-east-2`
- Verifique se os nomes dos recursos estão corretos

### Erro: "Credenciais não encontradas"
- Configure as credenciais AWS usando `aws configure`
- Ou defina as variáveis de ambiente `AWS_ACCESS_KEY_ID` e `AWS_SECRET_ACCESS_KEY`

## Observações Importantes

1. **Região:** Os recursos devem estar na região `us-east-2` conforme configurado no `appsettings.Test.json`

2. **Nomes dos Recursos:** Os nomes dos recursos seguem o padrão definido no depara de produção, mas podem precisar ser ajustados se estiverem em uma região diferente ou ambiente diferente

3. **Fila SQS Execucao:** O nome `sqs-embaixadas-execucao-dlq-devqa-etl-bdgd-us-east-1` parece ser uma DLQ. Verifique se este é o nome correto da fila principal ou se há outro nome

4. **Teste Gradual:** Recomenda-se testar primeiro a conexão e validação de recursos antes de executar o WorkManager completo

## Próximos Passos

Após validar que todos os recursos existem:
1. Execute o script de validação
2. Execute o WorkManager em modo teste
3. Verifique os logs para garantir que está usando os nomes corretos
4. Teste o processamento de uma mensagem de teste



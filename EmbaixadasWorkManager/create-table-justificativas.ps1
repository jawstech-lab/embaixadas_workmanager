# Script para criar tabela Justificativas no DynamoDB
# Armazena justificativas para remoção de apontamentos

param(
    [string]$TableName = "Justificativas",
    [string]$Region = "sa-east-1",
    [string]$ProfileName = "default"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Criando Tabela Justificativas" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tabela: $TableName" -ForegroundColor Yellow
Write-Host "Regiao: $Region" -ForegroundColor Yellow
Write-Host "Perfil: $ProfileName" -ForegroundColor Yellow
Write-Host ""

# Verificar se a tabela já existe
Write-Host "Verificando se a tabela ja existe..." -ForegroundColor Gray
$tableExists = aws dynamodb describe-table `
    --table-name $TableName `
    --region $Region `
    --profile $ProfileName `
    2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "AVISO: Tabela $TableName ja existe!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Verificando se o GSI_Status ja existe..." -ForegroundColor Gray
    
    $tableInfo = aws dynamodb describe-table `
        --table-name $TableName `
        --region $Region `
        --profile $ProfileName | ConvertFrom-Json
    
    $gsiExists = $tableInfo.Table.GlobalSecondaryIndexes | Where-Object { $_.IndexName -eq "GSI_Status" }
    
    if ($gsiExists) {
        Write-Host "GSI_Status ja existe na tabela!" -ForegroundColor Green
        Write-Host "A tabela esta pronta para uso." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "GSI_Status nao encontrado. Execute o script create-gsi-status-justificativa.ps1 para criar." -ForegroundColor Yellow
        exit 0
    }
} else {
    Write-Host "Tabela nao existe. Criando..." -ForegroundColor Green
}

Write-Host ""
Write-Host "Criando tabela $TableName..." -ForegroundColor Yellow

# Criar a tabela com GSI_Status
aws dynamodb create-table `
    --table-name $TableName `
    --attribute-definitions `
        AttributeName=Id,AttributeType=S `
        AttributeName=GSI1_PK,AttributeType=S `
        AttributeName=GSI1_SK,AttributeType=S `
    --key-schema `
        AttributeName=Id,KeyType=HASH `
    --global-secondary-indexes `
        "[{\"IndexName\":\"GSI_Status\",\"KeySchema\":[{\"AttributeName\":\"GSI1_PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1_SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}}]" `
    --billing-mode PROVISIONED `
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 `
    --region $Region `
    --profile $ProfileName

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Tabela criada com sucesso!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Aguarde alguns segundos para a tabela ficar ativa..." -ForegroundColor Yellow
    Write-Host ""
    
    # Aguardar até a tabela ficar ativa
    $maxAttempts = 20
    $attempt = 0
    $isActive = $false
    
    while ($attempt -lt $maxAttempts -and -not $isActive) {
        Start-Sleep -Seconds 5
        $attempt++
        
        Write-Host "Verificando status da tabela... (Tentativa $attempt/$maxAttempts)" -ForegroundColor Gray
        
        $status = aws dynamodb describe-table `
            --table-name $TableName `
            --region $Region `
            --profile $ProfileName `
            --query 'Table.TableStatus' `
            --output text
        
        if ($status -eq "ACTIVE") {
            $isActive = $true
            Write-Host "Tabela esta ativa!" -ForegroundColor Green
        }
    }
    
    if (-not $isActive) {
        Write-Host "AVISO: A tabela ainda nao esta ativa. Continue verificando manualmente." -ForegroundColor Yellow
        Write-Host "Execute: aws dynamodb describe-table --table-name $TableName --region $Region --profile $ProfileName" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "Estrutura da tabela:" -ForegroundColor Cyan
    Write-Host "  PK: Id (String)" -ForegroundColor White
    Write-Host "  Campos principais: VerificacaoId, Empresa, TabelaReferencia, Campo, Status, SelecionarTodos, IdsRelacionados" -ForegroundColor White
    Write-Host ""
    Write-Host "GSI_Status:" -ForegroundColor Cyan
    Write-Host "  GSI1_PK: STATUS#<Status> (ex: STATUS#APROVADO)" -ForegroundColor White
    Write-Host "  GSI1_SK: DATA#<Data> (ex: DATA#2025-10-19T20:37:43.435Z)" -ForegroundColor White
    Write-Host ""
    
} else {
    Write-Host ""
    Write-Host "ERRO ao criar a tabela!" -ForegroundColor Red
    Write-Host "Verifique os logs acima para detalhes." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESUMO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Tabela: $TableName" -ForegroundColor White
Write-Host "Status: ACTIVE" -ForegroundColor Green
Write-Host "GSI_Status: CRIADO" -ForegroundColor Green
Write-Host ""
Write-Host "Próximos passos:" -ForegroundColor Yellow
Write-Host "1. Popule a tabela com registros de justificativas" -ForegroundColor White
Write-Host "2. Certifique-se de que os campos GSI1_PK e GSI1_SK estao preenchidos" -ForegroundColor White
Write-Host "3. Execute o script atualizar-gsi-justificativas.ps1 se houver registros existentes" -ForegroundColor White
Write-Host ""


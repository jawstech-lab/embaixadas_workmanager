# Script para criar tabela Resultado no DynamoDB
# Armazena apontamentos detalhados de erros das execuções

param(
    [string]$TableName = "Resultado",
    [string]$Region = "sa-east-1",
    [string]$ProfileName = "default"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Criando Tabela Resultado" -ForegroundColor Cyan
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
    $response = Read-Host "Deseja continuar mesmo assim? (S/N)"
    if ($response -ne "S" -and $response -ne "s") {
        Write-Host "Operacao cancelada." -ForegroundColor Red
        exit 0
    }
} else {
    Write-Host "Tabela nao existe. Criando..." -ForegroundColor Green
}

Write-Host ""
Write-Host "Criando tabela $TableName..." -ForegroundColor Yellow

# Criar a tabela com GSI para agregação
aws dynamodb create-table `
    --table-name $TableName `
    --attribute-definitions `
        AttributeName=PK,AttributeType=S `
        AttributeName=SK,AttributeType=S `
        AttributeName=GSI1_PK,AttributeType=S `
        AttributeName=GSI1_SK,AttributeType=S `
    --key-schema `
        AttributeName=PK,KeyType=HASH `
        AttributeName=SK,KeyType=RANGE `
    --global-secondary-indexes `
        "[{\"IndexName\":\"GSI_Agregacao\",\"KeySchema\":[{\"AttributeName\":\"GSI1_PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1_SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}}]" `
    --billing-mode PROVISIONED `
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 `
    --region $Region `
    --profile $ProfileName

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "Tabela criada com sucesso!" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Estrutura da Tabela:" -ForegroundColor Cyan
    Write-Host "  PK: EXEC#<ExecucaoId>#VERIF#<VerificacaoId>" -ForegroundColor White
    Write-Host "  SK: ERRO#<Timestamp>#<Seq>" -ForegroundColor White
    Write-Host ""
    Write-Host "GSI_Agregacao:" -ForegroundColor Cyan
    Write-Host "  GSI1_PK: EXEC#<ExecucaoId>" -ForegroundColor White
    Write-Host "  GSI1_SK: EMP#<Empresa>#<Timestamp>" -ForegroundColor White
    Write-Host ""
    Write-Host "Campos:" -ForegroundColor Cyan
    Write-Host "  - Empresa: Sigla da empresa" -ForegroundColor White
    Write-Host "  - Tabela: Nome da tabela do banco" -ForegroundColor White
    Write-Host "  - Campo: Nome do campo com erro" -ForegroundColor White
    Write-Host "  - Referencia: Referencia do registro" -ForegroundColor White
    Write-Host "  - TipoApontamento: Tipo do erro" -ForegroundColor White
    Write-Host "  - Descricao: Descricao detalhada" -ForegroundColor White
    Write-Host ""
    Write-Host "Aguardando tabela ficar ativa..." -ForegroundColor Yellow
    
    aws dynamodb wait table-exists `
        --table-name $TableName `
        --region $Region `
        --profile $ProfileName
    
    Write-Host "Tabela ativa e pronta para uso!" -ForegroundColor Green
    Write-Host ""
    Write-Host "IMPORTANTE: Configure Auto Scaling para capacidade sob demanda:" -ForegroundColor Yellow
    Write-Host "  aws application-autoscaling register-scalable-target ..." -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "ERRO ao criar tabela!" -ForegroundColor Red
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "Verifique as credenciais e permissoes AWS" -ForegroundColor Yellow
    exit 1
}




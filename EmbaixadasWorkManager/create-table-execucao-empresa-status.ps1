# Script para criar tabela ExecucaoEmpresaStatus no DynamoDB
# Esta tabela armazena o histórico completo de todas as execuções por empresa

param(
    [string]$TableName = "ExecucaoEmpresaStatus",
    [string]$Region = "sa-east-1",
    [string]$ProfileName = "default"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Criando Tabela ExecucaoEmpresaStatus" -ForegroundColor Cyan
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

# Criar a tabela
aws dynamodb create-table `
    --table-name $TableName `
    --attribute-definitions `
        AttributeName=PK_STATUS,AttributeType=S `
        AttributeName=SK_STATUS,AttributeType=S `
    --key-schema `
        AttributeName=PK_STATUS,KeyType=HASH `
        AttributeName=SK_STATUS,KeyType=RANGE `
    --billing-mode PAY_PER_REQUEST `
    --region $Region `
    --profile $ProfileName

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "Tabela criada com sucesso!" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Estrutura da Tabela:" -ForegroundColor Cyan
    Write-Host "  PK: EMP#<Sigla> (ex: EMP#MA)" -ForegroundColor White
    Write-Host "  SK: DATA#<DataISO>#<ExecucaoId>" -ForegroundColor White
    Write-Host ""
    Write-Host "Atributos:" -ForegroundColor Cyan
    Write-Host "  - ExecucaoId: ID da execucao" -ForegroundColor White
    Write-Host "  - DataSolicitacao: Data da execucao" -ForegroundColor White
    Write-Host "  - Status: Status da execucao" -ForegroundColor White
    Write-Host "  - SiglaEmpresa: Sigla da empresa" -ForegroundColor White
    Write-Host ""
    Write-Host "Uso:" -ForegroundColor Cyan
    Write-Host "  Esta tabela permite consultar o historico completo de" -ForegroundColor White
    Write-Host "  execucoes de cada empresa, ordenado por data." -ForegroundColor White
    Write-Host ""
    Write-Host "Aguardando tabela ficar ativa..." -ForegroundColor Yellow
    
    aws dynamodb wait table-exists `
        --table-name $TableName `
        --region $Region `
        --profile $ProfileName
    
    Write-Host "Tabela ativa e pronta para uso!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "ERRO ao criar tabela!" -ForegroundColor Red
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "Verifique as credenciais e permissoes AWS" -ForegroundColor Yellow
    exit 1
}





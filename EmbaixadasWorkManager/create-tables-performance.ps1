# Script para criar TODAS as tabelas de performance no DynamoDB
# Executa os scripts individuais de criação de tabelas

param(
    [string]$Region = "sa-east-1",
    [string]$ProfileName = "default"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Criacao de Tabelas de Performance" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Este script criara as seguintes tabelas:" -ForegroundColor Yellow
Write-Host "  1. ExecucaoResumoView" -ForegroundColor White
Write-Host "  2. ExecucaoEmpresaStatus" -ForegroundColor White
Write-Host ""
Write-Host "Regiao: $Region" -ForegroundColor Yellow
Write-Host "Perfil: $ProfileName" -ForegroundColor Yellow
Write-Host ""

$response = Read-Host "Deseja continuar? (S/N)"
if ($response -ne "S" -and $response -ne "s") {
    Write-Host "Operacao cancelada." -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "1. Criando ExecucaoResumoView" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Criar ExecucaoResumoView
& "$PSScriptRoot\create-table-execucao-resumo-view.ps1" `
    -TableName "ExecucaoResumoView" `
    -Region $Region `
    -ProfileName $ProfileName

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERRO ao criar ExecucaoResumoView!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "2. Criando ExecucaoEmpresaStatus" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Criar ExecucaoEmpresaStatus
& "$PSScriptRoot\create-table-execucao-empresa-status.ps1" `
    -TableName "ExecucaoEmpresaStatus" `
    -Region $Region `
    -ProfileName $ProfileName

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERRO ao criar ExecucaoEmpresaStatus!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "TODAS AS TABELAS CRIADAS COM SUCESSO!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Resumo:" -ForegroundColor Cyan
Write-Host "  ExecucaoResumoView: Criada" -ForegroundColor Green
Write-Host "  ExecucaoEmpresaStatus: Criada" -ForegroundColor Green
Write-Host ""
Write-Host "As tabelas estao prontas para uso!" -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo:" -ForegroundColor Yellow
Write-Host "  Execute o WorkManager para comecar a usar as novas tabelas" -ForegroundColor White
Write-Host "  dotnet run --environment Development" -ForegroundColor Gray
Write-Host ""





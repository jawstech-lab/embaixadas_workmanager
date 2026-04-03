# Script para criar TODAS as tabelas de agregação no DynamoDB
# Executa os scripts individuais de criação de tabelas

param(
    [string]$Region = "sa-east-1",
    [string]$ProfileName = "default"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Criacao de Tabelas de Agregacao" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Este script criara as seguintes tabelas:" -ForegroundColor Yellow
Write-Host "  1. Resultado (com GSI_Agregacao)" -ForegroundColor White
Write-Host "  2. ResultadoAgregado" -ForegroundColor White
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
Write-Host "1. Criando Tabela Resultado" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Criar Resultado
& "$PSScriptRoot\create-table-resultado.ps1" `
    -TableName "Resultado" `
    -Region $Region `
    -ProfileName $ProfileName

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERRO ao criar tabela Resultado!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "2. Criando Tabela ResultadoAgregado" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Criar ResultadoAgregado
& "$PSScriptRoot\create-table-resultado-agregado.ps1" `
    -TableName "ResultadoAgregado" `
    -Region $Region `
    -ProfileName $ProfileName

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERRO ao criar tabela ResultadoAgregado!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "TODAS AS TABELAS CRIADAS COM SUCESSO!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Resumo:" -ForegroundColor Cyan
Write-Host "  Resultado: Criada (com GSI_Agregacao)" -ForegroundColor Green
Write-Host "  ResultadoAgregado: Criada" -ForegroundColor Green
Write-Host ""
Write-Host "As tabelas estao prontas para uso!" -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo:" -ForegroundColor Yellow
Write-Host "  O sistema ira automaticamente:" -ForegroundColor White
Write-Host "  1. Buscar apontamentos usando o GSI" -ForegroundColor Gray
Write-Host "  2. Agregar em memoria" -ForegroundColor Gray
Write-Host "  3. Salvar em ResultadoAgregado" -ForegroundColor Gray
Write-Host "  4. Atualizar ExecucaoResumoView" -ForegroundColor Gray
Write-Host ""




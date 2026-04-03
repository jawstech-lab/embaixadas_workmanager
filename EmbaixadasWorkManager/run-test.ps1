# Script para executar o WorkManager com configuração de teste
# Usa appsettings.Test.json com novos nomes e região us-east-2

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "Executando WorkManager - Ambiente de Teste" -ForegroundColor Cyan
Write-Host "Configuração: appsettings.Test.json" -ForegroundColor Cyan
Write-Host "Região AWS: us-east-2" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se estamos no diretório correto
if (-not (Test-Path "EmbaixadasWorkManager.csproj") -and -not (Test-Path "EmbaixadasWorkManager/EmbaixadasWorkManager.csproj")) {
    Write-Host "ERRO: Execute este script do diretório raiz do projeto" -ForegroundColor Red
    exit 1
}

# Navegar para o diretório do projeto se necessário
if (Test-Path "EmbaixadasWorkManager/EmbaixadasWorkManager.csproj") {
    Set-Location "EmbaixadasWorkManager"
}

# Verificar se o arquivo appsettings.Test.json existe
if (-not (Test-Path "appsettings.Test.json")) {
    Write-Host "ERRO: appsettings.Test.json não encontrado!" -ForegroundColor Red
    exit 1
}

Write-Host "Arquivo de configuração encontrado: appsettings.Test.json" -ForegroundColor Green
Write-Host ""

# Definir variável de ambiente para carregar appsettings.Test.json
$env:ASPNETCORE_ENVIRONMENT = "Test"
$env:DOTNET_ENVIRONMENT = "Test"

Write-Host "Variáveis de ambiente definidas:" -ForegroundColor Yellow
Write-Host "  ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Gray
Write-Host "  DOTNET_ENVIRONMENT = $env:DOTNET_ENVIRONMENT" -ForegroundColor Gray
Write-Host ""

Write-Host "Executando WorkManager..." -ForegroundColor Yellow
Write-Host ""

# Executar o projeto
dotnet run --environment Test



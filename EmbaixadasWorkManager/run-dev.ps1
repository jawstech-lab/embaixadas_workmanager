# Script para executar o Embaiadas WorkManager em modo desenvolvimento
Write-Host "=== Embaiadas WorkManager - Modo Desenvolvimento ===" -ForegroundColor Green

# Verificar se o .NET está instalado
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK encontrado: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK não encontrado. Instale o .NET 8.0 ou superior." -ForegroundColor Red
    exit 1
}

# Restaurar pacotes
Write-Host "Restaurando pacotes NuGet..." -ForegroundColor Yellow
dotnet restore

# Compilar o projeto
Write-Host "Compilando o projeto..." -ForegroundColor Yellow
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Falha na compilação" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Projeto compilado com sucesso" -ForegroundColor Green

# Executar em modo desenvolvimento
Write-Host "Executando o Worker Service..." -ForegroundColor Yellow
Write-Host "Pressione Ctrl+C para parar" -ForegroundColor Cyan

dotnet run --environment Development


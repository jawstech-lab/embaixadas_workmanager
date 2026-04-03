# Script para criar execuções de teste na tabela Execucoes
# Execute este script para criar dados de teste antes de processar mensagens

param(
    [int]$ExecucaoCount = 5,
    [string]$Region = "us-east-1",
    [string]$ProfileName = "default"
)

Write-Host "=== Criando execuções de teste no DynamoDB ===" -ForegroundColor Green
Write-Host "Quantidade: $ExecucaoCount" -ForegroundColor Yellow
Write-Host "Região: $Region" -ForegroundColor Yellow
Write-Host "Perfil AWS: $ProfileName" -ForegroundColor Yellow
Write-Host ""

# Verificar se AWS CLI está instalado
try {
    $awsVersion = aws --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "AWS CLI não encontrado. Instale o AWS CLI primeiro." -ForegroundColor Red
        exit 1
    }
    Write-Host "AWS CLI encontrado: $awsVersion" -ForegroundColor Green
}
catch {
    Write-Host "AWS CLI não encontrado. Instale o AWS CLI primeiro." -ForegroundColor Red
    exit 1
}

# Verificar se o perfil AWS está funcionando
Write-Host "Verificando perfil AWS..." -ForegroundColor Cyan
$callerIdentity = aws sts get-caller-identity --profile $ProfileName --region $Region 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Erro ao verificar perfil AWS. Verifique suas credenciais." -ForegroundColor Red
    exit 1
}

$accountId = ($callerIdentity | ConvertFrom-Json).Account
Write-Host "Perfil AWS válido. Account ID: $accountId" -ForegroundColor Green
Write-Host ""

# Dados de teste
$bases = @("Base1", "Base2", "Base3", "Base4")
$empresas = @("Empresa A", "Empresa B", "Empresa C", "Empresa D")
$usuarios = @("usuario.teste1", "usuario.teste2", "usuario.teste3")

# Criar execuções
$successCount = 0
$errorCount = 0

for ($i = 1; $i -le $ExecucaoCount; $i++) {
    $execucaoId = [System.Guid]::NewGuid().ToString()
    $base = $bases | Get-Random
    $empresa = $empresas | Get-Random
    $usuario = $usuarios | Get-Random
    $dataBase = (Get-Date).AddDays(-(Get-Random -Minimum 1 -Maximum 31))
    $dataSolicitacao = Get-Date
    $validacoesCount = Get-Random -Minimum 1 -Maximum 4
    
    # Gerar IDs de validação (GUIDs)
    $validacoes = @()
    for ($v = 1; $v -le $validacoesCount; $v++) {
        $validacoes += [System.Guid]::NewGuid().ToString()
    }
    
    # Criar item para DynamoDB
    $item = @{
        Id = $execucaoId
        Base = $base
        DataBase = $dataBase.ToString("yyyy-MM-ddTHH:mm:ssZ")
        DataSolicitacao = $dataSolicitacao.ToString("yyyy-MM-ddTHH:mm:ssZ")
        Empresa = $empresa
        Usuario = $usuario
        Validacoes = $validacoes
        Status = "Pendente"
        DataInicio = $null
        DataFim = $null
        Erro = $null
        Resultado = $null
    }

    Write-Host "Criando execução $i/$ExecucaoCount..." -ForegroundColor Cyan
    Write-Host "  ID: $execucaoId" -ForegroundColor Gray
    Write-Host "  Base: $base" -ForegroundColor Gray
    Write-Host "  Empresa: $empresa" -ForegroundColor Gray
    Write-Host "  Validações: $validacoesCount" -ForegroundColor Gray

    # Converter para JSON
    $itemJson = $item | ConvertTo-Json -Depth 3

    # Criar execução no DynamoDB
    $result = aws dynamodb put-item `
        --table-name "Execucoes" `
        --item $itemJson `
        --region $Region `
        --profile $ProfileName 2>$null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Criada com sucesso" -ForegroundColor Green
        $successCount++
    }
    else {
        Write-Host "  Erro ao criar execução" -ForegroundColor Red
        $errorCount++
    }

    Write-Host ""
    
    # Pequena pausa entre criações
    Start-Sleep -Milliseconds 500
}

# Resumo
Write-Host "=== Resumo da Criação ===" -ForegroundColor Green
Write-Host "Execuções criadas com sucesso: $successCount" -ForegroundColor Green
if ($errorCount -gt 0) {
    Write-Host "Execuções com erro: $errorCount" -ForegroundColor Red
}
Write-Host ""

Write-Host "Agora você pode enviar mensagens para processar essas execuções:" -ForegroundColor Yellow
Write-Host ".\test-send-messages.ps1 -MessageCount $ExecucaoCount" -ForegroundColor Cyan
Write-Host ""

Write-Host "Para verificar as execuções criadas:" -ForegroundColor Yellow
Write-Host "aws dynamodb scan --table-name Execucoes --region $Region --profile $ProfileName" -ForegroundColor Cyan


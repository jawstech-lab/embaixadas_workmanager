# Script para enviar mensagens de teste para a fila SQS
# Execute este script para testar o processamento de mensagens reais

param(
    [string]$QueueName = "fila-execucao",
    [int]$MessageCount = 5,
    [string]$Region = "us-east-1"
)

Write-Host "=== Enviando mensagens de teste para SQS ===" -ForegroundColor Green
Write-Host "Fila: $QueueName" -ForegroundColor Yellow
Write-Host "Quantidade: $MessageCount" -ForegroundColor Yellow
Write-Host "Região: $Region" -ForegroundColor Yellow
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

# Obter URL da fila
Write-Host "Obtendo URL da fila..." -ForegroundColor Cyan
$queueUrl = aws sqs get-queue-url --queue-name $QueueName --region $Region --query 'QueueUrl' --output text 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Erro ao obter URL da fila. Verifique se a fila existe e você tem permissões." -ForegroundColor Red
    exit 1
}

Write-Host "URL da fila obtida: $queueUrl" -ForegroundColor Green
Write-Host ""

# Enviar mensagens
$successCount = 0
$errorCount = 0

for ($i = 1; $i -le $MessageCount; $i++) {
    # Gerar ID de execução (GUID)
    $execucaoId = [System.Guid]::NewGuid().ToString()
    
    # Criar mensagem JSON simplificada (apenas o ID da execução)
    $message = @{
        execucaoId = $execucaoId
    } | ConvertTo-Json -Depth 1

    Write-Host "Enviando mensagem $i/$MessageCount..." -ForegroundColor Cyan
    Write-Host "  ExecucaoId: $execucaoId" -ForegroundColor Gray

    # Enviar mensagem
    $result = aws sqs send-message --queue-url $queueUrl --message-body $message --region $Region 2>$null

    if ($LASTEXITCODE -eq 0) {
        $messageId = ($result | ConvertFrom-Json).MessageId
        Write-Host "  Enviada com sucesso. MessageId: $messageId" -ForegroundColor Green
        $successCount++
    }
    else {
        Write-Host " Erro ao enviar mensagem" -ForegroundColor Red
        $errorCount++
    }

    Write-Host ""
    
    # Pequena pausa entre mensagens
    Start-Sleep -Milliseconds 500
}

# Resumo
Write-Host "=== Resumo do Envio ===" -ForegroundColor Green
Write-Host "Mensagens enviadas com sucesso: $successCount" -ForegroundColor Green
if ($errorCount -gt 0) {
    Write-Host "Mensagens com erro: $errorCount" -ForegroundColor Red
}
Write-Host ""

Write-Host "IMPORTANTE: As mensagens contêm apenas o ID da execução." -ForegroundColor Yellow
Write-Host "Certifique-se de que as execuções existem na tabela Execucoes antes de processar." -ForegroundColor Yellow
Write-Host ""

Write-Host "Agora execute o Worker para processar as mensagens:" -ForegroundColor Yellow
Write-Host "dotnet run --environment Development" -ForegroundColor Cyan
Write-Host ""

Write-Host "Para monitorar as mensagens na fila:" -ForegroundColor Yellow
Write-Host "aws sqs get-queue-attributes --queue-url $queueUrl --attribute-names All --region $Region" -ForegroundColor Cyan

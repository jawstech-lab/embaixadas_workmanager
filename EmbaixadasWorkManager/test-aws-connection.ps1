# Script para testar conectividade AWS antes de executar o projeto
Write-Host "=== Teste de Conectividade AWS ===" -ForegroundColor Green

# Verificar se AWS CLI está instalado
try {
    $awsVersion = aws --version
    Write-Host "✓ AWS CLI encontrado: $awsVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ AWS CLI não encontrado. Instale o AWS CLI primeiro." -ForegroundColor Red
    Write-Host "Download: https://aws.amazon.com/cli/" -ForegroundColor Yellow
    exit 1
}

# Verificar se o perfil AWS está configurado
Write-Host "`nVerificando perfil AWS..." -ForegroundColor Yellow
try {
    $callerIdentity = aws sts get-caller-identity --profile default --output json | ConvertFrom-Json
    Write-Host "✓ Perfil AWS configurado:" -ForegroundColor Green
    Write-Host "  - Account ID: $($callerIdentity.Account)" -ForegroundColor Cyan
    Write-Host "  - User ID: $($callerIdentity.UserId)" -ForegroundColor Cyan
    Write-Host "  - ARN: $($callerIdentity.Arn)" -ForegroundColor Cyan
} catch {
    Write-Host "✗ Falha ao obter identidade AWS. Verifique as credenciais:" -ForegroundColor Red
    Write-Host "  aws configure --profile default" -ForegroundColor Yellow
    exit 1
}

# Verificar região configurada
Write-Host "`nVerificando região AWS..." -ForegroundColor Yellow
try {
    $region = aws configure get region --profile default
    Write-Host "✓ Região configurada: $region" -ForegroundColor Green
} catch {
    Write-Host "⚠ Região não configurada. Usando padrão: us-east-1" -ForegroundColor Yellow
    $region = "us-east-1"
}

# Listar filas SQS
Write-Host "`nVerificando filas SQS..." -ForegroundColor Yellow
try {
    $queues = aws sqs list-queues --region $region --profile default --output json | ConvertFrom-Json
    
    if ($queues.QueueUrls -and $queues.QueueUrls.Count -gt 0) {
        Write-Host "✓ Filas SQS encontradas: $($queues.QueueUrls.Count)" -ForegroundColor Green
        Write-Host "Filas disponíveis:" -ForegroundColor Cyan
        
        foreach ($queueUrl in $queues.QueueUrls) {
            $queueName = $queueUrl.Split('/')[-1]
            Write-Host "  - $queueName" -ForegroundColor White
            Write-Host "    URL: $queueUrl" -ForegroundColor Gray
        }
        
        # Verificar se as filas necessárias existem
        $requiredQueues = @("fila-execucao", "fila-execucao-query-dev")
        $missingQueues = @()
        
        foreach ($requiredQueue in $requiredQueues) {
            $found = $queues.QueueUrls | Where-Object { $_ -like "*$requiredQueue" }
            if ($found) {
                Write-Host "✓ Fila '$requiredQueue' encontrada" -ForegroundColor Green
            } else {
                Write-Host "✗ Fila '$requiredQueue' NÃO encontrada" -ForegroundColor Red
                $missingQueues += $requiredQueue
            }
        }
        
        if ($missingQueues.Count -gt 0) {
            Write-Host "`n⚠ Filas em falta:" -ForegroundColor Yellow
            foreach ($missingQueue in $missingQueues) {
                Write-Host "  - $missingQueue" -ForegroundColor Red
            }
            Write-Host "`nCrie as filas em falta no console AWS ou use nomes diferentes." -ForegroundColor Yellow
        }
        
    } else {
        Write-Host "⚠ Nenhuma fila SQS encontrada na região $region" -ForegroundColor Yellow
        Write-Host "Crie as filas necessárias no console AWS:" -ForegroundColor Yellow
        Write-Host "  - fila-execucao" -ForegroundColor Cyan
        Write-Host "  - fila-execucao-query-dev" -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "✗ Falha ao listar filas SQS. Verifique permissões IAM:" -ForegroundColor Red
    Write-Host "  - sqs:ListQueues" -ForegroundColor Yellow
    Write-Host "  - sqs:GetQueueAttributes" -ForegroundColor Yellow
}

# Verificar permissões DynamoDB
Write-Host "`nVerificando permissões DynamoDB..." -ForegroundColor Yellow
try {
    $tables = aws dynamodb list-tables --region $region --profile default --output json | ConvertFrom-Json
    
    if ($tables.TableNames -and $tables.TableNames.Count -gt 0) {
        Write-Host "✓ Tabelas DynamoDB encontradas: $($tables.TableNames.Count)" -ForegroundColor Green
        Write-Host "Tabelas disponíveis:" -ForegroundColor Cyan
        
        foreach ($tableName in $tables.TableNames) {
            Write-Host "  - $tableName" -ForegroundColor White
        }
        
        # Verificar se as tabelas necessárias existem
        $requiredTables = @("Execucao", "Verificacao", "ExecucaoVerificacao")
        $missingTables = @()
        
        foreach ($requiredTable in $requiredTables) {
            if ($tables.TableNames -contains $requiredTable) {
                Write-Host "✓ Tabela '$requiredTable' encontrada" -ForegroundColor Green
            } else {
                Write-Host "✗ Tabela '$requiredTable' NÃO encontrada" -ForegroundColor Red
                $missingTables += $requiredTable
            }
        }
        
        if ($missingTables.Count -gt 0) {
            Write-Host "`n⚠ Tabelas em falta:" -ForegroundColor Yellow
            foreach ($missingTable in $missingTables) {
                Write-Host "  - $missingTable" -ForegroundColor Red
            }
            Write-Host "`nCrie as tabelas em falta no console AWS ou use nomes diferentes." -ForegroundColor Yellow
        }
        
    } else {
        Write-Host "⚠ Nenhuma tabela DynamoDB encontrada na região $region" -ForegroundColor Yellow
        Write-Host "Crie as tabelas necessárias no console AWS:" -ForegroundColor Yellow
        Write-Host "  - Execucao" -ForegroundColor Cyan
        Write-Host "  - Verificacao" -ForegroundColor Cyan
        Write-Host "  - ExecucaoVerificacao" -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "✗ Falha ao listar tabelas DynamoDB. Verifique permissões IAM:" -ForegroundColor Red
    Write-Host "  - dynamodb:ListTables" -ForegroundColor Yellow
    Write-Host "  - dynamodb:DescribeTable" -ForegroundColor Yellow
}

Write-Host "`n=== Teste Concluído ===" -ForegroundColor Green
Write-Host "Se todos os testes passaram, você pode executar o projeto:" -ForegroundColor Cyan
Write-Host "  dotnet run --environment Development" -ForegroundColor White

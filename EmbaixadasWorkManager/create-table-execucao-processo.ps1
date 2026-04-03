# Script para criar a tabela ExecucaoProcesso no DynamoDB
# Execute este script para criar a tabela necessária para o novo sistema de processos

param(
    [string]$TableName = "ExecucaoProcesso",
    [string]$Region = "sa-east-1",
    [string]$ProfileName = "default"
)

Write-Host "=== Criando Tabela ExecucaoProcesso no DynamoDB ===" -ForegroundColor Green
Write-Host "Tabela: $TableName" -ForegroundColor Yellow
Write-Host "Região: $Region" -ForegroundColor Yellow
Write-Host "Perfil: $ProfileName" -ForegroundColor Yellow
Write-Host ""

try {
    # Verificar se o AWS CLI está instalado
    $awsVersion = aws --version 2>$null
    if (-not $awsVersion) {
        Write-Error "AWS CLI não está instalado ou não está no PATH"
        exit 1
    }
    
    Write-Host "AWS CLI encontrado: $awsVersion" -ForegroundColor Green
    
    # Verificar se o perfil existe
    $profileExists = aws configure list-profiles 2>$null | Where-Object { $_ -eq $ProfileName }
    if (-not $profileExists) {
        Write-Warning "Perfil AWS '$ProfileName' não encontrado. Tentando usar configuração padrão..."
    }
    
    # Comando para criar a tabela
    $createTableCommand = @"
aws dynamodb create-table `
    --table-name $TableName `
    --attribute-definitions `
        AttributeName=Id,AttributeType=S `
    --key-schema `
        AttributeName=Id,KeyType=HASH `
    --billing-mode PAY_PER_REQUEST `
    --region $Region
"@
    
    if ($profileExists) {
        $createTableCommand = $createTableCommand.Replace("aws dynamodb", "aws dynamodb --profile $ProfileName")
    }
    
    Write-Host "Executando comando de criação da tabela..." -ForegroundColor Cyan
    Write-Host $createTableCommand -ForegroundColor Gray
    
    # Executar o comando
    Invoke-Expression $createTableCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Tabela $TableName criada com sucesso!" -ForegroundColor Green
        
        # Aguardar a tabela ficar ativa
        Write-Host "Aguardando tabela ficar ativa..." -ForegroundColor Yellow
        
        $waitCommand = "aws dynamodb wait table-exists --table-name $TableName --region $Region"
        if ($profileExists) {
            $waitCommand = $waitCommand.Replace("aws dynamodb", "aws dynamodb --profile $ProfileName")
        }
        
        Invoke-Expression $waitCommand
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Tabela $TableName está ativa e pronta para uso!" -ForegroundColor Green
            
            # Mostrar informações da tabela
            $describeCommand = "aws dynamodb describe-table --table-name $TableName --region $Region"
            if ($profileExists) {
                $describeCommand = $describeCommand.Replace("aws dynamodb", "aws dynamodb --profile $ProfileName")
            }
            
            Write-Host "Informações da tabela:" -ForegroundColor Cyan
            Invoke-Expression $describeCommand
        } else {
            Write-Warning "Tabela criada mas pode não estar totalmente ativa ainda"
        }
    } else {
        Write-Error "Erro ao criar tabela $TableName"
        exit 1
    }
    
} catch {
    Write-Error "Erro durante a criação da tabela: $_"
    exit 1
}

Write-Host ""
Write-Host "=== Tabela ExecucaoProcesso criada com sucesso! ===" -ForegroundColor Green
Write-Host "Agora você pode executar o Embaixadas WorkManager com o novo sistema de processos" -ForegroundColor Yellow
Write-Host ""

# Verificar se a tabela foi criada
try {
    $listCommand = "aws dynamodb list-tables --region $Region"
    if ($profileExists) {
        $listCommand = $listCommand.Replace("aws dynamodb", "aws dynamodb --profile $ProfileName")
    }
    
    $tables = Invoke-Expression $listCommand | ConvertFrom-Json
    
    if ($tables.TableNames -contains $TableName) {
        Write-Host "Verificação: Tabela $TableName encontrada na lista de tabelas" -ForegroundColor Green
    } else {
        Write-Warning "Verificação: Tabela $TableName não encontrada na lista de tabelas"
    }
} catch {
    Write-Warning "Não foi possível verificar se a tabela foi criada corretamente"
}


# Script para criar GSI_Status na tabela Justificativa
# Permite buscar justificativas por Status de forma otimizada

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Criando GSI_Status na tabela Justificativa" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Parâmetros
$tableName = "Justificativas"
$indexName = "GSI_Status"
$region = "sa-east-1"

Write-Host "Tabela: $tableName" -ForegroundColor Yellow
Write-Host "Index: $indexName" -ForegroundColor Yellow
Write-Host "Regiao: $region" -ForegroundColor Yellow
Write-Host ""

# Verificar se a tabela existe
Write-Host "Verificando se a tabela existe..." -ForegroundColor Cyan
try {
    $tableInfo = aws dynamodb describe-table `
        --table-name $tableName `
        --region $region `
        2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERRO: Tabela $tableName nao encontrada!" -ForegroundColor Red
        exit 1
    }

    Write-Host "Tabela encontrada!" -ForegroundColor Green
}
catch {
    Write-Host "ERRO ao verificar tabela: $_" -ForegroundColor Red
    exit 1
}

# Criar o GSI
Write-Host ""
Write-Host "Criando GSI_Status..." -ForegroundColor Cyan
Write-Host ""

try {
    aws dynamodb update-table `
        --table-name $tableName `
        --attribute-definitions `
            AttributeName=GSI1_PK,AttributeType=S `
            AttributeName=GSI1_SK,AttributeType=S `
        --global-secondary-index-updates `
            "[{
                \`"Create\`": {
                    \`"IndexName\`": \`"$indexName\`",
                    \`"KeySchema\`": [
                        {\`"AttributeName\`": \`"GSI1_PK\`", \`"KeyType\`": \`"HASH\`"},
                        {\`"AttributeName\`": \`"GSI1_SK\`", \`"KeyType\`": \`"RANGE\`"}
                    ],
                    \`"Projection\`": {\`"ProjectionType\`": \`"ALL\`"},
                    \`"ProvisionedThroughput\`": {
                        \`"ReadCapacityUnits\`": 5,
                        \`"WriteCapacityUnits\`": 5
                    }
                }
            }]" `
        --region $region

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "GSI_Status criado com sucesso!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "O indice esta sendo criado em background." -ForegroundColor Yellow
        Write-Host "Aguarde alguns minutos ate que o status mude para ACTIVE." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Para verificar o status:" -ForegroundColor Cyan
        Write-Host "aws dynamodb describe-table --table-name $tableName --region $region --query 'Table.GlobalSecondaryIndexes[?IndexName==``$indexName``].IndexStatus'" -ForegroundColor White
        Write-Host ""
        Write-Host "IMPORTANTE: Atualize os registros existentes para popular os campos GSI1_PK e GSI1_SK:" -ForegroundColor Yellow
        Write-Host "  GSI1_PK = STATUS#<Status> (ex: STATUS#APROVADO)" -ForegroundColor White
        Write-Host "  GSI1_SK = DATA#<Data> (ex: DATA#2025-10-19T20:37:43.435Z)" -ForegroundColor White
    }
    else {
        Write-Host ""
        Write-Host "ERRO ao criar GSI_Status!" -ForegroundColor Red
        Write-Host "Verifique os logs acima para detalhes." -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host ""
    Write-Host "ERRO ao criar GSI_Status: $_" -ForegroundColor Red
    exit 1
}


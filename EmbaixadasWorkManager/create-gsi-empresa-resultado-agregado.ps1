# Script para criar GSI_Empresa na tabela ResultadoAgregado
# Permite buscar eficientemente todos os registros de uma empresa específica

Write-Host "Criando GSI_Empresa na tabela ResultadoAgregado..." -ForegroundColor Cyan

try {
    aws dynamodb update-table `
        --table-name ResultadoAgregado `
        --attribute-definitions `
            AttributeName=GSI2_PK,AttributeType=S `
            AttributeName=GSI2_SK,AttributeType=S `
        --global-secondary-index-updates `
            "[{
                \`"Create\`": {
                    \`"IndexName\`": \`"GSI_Empresa\`",
                    \`"KeySchema\`": [
                        {\`"AttributeName\`": \`"GSI2_PK\`", \`"KeyType\`": \`"HASH\`"},
                        {\`"AttributeName\`": \`"GSI2_SK\`", \`"KeyType\`": \`"RANGE\`"}
                    ],
                    \`"Projection\`": {\`"ProjectionType\`": \`"ALL\`"},
                    \`"ProvisionedThroughput\`": {
                        \`"ReadCapacityUnits\`": 5,
                        \`"WriteCapacityUnits\`": 5
                    }
                }
            }]" `
        --region sa-east-1

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ GSI_Empresa criado com sucesso!" -ForegroundColor Green
        Write-Host ""
        Write-Host "⏳ IMPORTANTE: O índice está sendo criado em background." -ForegroundColor Yellow
        Write-Host "   Aguarde alguns minutos até que o status fique ACTIVE." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   Execute o comando abaixo para verificar o status:" -ForegroundColor Cyan
        Write-Host "   aws dynamodb describe-table --table-name ResultadoAgregado --query \`"Table.GlobalSecondaryIndexes[?IndexName=='GSI_Empresa'].IndexStatus\`" --region sa-east-1" -ForegroundColor White
        Write-Host ""
        Write-Host "   Quando retornar [\"ACTIVE\"], o GSI estará pronto para uso!" -ForegroundColor Green
        Write-Host ""
    } else {
        Write-Host "❌ Erro ao criar GSI_Empresa!" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "❌ Erro ao executar comando: $_" -ForegroundColor Red
    exit 1
}


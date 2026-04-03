# Script para atualizar registros existentes na tabela Justificativa
# Popula os campos GSI1_PK e GSI1_SK para permitir uso do GSI_Status

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Atualizando GSI_Status em Justificativas" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$tableName = "Justificativas"
$region = "sa-east-1"

Write-Host "Tabela: $tableName" -ForegroundColor Yellow
Write-Host "Regiao: $region" -ForegroundColor Yellow
Write-Host ""

# 1. Buscar TODOS os registros da tabela
Write-Host "Buscando registros existentes..." -ForegroundColor Cyan

$scanCommand = @"
aws dynamodb scan `
  --table-name $tableName `
  --region $region `
  --output json
"@

$result = Invoke-Expression $scanCommand | ConvertFrom-Json

if (-not $result.Items) {
    Write-Host "Nenhum registro encontrado na tabela!" -ForegroundColor Yellow
    exit 0
}

$totalItems = $result.Items.Count
Write-Host "Encontrados $totalItems registros para atualizar" -ForegroundColor Green
Write-Host ""

# 2. Atualizar cada registro
$updated = 0
$failed = 0

foreach ($item in $result.Items) {
    try {
        $id = $item.Id.S
        $status = if ($item.Status.S) { $item.Status.S.ToUpper() } else { "DESCONHECIDO" }
        $data = if ($item.Data.S) { $item.Data.S } else { (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ") }
        
        # Criar valores do GSI
        $gsi1_pk = "STATUS#$status"
        $gsi1_sk = "DATA#$data"
        
        Write-Host "Atualizando ID: $id" -ForegroundColor Cyan
        Write-Host "  Status: $status -> GSI1_PK: $gsi1_pk" -ForegroundColor White
        Write-Host "  Data: $data -> GSI1_SK: $gsi1_sk" -ForegroundColor White
        
        # Update item
        $updateCommand = @"
aws dynamodb update-item `
  --table-name $tableName `
  --key '{\"Id\":{\"S\":\"$id\"}}' `
  --update-expression 'SET GSI1_PK = :pk, GSI1_SK = :sk' `
  --expression-attribute-values '{\":pk\":{\"S\":\"$gsi1_pk\"},\":sk\":{\"S\":\"$gsi1_sk\"}}' `
  --region $region `
  --output json
"@

        $updateResult = Invoke-Expression $updateCommand
        
        if ($LASTEXITCODE -eq 0) {
            $updated++
            Write-Host "  ✓ Atualizado com sucesso!" -ForegroundColor Green
        } else {
            $failed++
            Write-Host "  ✗ Falha na atualizacao!" -ForegroundColor Red
        }
        
        Write-Host ""
    }
    catch {
        $failed++
        Write-Host "  ✗ ERRO: $_" -ForegroundColor Red
        Write-Host ""
    }
}

# Resumo
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESUMO DA ATUALIZACAO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total de registros: $totalItems" -ForegroundColor White
Write-Host "Atualizados: $updated" -ForegroundColor Green
Write-Host "Falharam: $failed" -ForegroundColor Red
Write-Host ""

if ($failed -eq 0) {
    Write-Host "✓ TODOS os registros foram atualizados com sucesso!" -ForegroundColor Green
} else {
    Write-Host "⚠ Alguns registros falharam. Verifique os erros acima." -ForegroundColor Yellow
}


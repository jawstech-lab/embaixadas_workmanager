# Script de Migração: Consolidação Execucao + ExecucaoProcesso
# Este script migra os dados da tabela ExecucaoProcesso para a tabela Execucoes expandida
# IMPORTANTE: Execute este script APENAS após fazer backup das tabelas existentes!

param(
    [string]$ProfileName = "default",
    [string]$Region = "us-east-1",
    [switch]$DryRun = $false,
    [switch]$Force = $false
)

Write-Host "=== MIGRAÇÃO EXECUCAO + EXECUCAOPROCESSO ===" -ForegroundColor Cyan
Write-Host "Perfil AWS: $ProfileName" -ForegroundColor Yellow
Write-Host "Região: $Region" -ForegroundColor Yellow
Write-Host "Modo Dry Run: $DryRun" -ForegroundColor Yellow
Write-Host "Forçar execução: $Force" -ForegroundColor Yellow
Write-Host ""

if (-not $Force) {
    Write-Host "⚠️  ATENÇÃO: Este script irá modificar dados nas tabelas DynamoDB!" -ForegroundColor Red
    Write-Host "⚠️  Certifique-se de ter feito backup das tabelas antes de continuar!" -ForegroundColor Red
    Write-Host ""
    $confirma = Read-Host "Digite 'CONFIRMO' para continuar ou qualquer outra coisa para cancelar"
    
    if ($confirma -ne "CONFIRMO") {
        Write-Host "Migração cancelada pelo usuário." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "Iniciando migração..." -ForegroundColor Green

try {
    # Configurar AWS CLI
    Write-Host "Configurando AWS CLI..." -ForegroundColor Blue
    aws configure list --profile $ProfileName | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        throw "Perfil AWS '$ProfileName' não encontrado ou inválido"
    }

    # Verificar se as tabelas existem
    Write-Host "Verificando existência das tabelas..." -ForegroundColor Blue
    
    $tabelaExecucoes = "Execucoes"
    $tabelaExecucaoProcesso = "ExecucaoProcesso"
    
    # Verificar tabela Execucoes
    $execucoesExiste = aws dynamodb describe-table --table-name $tabelaExecucoes --profile $ProfileName --region $Region 2>$null
    if (-not $execucoesExiste) {
        throw "Tabela '$tabelaExecucoes' não encontrada na região $Region"
    }
    
    # Verificar tabela ExecucaoProcesso
    $execucaoProcessoExiste = aws dynamodb describe-table --table-name $tabelaExecucaoProcesso --profile $ProfileName --region $Region 2>$null
    if (-not $execucaoProcessoExiste) {
        Write-Host "⚠️  Tabela '$tabelaExecucaoProcesso' não encontrada. Nenhuma migração necessária." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host "✅ Tabelas encontradas com sucesso" -ForegroundColor Green
    
    # Contar registros na tabela ExecucaoProcesso
    Write-Host "Contando registros para migração..." -ForegroundColor Blue
    $countResult = aws dynamodb scan --table-name $tabelaExecucaoProcesso --select COUNT --profile $ProfileName --region $Region | ConvertFrom-Json
    $totalRegistros = $countResult.Count
    
    if ($totalRegistros -eq 0) {
        Write-Host "ℹ️  Nenhum registro encontrado na tabela '$tabelaExecucaoProcesso'. Nenhuma migração necessária." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host "📊 Total de registros para migrar: $totalRegistros" -ForegroundColor Green
    
    # Listar registros da tabela ExecucaoProcesso
    Write-Host "Listando registros da tabela ExecucaoProcesso..." -ForegroundColor Blue
    $registros = aws dynamodb scan --table-name $tabelaExecucaoProcesso --profile $ProfileName --region $Region | ConvertFrom-Json
    
    $migrados = 0
    $erros = 0
    
    foreach ($registro in $registros.Items) {
        try {
            $execucaoId = $registro.ExecucaoId.S
            $quantidadeVerificacoes = [int]$registro.QuantidadeVerificacoes.N
            $verificacoesProcessadas = [int]$registro.VerificacoesProcessadas.N
            $erros = [int]$registro.Erros.N
            $dataInicio = $registro.DataInicio.S
            
            Write-Host "🔄 Migrando execução: $execucaoId" -ForegroundColor Blue
            
            if ($DryRun) {
                Write-Host "   [DRY RUN] Seria atualizada com:" -ForegroundColor Gray
                Write-Host "     - QuantidadeVerificacoes: $quantidadeVerificacoes" -ForegroundColor Gray
                Write-Host "     - VerificacoesProcessadas: $verificacoesProcessadas" -ForegroundColor Gray
                Write-Host "     - VerificacoesComErro: $erros" -ForegroundColor Gray
                Write-Host "     - DataInicioProcessamento: $dataInicio" -ForegroundColor Gray
            } else {
                # Atualizar a tabela Execucoes com os campos consolidados
                $updateExpression = "SET QuantidadeVerificacoes = :qty, VerificacoesProcessadas = :proc, VerificacoesComErro = :err, DataInicioProcessamento = :data"
                $expressionValues = "{
                    \":qty\": {\"N\": \"$quantidadeVerificacoes\"},
                    \":proc\": {\"N\": \"$verificacoesProcessadas\"},
                    \":err\": {\"N\": \"$erros\"},
                    \":data\": {\"S\": \"$dataInicio\"}
                }"
                
                $result = aws dynamodb update-item `
                    --table-name $tabelaExecucoes `
                    --key "{\"Id\": {\"S\": \"$execucaoId\"}}" `
                    --update-expression "$updateExpression" `
                    --expression-attribute-values "$expressionValues" `
                    --profile $ProfileName `
                    --region $Region
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "   ✅ Migrado com sucesso" -ForegroundColor Green
                    $migrados++
                } else {
                    Write-Host "   ❌ Erro na migração" -ForegroundColor Red
                    $erros++
                }
            }
        }
        catch {
            Write-Host "   ❌ Erro ao processar registro: $($_.Exception.Message)" -ForegroundColor Red
            $erros++
        }
    }
    
    Write-Host ""
    Write-Host "=== RESUMO DA MIGRAÇÃO ===" -ForegroundColor Cyan
    
    if ($DryRun) {
        Write-Host "🔍 MODO DRY RUN - Nenhuma alteração foi feita" -ForegroundColor Yellow
        Write-Host "📊 Total de registros que seriam migrados: $totalRegistros" -ForegroundColor Blue
    } else {
        Write-Host "✅ Migrados com sucesso: $migrados" -ForegroundColor Green
        Write-Host "❌ Erros na migração: $erros" -ForegroundColor Red
        Write-Host "📊 Total processados: $($migrados + $erros)" -ForegroundColor Blue
    }
    
    Write-Host ""
    
    if (-not $DryRun -and $erros -eq 0) {
        Write-Host "🎉 Migração concluída com sucesso!" -ForegroundColor Green
        Write-Host "💡 Próximo passo: Remover a tabela '$tabelaExecucaoProcesso' após validar os dados" -ForegroundColor Cyan
    } elseif (-not $DryRun) {
        Write-Host "⚠️  Migração concluída com erros. Verifique os logs acima." -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "❌ ERRO CRÍTICO: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== FIM DA MIGRAÇÃO ===" -ForegroundColor Cyan





















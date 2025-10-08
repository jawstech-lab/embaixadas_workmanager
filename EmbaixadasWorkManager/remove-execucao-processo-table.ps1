# Script para Remover Tabela ExecucaoProcesso
# IMPORTANTE: Execute este script APENAS após confirmar que a migração foi bem-sucedida!
# A tabela ExecucaoProcesso será PERMANENTEMENTE removida!

param(
    [string]$ProfileName = "default",
    [string]$Region = "us-east-1",
    [switch]$Force = $false
)

Write-Host "=== REMOÇÃO DA TABELA EXECUCAOPROCESSO ===" -ForegroundColor Red
Write-Host "⚠️  ATENÇÃO: Esta operação é IRREVERSÍVEL!" -ForegroundColor Red
Write-Host "⚠️  A tabela ExecucaoProcesso será PERMANENTEMENTE removida!" -ForegroundColor Red
Write-Host ""
Write-Host "Perfil AWS: $ProfileName" -ForegroundColor Yellow
Write-Host "Região: $Region" -ForegroundColor Yellow
Write-Host ""

if (-not $Force) {
    Write-Host "🔴 CONFIRMAÇÃO FINAL REQUERIDA:" -ForegroundColor Red
    Write-Host "Digite 'REMOVER TABELA' para confirmar a remoção permanente" -ForegroundColor Red
    Write-Host "ou qualquer outra coisa para cancelar" -ForegroundColor Red
    Write-Host ""
    $confirma = Read-Host "Confirmação"
    
    if ($confirma -ne "REMOVER TABELA") {
        Write-Host "Remoção cancelada pelo usuário." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    Write-Host "⚠️  ÚLTIMA CHANCE: Digite 'SIM REMOVER' para confirmar" -ForegroundColor Red
    $confirmaFinal = Read-Host "Confirmação final"
    
    if ($confirmaFinal -ne "SIM REMOVER") {
        Write-Host "Remoção cancelada pelo usuário." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "Iniciando remoção da tabela ExecucaoProcesso..." -ForegroundColor Red

try {
    # Configurar AWS CLI
    Write-Host "Configurando AWS CLI..." -ForegroundColor Blue
    aws configure list --profile $ProfileName | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        throw "Perfil AWS '$ProfileName' não encontrado ou inválido"
    }

    $tabelaExecucaoProcesso = "ExecucaoProcesso"
    
    # Verificar se a tabela existe
    Write-Host "Verificando existência da tabela..." -ForegroundColor Blue
    $tabelaExiste = aws dynamodb describe-table --table-name $tabelaExecucaoProcesso --profile $ProfileName --region $Region 2>$null
    
    if (-not $tabelaExiste) {
        Write-Host "ℹ️  Tabela '$tabelaExecucaoProcesso' não encontrada. Nada a remover." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host "✅ Tabela encontrada. Iniciando remoção..." -ForegroundColor Green
    
    # Contar registros antes da remoção
    Write-Host "Contando registros na tabela..." -ForegroundColor Blue
    $countResult = aws dynamodb scan --table-name $tabelaExecucaoProcesso --select COUNT --profile $ProfileName --region $Region | ConvertFrom-Json
    $totalRegistros = $countResult.Count
    
    Write-Host "📊 Total de registros na tabela: $totalRegistros" -ForegroundColor Yellow
    
    if ($totalRegistros -gt 0) {
        Write-Host "⚠️  A tabela contém $totalRegistros registros que serão PERMANENTEMENTE perdidos!" -ForegroundColor Red
        Write-Host "⚠️  Certifique-se de que a migração foi concluída com sucesso!" -ForegroundColor Red
        Write-Host ""
        
        $confirmaRegistros = Read-Host "Digite 'CONFIRMO PERDA' para continuar com a remoção"
        if ($confirmaRegistros -ne "CONFIRMO PERDA") {
            Write-Host "Remoção cancelada pelo usuário." -ForegroundColor Yellow
            exit 0
        }
    }
    
    # Remover a tabela
    Write-Host "🗑️  Removendo tabela '$tabelaExecucaoProcesso'..." -ForegroundColor Red
    
    $result = aws dynamodb delete-table --table-name $tabelaExecucaoProcesso --profile $ProfileName --region $Region
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Tabela '$tabelaExecucaoProcesso' removida com sucesso!" -ForegroundColor Green
        Write-Host ""
        Write-Host "🎉 Consolidação concluída com sucesso!" -ForegroundColor Green
        Write-Host "💡 A tabela Execucoes agora contém todos os dados consolidados" -ForegroundColor Cyan
    } else {
        throw "Erro ao remover tabela: $result"
    }
    
} catch {
    Write-Host "❌ ERRO CRÍTICO: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== FIM DA REMOÇÃO ===" -ForegroundColor Red





















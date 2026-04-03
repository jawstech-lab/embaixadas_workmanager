# Script de teste para validar conexão AWS com novos nomes de recursos
# Região: us-east-2

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "Teste de Conexão AWS - Novos Nomes" -ForegroundColor Cyan
Write-Host "Região: us-east-2" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se AWS CLI está instalado
Write-Host "[1/5] Verificando AWS CLI..." -ForegroundColor Yellow
try {
    $awsVersion = aws --version 2>&1
    Write-Host "AWS CLI encontrado: $awsVersion" -ForegroundColor Green
} catch {
    Write-Host "ERRO: AWS CLI não encontrado. Por favor, instale o AWS CLI primeiro." -ForegroundColor Red
    exit 1
}

Write-Host ""

# Verificar credenciais AWS
Write-Host "[2/5] Verificando credenciais AWS..." -ForegroundColor Yellow
try {
    $identity = aws sts get-caller-identity --region us-east-2 2>&1
    if ($LASTEXITCODE -eq 0) {
        $identity | ConvertFrom-Json | ForEach-Object {
            Write-Host "Account: $($_.Account)" -ForegroundColor Green
            Write-Host "User/Role: $($_.Arn)" -ForegroundColor Green
        }
    } else {
        Write-Host "ERRO: Não foi possível verificar credenciais AWS." -ForegroundColor Red
        Write-Host "Certifique-se de que as credenciais estão configuradas corretamente." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "ERRO ao verificar credenciais: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Listar tabelas DynamoDB na região us-east-2
Write-Host "[3/5] Verificando tabelas DynamoDB na região us-east-2..." -ForegroundColor Yellow
$tabelasEsperadas = @(
    "dynamo-embaixadas-execucoes-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-verificacoes-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-execucao-verificacao-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-execucao-resumo-view-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-execucao-empresa-status-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-resultado-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-resultado-agregado-devqa-eqtl-bdgd-us-east-1",
    "dynamo-embaixadas-justificativa-devqa-eqtl-bdgd-us-east-1"
)

Write-Host ""
Write-Host "Tabelas esperadas:" -ForegroundColor Cyan
foreach ($tabela in $tabelasEsperadas) {
    Write-Host "  - $tabela" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Verificando existência das tabelas..." -ForegroundColor Yellow

$tabelasEncontradas = 0
$tabelasNaoEncontradas = @()

try {
    $listaTabelas = aws dynamodb list-tables --region us-east-2 --output json 2>&1
    if ($LASTEXITCODE -eq 0) {
        $tabelas = ($listaTabelas | ConvertFrom-Json).TableNames
        
        foreach ($tabelaEsperada in $tabelasEsperadas) {
            if ($tabelas -contains $tabelaEsperada) {
                Write-Host "  [OK] $tabelaEsperada" -ForegroundColor Green
                $tabelasEncontradas++
            } else {
                Write-Host "  [FALTA] $tabelaEsperada" -ForegroundColor Red
                $tabelasNaoEncontradas += $tabelaEsperada
            }
        }
        
        Write-Host ""
        Write-Host "Resultado: $tabelasEncontradas/$($tabelasEsperadas.Count) tabelas encontradas" -ForegroundColor $(if ($tabelasEncontradas -eq $tabelasEsperadas.Count) { "Green" } else { "Yellow" })
        
        if ($tabelasNaoEncontradas.Count -gt 0) {
            Write-Host ""
            Write-Host "Tabelas não encontradas:" -ForegroundColor Red
            foreach ($tabela in $tabelasNaoEncontradas) {
                Write-Host "  - $tabela" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "ERRO ao listar tabelas DynamoDB: $listaTabelas" -ForegroundColor Red
    }
} catch {
    Write-Host "ERRO ao verificar tabelas: $_" -ForegroundColor Red
}

Write-Host ""

# Verificar filas SQS na região us-east-2
Write-Host "[4/5] Verificando filas SQS na região us-east-2..." -ForegroundColor Yellow
$filasEsperadas = @(
    "sqs-embaixadas-execucao-dlq-devqa-etl-bdgd-us-east-1",
    "sqs-embaixadas-execucao-query-devqa-etl-bdgd-us-east-1",
    "sqs-embaixadas-execucao-processo-devqa-etl-bdgd-us-east-1"
)

Write-Host ""
Write-Host "Filas esperadas:" -ForegroundColor Cyan
foreach ($fila in $filasEsperadas) {
    Write-Host "  - $fila" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Verificando existência das filas..." -ForegroundColor Yellow

$filasEncontradas = 0
$filasNaoEncontradas = @()

foreach ($filaEsperada in $filasEsperadas) {
    try {
        # Tentar obter URL da fila
        $urlFila = aws sqs get-queue-url --queue-name $filaEsperada --region us-east-2 --output json 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] $filaEsperada" -ForegroundColor Green
            $filasEncontradas++
        } else {
            Write-Host "  [FALTA] $filaEsperada" -ForegroundColor Red
            $filasNaoEncontradas += $filaEsperada
        }
    } catch {
        Write-Host "  [FALTA] $filaEsperada (erro: $_)" -ForegroundColor Red
        $filasNaoEncontradas += $filaEsperada
    }
}

Write-Host ""
Write-Host "Resultado: $filasEncontradas/$($filasEsperadas.Count) filas encontradas" -ForegroundColor $(if ($filasEncontradas -eq $filasEsperadas.Count) { "Green" } else { "Yellow" })

if ($filasNaoEncontradas.Count -gt 0) {
    Write-Host ""
    Write-Host "Filas não encontradas:" -ForegroundColor Red
    foreach ($fila in $filasNaoEncontradas) {
        Write-Host "  - $fila" -ForegroundColor Red
    }
}

Write-Host ""

# Resumo final
Write-Host "[5/5] Resumo final..." -ForegroundColor Yellow
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "Tabelas DynamoDB: $tabelasEncontradas/$($tabelasEsperadas.Count) encontradas" -ForegroundColor $(if ($tabelasEncontradas -eq $tabelasEsperadas.Count) { "Green" } else { "Yellow" })
Write-Host "Filas SQS: $filasEncontradas/$($filasEsperadas.Count) encontradas" -ForegroundColor $(if ($filasEncontradas -eq $filasEsperadas.Count) { "Green" } else { "Yellow" })
Write-Host "===========================================" -ForegroundColor Cyan

if ($tabelasEncontradas -eq $tabelasEsperadas.Count -and $filasEncontradas -eq $filasEsperadas.Count) {
    Write-Host ""
    Write-Host "SUCESSO: Todos os recursos foram encontrados!" -ForegroundColor Green
    Write-Host "Você pode executar o WorkManager com: dotnet run --environment Test" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "ATENÇÃO: Alguns recursos não foram encontrados." -ForegroundColor Yellow
    Write-Host "Verifique se os nomes estão corretos ou se os recursos foram criados na região us-east-2." -ForegroundColor Yellow
    exit 1
}



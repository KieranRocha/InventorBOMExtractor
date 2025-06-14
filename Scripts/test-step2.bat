@echo off
echo ================================================
echo TESTE STEP 2 - WORK-DRIVEN MONITORING
echo ================================================
echo.

echo [1/5] Testando build do projeto...
dotnet build --configuration Release
if %errorLevel% neq 0 (
    echo ❌ ERRO: Build falhou
    pause
    exit /b 1
)
echo ✅ Build OK

echo.
echo [2/5] Testando execução em console...
echo Iniciando companion em modo console...
echo (Aguarde aparecer logs de conexão com Inventor)
echo.
echo 📋 TESTES PARA FAZER MANUALMENTE:
echo    1. Verifique se aparece "Work-Driven Monitoring ativo"
echo    2. Abra um arquivo .iam no Inventor
echo    3. Verifique se aparece "INICIANDO monitoring"
echo    4. Salve o arquivo no Inventor
echo    5. Verifique se aparece "BOM extraction"
echo    6. Feche o arquivo no Inventor
echo    7. Verifique se aparece "PARANDO monitoring"
echo.
echo Pressione Ctrl+C para parar o teste quando terminar
echo.

dotnet run --configuration Release -- --console

echo.
echo ================================================
echo ANÁLISE DOS LOGS
echo ================================================
echo.
echo Verificando logs gerados...

if exist "logs\" (
    echo ✅ Pasta de logs criada
    for %%f in (logs\companion-*.log) do (
        echo 📄 Log encontrado: %%f
        echo Últimas 10 linhas:
        echo ----------------------------------------
        powershell "Get-Content '%%f' | Select-Object -Last 10"
        echo ----------------------------------------
    )
) else (
    echo ❌ Pasta de logs não encontrada
)

echo.
echo ================================================
echo CHECKLIST DE VALIDAÇÃO
echo ================================================
echo.
echo ✅ Verificações obrigatórias:
echo    [ ] Build sem erros
echo    [ ] Logs aparecem no console
echo    [ ] "Work-Driven Monitoring ativo" nos logs
echo    [ ] Abre arquivo → "INICIANDO monitoring"
echo    [ ] Salva arquivo → "BOM extraction" ou "Processando"
echo    [ ] Fecha arquivo → "PARANDO monitoring"
echo    [ ] Arquivo de log gerado em /logs/
echo.
echo ⚠️  Se algum item falhou, verifique:
echo    - Inventor está instalado?
echo    - Arquivo .iam válido foi usado?
echo    - Companion conseguiu conectar no Inventor?
echo.

pause
@echo off
echo ================================================
echo REMOVENDO INVENTOR COMPANION SERVICE
echo ================================================

:: Verifica se está rodando como Administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERRO: Execute como Administrador!
    echo Click direito neste arquivo e selecione "Executar como administrador"
    pause
    exit /b 1
)

:: Para o service
echo Parando service...
sc stop "InventorCompanionService"

:: Aguarda um pouco
echo Aguardando service parar...
timeout /t 5 /nobreak

:: Remove o service
echo Removendo service...
sc delete "InventorCompanionService"

if %errorLevel% equ 0 (
    echo ✓ Service removido com sucesso!
    echo.
    echo O service foi completamente removido do sistema.
    echo Os logs na pasta 'logs\' foram mantidos.
) else (
    echo ✗ Erro ao remover service (pode já estar removido)
    echo.
    echo Se o service ainda aparecer na lista, reinicie o computador.
)

echo.
echo Pressione qualquer tecla para continuar...
pause >nul
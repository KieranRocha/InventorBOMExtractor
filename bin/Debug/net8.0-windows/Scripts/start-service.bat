@echo off
echo ================================================
echo INICIANDO INVENTOR COMPANION SERVICE
echo ================================================

echo Iniciando service...
sc start "InventorCompanionService"

if %errorLevel% equ 0 (
    echo ✓ Service iniciado com sucesso!
) else (
    echo ✗ Erro ao iniciar service
    echo.
    echo Possíveis causas:
    echo - Service não está instalado (execute install-service.bat)
    echo - Service já está rodando
    echo - Erro de configuração (verifique logs na pasta 'logs\')
)

echo.
echo Status atual do service:
timeout /t 2 /nobreak >nul
sc query "InventorCompanionService"

echo.
echo Pressione qualquer tecla para continuar...
pause >nul
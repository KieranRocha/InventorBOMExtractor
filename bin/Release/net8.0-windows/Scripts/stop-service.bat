@echo off
echo ================================================
echo PARANDO INVENTOR COMPANION SERVICE
echo ================================================

echo Parando service...
sc stop "InventorCompanionService"

if %errorLevel% equ 0 (
    echo ✓ Service parado com sucesso!
) else (
    echo ✗ Erro ao parar service
    echo.
    echo Possíveis causas:
    echo - Service não está instalado
    echo - Service já está parado
    echo - Service travado (pode precisar reiniciar o computador)
)

echo.
echo Aguardando service parar completamente...
timeout /t 3 /nobreak >nul

echo Status atual do service:
sc query "InventorCompanionService"

echo.
echo Pressione qualquer tecla para continuar...
pause >nul
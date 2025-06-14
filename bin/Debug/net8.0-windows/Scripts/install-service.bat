@echo off
echo ================================================
echo INSTALANDO INVENTOR COMPANION SERVICE
echo ================================================

:: Verifica se está rodando como Administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERRO: Execute como Administrador!
    echo Click direito neste arquivo e selecione "Executar como administrador"
    pause
    exit /b 1
)

:: Para o service se estiver rodando
echo Parando service existente...
sc stop "InventorCompanionService" >nul 2>&1

:: Aguarda um pouco
timeout /t 3 /nobreak >nul

:: Remove service existente
echo Removendo service existente...
sc delete "InventorCompanionService" >nul 2>&1

:: Aguarda um pouco
timeout /t 2 /nobreak >nul

:: Cria o novo service
echo Instalando novo service...
sc create "InventorCompanionService" ^
    binPath= "\"%~dp0InventorBOMExtractor.exe\"" ^
    start= auto ^
    DisplayName= "Inventor Companion Service" ^
    description= "Serviço para monitoramento automático de projetos CAD do Inventor"

if %errorLevel% equ 0 (
    echo ✓ Service instalado com sucesso!
    
    :: Configura service para reiniciar automaticamente em caso de falha
    echo Configurando auto-restart...
    sc failure "InventorCompanionService" reset= 60 actions= restart/5000/restart/10000/restart/30000
    
    :: Inicia o service
    echo Iniciando service...
    sc start "InventorCompanionService"
    
    if %errorLevel% equ 0 (
        echo ✓ Service iniciado com sucesso!
        echo.
        echo INSTRUÇÕES:
        echo - Para verificar status: sc query InventorCompanionService
        echo - Para parar: Scripts\stop-service.bat
        echo - Para ver logs: pasta 'logs\'
        echo - Service inicia automaticamente com o Windows
    ) else (
        echo ✗ Erro ao iniciar service
        echo Verifique os logs na pasta 'logs\' para mais detalhes
    )
) else (
    echo ✗ Erro ao instalar service
    echo Verifique se:
    echo - Está executando como Administrador
    echo - O arquivo InventorBOMExtractor.exe existe nesta pasta
    echo - Não há outro service com o mesmo nome
)

echo.
echo Pressione qualquer tecla para continuar...
pause >nul
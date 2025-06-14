@echo off
echo ================================================
echo EXECUTANDO COMPANION EM MODO CONSOLE
echo ================================================
echo.
echo Executando em modo de teste (não como service)
echo Pressione Ctrl+C para parar
echo.
echo Logs aparecerão aqui E na pasta 'logs\'
echo.

:: Executa em modo console para testes
InventorBOMExtractor.exe --console

echo.
echo Service parado.
pause
@echo off
chcp 65001 >nul 2>&1
title MD.converter360 - Pornire Completa
color 0D

echo.
echo  ================================================================
echo               MD.converter360 v1.0
echo        Document Converter: PDF/Word to Markdown
echo  ================================================================
echo.
echo  PORTURI FIXE (din D:\AI_projects\PORTS.json):
echo  Backend=5294  Frontend=5172
echo  ================================================================
echo.

REM ================================================================
REM PASUL 0: Opreste serviciile existente (clean restart)
REM ================================================================
echo  [0/4] Curatare servicii existente...
echo        Oprire backend existent...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5294.*LISTENING" 2^>nul') do (
    taskkill /PID %%a /F >nul 2>&1
)
echo        Oprire frontend existent...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5172.*LISTENING" 2^>nul') do (
    taskkill /PID %%a /F >nul 2>&1
)
REM Kill by window title (backup method)
taskkill /FI "WINDOWTITLE eq MDConverter360-Backend*" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq MDConverter360-Frontend*" /F >nul 2>&1
echo        OK - Servicii vechi oprite

REM ================================================================
REM PASUL 1: Verificare dependente
REM ================================================================
echo  [1/4] Verificare dependente...

REM Check .NET SDK
dotnet --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo        EROARE: .NET SDK nu este instalat!
    echo        Descarca de la: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
echo        OK - .NET SDK disponibil

REM Check Node.js
node --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo        EROARE: Node.js nu este instalat!
    echo        Descarca de la: https://nodejs.org
    pause
    exit /b 1
)
echo        OK - Node.js disponibil

REM ================================================================
REM PASUL 2: Porneste Backend .NET
REM ================================================================
echo  [2/4] Pornire Backend .NET (port 5294)...
cd /d "D:\AI_projects\MD.converter360\Backend"
start "MDConverter360-Backend" /min cmd /k "title MDConverter360-Backend (5294) && dotnet run"
echo        Asteptare initializare backend (10 secunde)...
timeout /t 10 /nobreak >nul

REM Verifica backend
curl -s http://localhost:5294/api/health >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo        OK - Backend ONLINE
) else (
    echo        ATENTIE: Backend inca se initializeaza...
    timeout /t 5 /nobreak >nul
)

REM ================================================================
REM PASUL 3: Porneste Frontend Vite
REM ================================================================
echo  [3/4] Pornire Frontend Vite (port 5172)...
cd /d "D:\AI_projects\MD.converter360\Frontend"

REM Check if node_modules exists
if not exist "node_modules" (
    echo        Instalare dependente npm...
    npm install >nul 2>&1
)

start "MDConverter360-Frontend" /min cmd /k "title MDConverter360-Frontend (5172) && npm run dev"
echo        Asteptare initializare frontend (5 secunde)...
timeout /t 5 /nobreak >nul
echo        OK - Frontend lansat

REM ================================================================
REM PASUL 4: Verificare finala
REM ================================================================
echo  [4/4] Verificare finala servicii...
echo.

REM Check Backend
curl -s http://localhost:5294/api/health >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo        [OK] Backend:     http://localhost:5294
) else (
    echo        [!!] Backend:     NU RASPUNDE - verifica fereastra MDConverter360-Backend
)

REM Check Frontend
curl -s http://localhost:5172 >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo        [OK] Frontend:    http://localhost:5172
) else (
    echo        [!!] Frontend:    NU RASPUNDE - verifica fereastra MDConverter360-Frontend
)

echo.
echo  ================================================================
echo                   MD.converter360 PORNIT!
echo  ================================================================
echo    Frontend:   http://localhost:5172
echo    Backend:    http://localhost:5294
echo    Swagger:    http://localhost:5294/swagger
echo  ================================================================
echo.
echo    Formate suportate:
echo    - PDF -> Markdown
echo    - Word (DOCX/DOC) -> Markdown
echo    - ODT -> Markdown
echo    - Markdown -> PDF
echo    - Markdown -> Word (DOCX)
echo  ================================================================
echo.
echo    Pentru oprire: rulati Stop.bat
echo  ================================================================
echo.

echo  Deschid browserul...
timeout /t 2 /nobreak >nul
start http://localhost:5172

echo.
echo  Apasa orice tasta pentru a inchide aceasta fereastra...
echo  (Serviciile continua sa ruleze in background)
pause >nul

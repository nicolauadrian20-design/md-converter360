@echo off
chcp 65001 >nul 2>&1
title MD.converter360 - Oprire Servicii
color 0C

echo.
echo  ================================================================
echo               MD.converter360 - Oprire Servicii
echo  ================================================================
echo.

echo  Oprire Backend (port 5294)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5294.*LISTENING" 2^>nul') do (
    echo        Stopping PID: %%a
    taskkill /PID %%a /F >nul 2>&1
)

echo  Oprire Frontend (port 5172)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5172.*LISTENING" 2^>nul') do (
    echo        Stopping PID: %%a
    taskkill /PID %%a /F >nul 2>&1
)

REM Kill by window title (backup method)
taskkill /FI "WINDOWTITLE eq MDConverter360-Backend*" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq MDConverter360-Frontend*" /F >nul 2>&1

echo.
echo  ================================================================
echo        [OK] MD.converter360 oprit cu succes
echo  ================================================================
echo.

timeout /t 3

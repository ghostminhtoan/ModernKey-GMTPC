@echo off
echo ========================================================
echo   ModernKey GMTPC - Build Standalone Portable (.NET 4.7.2)
echo ========================================================
echo.

dotnet build ModernKey.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)

set TARGET_DIR=bin\Release\net472
if not exist "%TARGET_DIR%\.portable" mkdir "%TARGET_DIR%\.portable"
if not exist "%TARGET_DIR%\.portable\bin" mkdir "%TARGET_DIR%\.portable\bin"

:: Don sach file tam neu co
if exist "%TARGET_DIR%\ModernKey.exe.old" del /f /q "%TARGET_DIR%\ModernKey.exe.old"

:: Di chuyen bat ky file DLL nao con sot lai o thu muc goc vao .portable\bin
if exist "%TARGET_DIR%\*.dll" (
    move /y "%TARGET_DIR%\*.dll" "%TARGET_DIR%\.portable\bin\" >nul 2>&1
)

:: Di chuyen thu muc dll neu con ton tai
if exist "%TARGET_DIR%\dll" (
    xcopy /e /i /y "%TARGET_DIR%\dll\*" "%TARGET_DIR%\.portable\bin\" >nul 2>&1
    rd /s /q "%TARGET_DIR%\dll" >nul 2>&1
)

echo.
echo ========================================================
echo [SUCCESS] Build Portable hoan tat tai: %TARGET_DIR%\ModernKey.exe
echo - Thu muc goc sach se: chi con ModernKey.exe va cau hinh.
echo - Toan bo thu vien DLL nam gon gang tai: %TARGET_DIR%\.portable\bin\
echo ========================================================
echo.

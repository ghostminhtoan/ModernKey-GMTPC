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

if not exist "bin\Release\net472\.portable" (
    mkdir "bin\Release\net472\.portable"
)

echo.
echo [SUCCESS] Build Portable hoan tat tai: bin\Release\net472\ModernKey.exe
echo Co thu muc .portable di kem de luu tru toan bo cau hinh va macro.
echo.

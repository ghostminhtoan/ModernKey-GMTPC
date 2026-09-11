@echo off
echo ========================================================
echo   ModernKey GMTPC - Build Standalone Portable (.NET 10)
echo ========================================================
echo.

dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\portable
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)

if not exist "publish\portable\.portable" (
    mkdir "publish\portable\.portable"
)

echo.
echo [SUCCESS] Build Portable hoan tat tai: publish\portable\ModernKey.exe
echo Co thu muc .portable di kem de luu tru toan bo cau hinh va macro.
echo.

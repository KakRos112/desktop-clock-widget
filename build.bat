@echo off
rem Membangun ulang ClockWidget.exe ke folder publish. Hanya perlu setelah kode diubah.
taskkill /IM ClockWidget.exe /F >nul 2>&1
dotnet publish "%~dp0ClockWidget.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%~dp0publish" -nologo
if errorlevel 1 (
    echo Build gagal.
    pause
    exit /b 1
)
start "" "%~dp0publish\ClockWidget.exe"

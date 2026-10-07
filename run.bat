@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "src\YTRC.ERP.Wpf\YTRC.ERP.Wpf.csproj" %*

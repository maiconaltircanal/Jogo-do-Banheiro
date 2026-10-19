@echo off
rem Compila o jogo usando o compilador C# que ja vem no Windows (nada para instalar).
cd /d "%~dp0"
if not exist dist mkdir dist
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:winexe /codepage:65001 ^
  /out:dist\JogoDoBanheiro.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  src\*.cs
if errorlevel 1 (
  echo.
  echo Falha na compilacao.
  exit /b 1
)
echo.
echo Pronto: dist\JogoDoBanheiro.exe

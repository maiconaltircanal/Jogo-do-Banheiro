@echo off
rem Compila o jogo usando o compilador C# que ja vem no Windows (nada para instalar).
if not exist dist mkdir dist
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:winexe /codepage:65001 ^
  /out:dist\TesteDoMictorio.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\Main.cs
if errorlevel 1 (
  echo.
  echo Falha na compilacao.
  exit /b 1
)
echo.
echo Pronto: dist\TesteDoMictorio.exe

@echo off
rem Uso: publicar.bat 1.0.1 "O que mudou nesta versao"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publicar.ps1" %*

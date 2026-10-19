# Publica uma versao nova do Jogo do Banheiro no GitHub.
# Uso: publicar.bat 1.0.1 "O que mudou nesta versao"
#
# 1. grava a versao no codigo   2. compila o jogo   3. gera o instalador
# 4. commit + tag + push        5. cria a Release com o instalador e o sha256
# Os jogos instalados encontram a Release nova na proxima vez que abrirem.

param(
    [Parameter(Mandatory = $true)][string]$Versao,
    [string]$Notas = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")

function Falha($msg) { Write-Host ""; Write-Host "ERRO: $msg" -ForegroundColor Red; exit 1 }
function Passo($msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }

if ($Versao -notmatch '^\d+\.\d+\.\d+$') { Falha "Use o formato 1.2.3 (ex.: publicar.bat 1.0.1)" }
$tag = "v$Versao"

gh auth status *> $null
if ($LASTEXITCODE -ne 0) { Falha "GitHub CLI sem login. Rode: gh auth login" }
git rev-parse -q --verify "refs/tags/$tag" *> $null
if ($LASTEXITCODE -eq 0) { Falha "A versao $tag ja existe." }

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { Falha "Inno Setup nao encontrado. Instale com: winget install JRSoftware.InnoSetup" }

Passo "Gravando versao $Versao"
$vc = [IO.File]::ReadAllText("$PSScriptRoot\src\Versao.cs")
$vc = $vc -replace 'AssemblyVersion\("[^"]*"\)', "AssemblyVersion(`"$Versao.0`")"
$vc = $vc -replace 'AssemblyFileVersion\("[^"]*"\)', "AssemblyFileVersion(`"$Versao.0`")"
[IO.File]::WriteAllText("$PSScriptRoot\src\Versao.cs", $vc, (New-Object Text.UTF8Encoding $true))

Passo "Compilando o jogo"
& "$PSScriptRoot\build.bat"
if ($LASTEXITCODE -ne 0) { Falha "Falha na compilacao." }

Passo "Gerando o instalador"
& $iscc /Qp "/DAppVersion=$Versao" installer.iss
if ($LASTEXITCODE -ne 0) { Falha "Falha ao gerar o instalador." }
$setup = "$PSScriptRoot\dist\JogoDoBanheiro-Setup.exe"
$sha = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()

Passo "Enviando o codigo para o GitHub"
git add -A
git commit -q -m "Versao $Versao"
git tag $tag
git push -q origin main
if ($LASTEXITCODE -ne 0) { Falha "Falha no git push." }
git push -q origin $tag
if ($LASTEXITCODE -ne 0) { Falha "Falha ao enviar a tag." }

Passo "Criando a Release $tag"
if (-not $Notas) { $Notas = "Versao $Versao" }
$notasArquivo = [IO.Path]::GetTempFileName()
[IO.File]::WriteAllText($notasArquivo, "$Notas`n`nsha256: $sha`n", (New-Object Text.UTF8Encoding $false))
gh release create $tag $setup --title "Jogo do Banheiro $Versao" --notes-file $notasArquivo
$ok = $LASTEXITCODE
Remove-Item $notasArquivo
if ($ok -ne 0) { Falha "Falha ao criar a Release." }

Write-Host ""
Write-Host "Versao $Versao publicada!" -ForegroundColor Green

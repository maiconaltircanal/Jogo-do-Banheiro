; Instalador do Jogo do Banheiro (Inno Setup 6).
; A versao vem do publicar.bat: ISCC /DAppVersion=1.2.3 installer.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{8F3C2A71-5B4D-4E9A-9C1E-2D7B6A4F0E13}
AppName=Jogo do Banheiro
AppVersion={#AppVersion}
AppVerName=Jogo do Banheiro {#AppVersion}
AppPublisher=maiconaltircanal
AppPublisherURL=https://github.com/maiconaltircanal/Jogo-do-Banheiro
AppUpdatesURL=https://github.com/maiconaltircanal/Jogo-do-Banheiro/releases
VersionInfoVersion={#AppVersion}
; Instala na pasta do usuario: nao pede administrador e o jogo pode se atualizar sozinho.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\JogoDoBanheiro
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
OutputDir=dist
OutputBaseFilename=JogoDoBanheiro-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\JogoDoBanheiro.exe
UninstallDisplayName=Jogo do Banheiro
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "dist\JogoDoBanheiro.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Jogo do Banheiro"; Filename: "{app}\JogoDoBanheiro.exe"
Name: "{autodesktop}\Jogo do Banheiro"; Filename: "{app}\JogoDoBanheiro.exe"; Tasks: desktopicon

[Run]
; Sem skipifsilent: depois de uma atualizacao silenciosa o jogo abre sozinho.
Filename: "{app}\JogoDoBanheiro.exe"; Description: "Jogar agora"; Flags: nowait postinstall

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

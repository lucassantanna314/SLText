[Setup]
AppName=SLText
AppVersion={#AppVersion}
AppPublisher=Lucas Santanna
AppPublisherURL=https://github.com/lucassantanna314/SLText
DefaultDirName={autopf}\SLText
DefaultGroupName=SLText
OutputDir=Output
LicenseFile=..\LICENSE
SetupIconFile=..\SLText.View\Assets\icon.ico
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64

[Files]
; ... (seus arquivos continuam iguais)
Source: "..\publish\win\SLText.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "SLText.exe"
Source: "..\publish\win\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\SLText"; Filename: "{app}\SLText.exe"
Name: "{autodesktop}\SLText"; Filename: "{app}\SLText.exe"

[Registry]
; Registra o "Abrir com" no Menu de Contexto do Windows (Botão Direito)
Root: HKCR; Subkey: "*\shell\Open with SLText"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\Open with SLText\command"; ValueType: string; ValueData: """{app}\SLText.exe"" ""%1"""
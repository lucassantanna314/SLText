[Setup]
AppName=SLText
AppVersion={#AppVersion}
DefaultDirName={autopf}\SLText
DefaultGroupName=SLText
OutputDir=Output
OutputBaseFilename=SLText_Setup_x64
SetupIconFile=..\SLText.View\Assets\icon.ico
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64

[Files]
; Binário principal
Source: "..\publish\win\SLText.exe"; DestDir: "{app}"; Flags: ignoreversion
; Pasta Assets
Source: "..\publish\win\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\SLText"; Filename: "{app}\SLText.exe"
Name: "{autodesktop}\SLText"; Filename: "{app}\SLText.exe"

[Registry]
; Registra o "Abrir com" no Menu de Contexto do Windows (Botão Direito)
Root: HKCR; Subkey: "*\shell\Open with SLText"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\Open with SLText\command"; ValueType: string; ValueData: """{app}\SLText.exe"" ""%1"""
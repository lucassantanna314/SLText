[Setup]
AppName=SLText
AppVersion=0.01
DefaultDirName={autopf}\SLText
DefaultGroupName=SLText
OutputDir=Output
OutputBaseFilename=SLText_Setup_x64
SetupIconFile=..\Assets\icon.ico
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64

[Files]
Source: "..\publish\win\SLText.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\SLText"; Filename: "{app}\SLText.exe"
Name: "{autodesktop}\SLText"; Filename: "{app}\SLText.exe"

[Registry]
; Registra o "Abrir com" no Windows
Root: HKCR; Subkey: "*\shell\Open with SLText"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\Open with SLText\command"; ValueType: string; ValueData: """{app}\SLText.exe"" ""%1"""
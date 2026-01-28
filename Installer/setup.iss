[Setup]
AppName=SLText
AppVersion={#AppVersion}
DefaultDirName={autopf}\SLText
DefaultGroupName=SLText
OutputDir=Output
SetupIconFile=..\SLText.View\Assets\icon.ico
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64

[Files]
; 1. Copia o executável principal separadamente para garantir permissões e atalhos
Source: "..\publish\win\SLText.exe"; DestDir: "{app}"; Flags: ignoreversion

; 2. Copia TODAS as DLLs e arquivos de runtime necessários (incluindo libSkiaSharp.dll)
; Usamos * para pegar tudo, mas excluímos o .exe para não duplicar (opcional)
Source: "..\publish\win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "SLText.exe"

; 3. Garante que a pasta Assets seja incluída mantendo a estrutura
Source: "..\publish\win\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs


[Icons]
Name: "{group}\SLText"; Filename: "{app}\SLText.exe"
Name: "{autodesktop}\SLText"; Filename: "{app}\SLText.exe"

[Registry]
; Registra o "Abrir com" no Menu de Contexto do Windows (Botão Direito)
Root: HKCR; Subkey: "*\shell\Open with SLText"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\Open with SLText\command"; ValueType: string; ValueData: """{app}\SLText.exe"" ""%1"""
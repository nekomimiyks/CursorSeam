[Setup]
AppId={{3FCD6D2E-9D3B-4AA2-8BDC-BBE942DEE629}
AppName=CursorSeam
AppVersion={#AppVersion}
AppPublisher=nekomimiyks
AppPublisherURL=https://github.com/nekomimiyks/CursorSeam
AppSupportURL=https://github.com/nekomimiyks/CursorSeam/issues
AppUpdatesURL=https://github.com/nekomimiyks/CursorSeam/releases/latest
DefaultDirName={localappdata}\Programs\CursorSeam
DefaultGroupName=CursorSeam
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
MinVersion=10.0.19041
AppMutex=Local\CursorSeam
UninstallDisplayIcon={app}\CursorSeam.exe
SetupIconFile=..\CursorSeam.ico
OutputDir={#OutputDirectory}
OutputBaseFilename=CursorSeam-{#AppVersion}-Setup
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "simplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "traditional"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "brazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.LaunchQuestion=Do you want to start CursorSeam now?
japanese.LaunchQuestion=今すぐアプリを起動しますか？
korean.LaunchQuestion=지금 CursorSeam을 시작할까요?
simplified.LaunchQuestion=现在启动 CursorSeam 吗？
traditional.LaunchQuestion=要立即啟動 CursorSeam 嗎？
german.LaunchQuestion=Möchten Sie CursorSeam jetzt starten?
french.LaunchQuestion=Voulez-vous lancer CursorSeam maintenant ?
spanish.LaunchQuestion=¿Quieres iniciar CursorSeam ahora?
brazilian.LaunchQuestion=Deseja iniciar o CursorSeam agora?
english.LaunchFailed=Could not start CursorSeam. Open it from the Start menu.
japanese.LaunchFailed=CursorSeamを起動できませんでした。スタートメニューから起動してください。
korean.LaunchFailed=CursorSeam을 시작할 수 없습니다. 시작 메뉴에서 열어 주세요.
simplified.LaunchFailed=无法启动 CursorSeam。请从开始菜单打开。
traditional.LaunchFailed=無法啟動 CursorSeam。請從開始功能表開啟。
german.LaunchFailed=CursorSeam konnte nicht gestartet werden. Öffnen Sie die App über das Startmenü.
french.LaunchFailed=Impossible de lancer CursorSeam. Ouvrez l’application depuis le menu Démarrer.
spanish.LaunchFailed=No se pudo iniciar CursorSeam. Ábrelo desde el menú Inicio.
brazilian.LaunchFailed=Não foi possível iniciar o CursorSeam. Abra o aplicativo pelo menu Iniciar.

[Files]
Source: "{#Application}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\CursorSeam"; Filename: "{app}\CursorSeam.exe"

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

function InstalledCommand: String;
begin
  Result := '"' + ExpandConstant('{app}\CursorSeam.exe') + '" --startup';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Command: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if RegQueryStringValue(HKCU, RunKey, 'CursorSeam', Command) and
       (Pos('\cursorseam.exe" --startup', Lowercase(Command)) > 0) then
      RegWriteStringValue(HKCU, RunKey, 'CursorSeam', InstalledCommand);
  end;
  if (CurStep = ssDone) and not WizardSilent then
    if MsgBox(CustomMessage('LaunchQuestion'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      if not Exec(ExpandConstant('{app}\CursorSeam.exe'), '', ExpandConstant('{app}'), SW_SHOWNORMAL, ewNoWait, ResultCode) then
        MsgBox(CustomMessage('LaunchFailed'), mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if (CurUninstallStep = usPostUninstall) and RegQueryStringValue(HKCU, RunKey, 'CursorSeam', Command) then
    if CompareText(Command, InstalledCommand) = 0 then RegDeleteValue(HKCU, RunKey, 'CursorSeam');
end;

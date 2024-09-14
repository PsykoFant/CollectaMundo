; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "CollectaMundo"
#define MyAppVersion "0.0.1"
#define MyAppExeName "CollectaMundo.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{02D76BD7-501E-410E-8388-4742A27ECF4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={userappdata}\{#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\AddToCollectionManager.cs"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\CollectaMundo.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\CollectaMundo.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\CollectaMundo.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\CollectaMundo.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\CollectaMundo.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\CollectaMundo.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\EntityFramework.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\EntityFramework.SqlServer.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\ServiceStack.Client.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\ServiceStack.Common.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\ServiceStack.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\ServiceStack.Interfaces.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\ServiceStack.Text.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Converters.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Css.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Dom.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Model.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Rendering.Gdi.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Rendering.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SharpVectors.Runtime.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SkiaSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SkiaSharp.Extended.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SkiaSharp.Extended.Svg.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SkiaSharp.Views.Desktop.Common.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\SkiaSharp.Views.WPF.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\System.Data.SqlClient.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\System.Data.SQLite.EF6.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\System.Linq.Dynamic.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\code\CollectaMundo\CollectaMundo\bin\Release\net8.0-windows\runtimes\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent


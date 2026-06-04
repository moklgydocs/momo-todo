# ============================================================
# MokReport.Todo - Publish & Package Script
# ============================================================

param(
    [switch]$Installer  # Generate Inno Setup installer
)

$ProjectDir = "$PSScriptRoot"
$OutputDir  = "$ProjectDir\publish"
$InstallerDir = "$ProjectDir\installer"

Write-Host "📦  Building Momo Todo..." -ForegroundColor Magenta

# 1. Clean
Remove-Item -Recurse -Force $OutputDir -ErrorAction SilentlyContinue

# 2. Publish as self-contained single file
dotnet publish "$ProjectDir\MokReport.Todo.csproj" `
    -c Release `
    -o $OutputDir `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

# 3. Clean up debug files
Remove-Item "$OutputDir\*.pdb" -ErrorAction SilentlyContinue

$ExePath = "$OutputDir\MokReport.Todo.exe"
$Size = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
Write-Host "✅ Single-file EXE: $ExePath" -ForegroundColor Green
Write-Host "📦 Size: ${Size}MB" -ForegroundColor Green

# 4. Create ZIP
$ZipPath = "$OutputDir\MomoTodo-v1.0.0.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path $ExePath -DestinationPath $ZipPath
Write-Host "📦 ZIP: $ZipPath" -ForegroundColor Green

# 5. Inno Setup installer (optional)
if ($Installer) {
    Write-Host "`n📦 Generating installer..." -ForegroundColor Magenta
    Remove-Item -Recurse -Force $InstallerDir -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $InstallerDir | Out-Null

    $IssContent = @"
; Momo Todo Installer Script
#define MyAppName "Momo Todo"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MokReport"
#define MyAppExeName "MokReport.Todo.exe"

[Setup]
AppId={{B8F4A3D2-7E5C-4A1B-9F6D-2C8E3A4B5D7F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir={$InstallerDir}
OutputBaseFilename=MomoTodo-Setup-v1.0.0
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile={$ProjectDir}\Assets\todo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"

[Files]
Source: "{$OutputDir}\MokReport.Todo.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{$OutputDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 Momo Todo"; Flags: nowait postinstall skipifsilent
"@

    $IssPath = "$InstallerDir\setup.iss"
    $IssContent | Out-File -FilePath $IssPath -Encoding UTF8
    Write-Host "📝 Inno Setup script: $IssPath" -ForegroundColor Green
    Write-Host "⚠️  Run: 'iscc $IssPath' to generate .exe installer" -ForegroundColor Yellow
}

Write-Host "`n✨ Done!  Output: $OutputDir" -ForegroundColor Magenta

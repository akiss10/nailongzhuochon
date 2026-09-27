# Build YellowPet.exe with the csc.exe that ships with Windows (.NET Framework).
$ErrorActionPreference = "Stop"
$ws = Split-Path -Parent $MyInvocation.MyCommand.Path
$ws = Split-Path -Parent $ws          # repo root (build script lives in build/)
$out = Join-Path $ws "dist"
$assets = Join-Path $ws "assets\sprites"
New-Item -ItemType Directory -Force -Path $out | Out-Null
if (-not (Test-Path "$assets\scene.png")) { throw "run assets/step7_scene.py first (missing $assets\scene.png)" }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe not found - .NET Framework 4.x is required" }

$exeName = [string]::Concat([char]0x9EC4, [char]0x8272, [char]0x5C0F, [char]0x5BA0, [char]0x7269) + ".exe"
$exe = Join-Path $out $exeName
& $csc /nologo /target:winexe /optimize+ /unsafe /platform:anycpu `
    "/out:$exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "/win32icon:$assets\pet.ico" `
    "/resource:$assets\scene.png,scene.png" `
    "/resource:$assets\arm.png,arm.png" `
    "/resource:$assets\armw.png,armw.png" `
    (Join-Path $ws "src\YellowPet.cs")
if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Get-Item $exe | Select-Object Name, Length, LastWriteTime

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$payload = Join-Path $projectRoot 'work\installer_build\payload.zip'
if (!(Test-Path -LiteralPath $payload)) { throw 'Build the verified delta payload first using build_payload.py.' }
$output = Join-Path $projectRoot 'release\Dishonored-AZ-Setup-0.1.0-beta.1.exe'
New-Item -ItemType Directory -Force (Split-Path $output -Parent) | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/out:$output" "/win32manifest:$PSScriptRoot\app.manifest" "/resource:$payload,payload.zip" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\src\Program.cs" "$PSScriptRoot\src\PatchEngine.cs" "$PSScriptRoot\src\UnrealPackage.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Get-Item -LiteralPath $output | Select-Object Name,Length

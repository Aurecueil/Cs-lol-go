param(
	[switch]$Desktop,
	[string]$ProjectPath = "",
	[string]$InstallFolder = "$env:LOCALAPPDATA\cslol-go",
	[switch]$NoPublish
)

Set-StrictMode -Version Latest

function Find-Project {
	param()
	if ($ProjectPath -and (Test-Path $ProjectPath)) { return (Resolve-Path $ProjectPath).ProviderPath }
	$proj = Get-ChildItem -Path . -Recurse -Filter *.csproj -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "obj" } | Select-Object -First 1
	if ($proj) { return $proj.FullName }
	throw "Could not find a .csproj in the repository. Pass -ProjectPath <path-to-csproj>."
}

try {
	$project = Find-Project
} catch {
	Write-Error $_.Exception.Message
	exit 1
}

Write-Host "Project: $project"
Write-Host "Install folder: $InstallFolder"

$publishTemp = Join-Path $env:TEMP "cslol-go-publish"

if (-not $NoPublish) {
	if (Test-Path $publishTemp) { Remove-Item -Recurse -Force $publishTemp }
	New-Item -ItemType Directory -Path $publishTemp | Out-Null

	Write-Host "Publishing project (Release)..."
	$pub = dotnet publish `"$project`" -c Release -o `"$publishTemp`"
	if ($LASTEXITCODE -ne 0) {
		Write-Error "dotnet publish failed."
		exit $LASTEXITCODE
	}
} else {
	if (-not (Test-Path $publishTemp)) {
		Write-Error "Publish output directory '$publishTemp' does not exist. Run without -NoPublish or provide a publish folder manually."
		exit 1
	}
}

# Ensure install folder
if (Test-Path $InstallFolder) {
	Write-Host "Removing existing install at $InstallFolder"
	Remove-Item -Recurse -Force $InstallFolder
}
New-Item -ItemType Directory -Path $InstallFolder | Out-Null

Write-Host "Copying files to $InstallFolder"
Copy-Item -Path (Join-Path $publishTemp '*') -Destination $InstallFolder -Recurse -Force

# Find the executable to point the shortcut to
$exe = Join-Path $InstallFolder 'modloader.exe'
if (-not (Test-Path $exe)) {
	$exe = Get-ChildItem -Path $InstallFolder -Filter *.exe | Select-Object -First 1
	if ($exe) { $exe = $exe.FullName } else { $exe = $null }
}

if (-not $exe) {
	Write-Error "No executable found in published output. Expected modloader.exe or another .exe."
	exit 1
}

function New-Shortcut($path, $target, $workingDir) {
	$shell = New-Object -ComObject WScript.Shell
	$sc = $shell.CreateShortcut($path)
	$sc.TargetPath = $target
	$sc.WorkingDirectory = $workingDir
	$sc.IconLocation = $target
	$sc.Save()
}

# Start Menu (per-user)
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
if (-not (Test-Path $startMenuDir)) { New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null }
$startShortcut = Join-Path $startMenuDir 'cslol-go.lnk'
Write-Host "Creating Start Menu shortcut: $startShortcut -> $exe"
New-Shortcut -path $startShortcut -target $exe -workingDir $InstallFolder

if ($Desktop) {
	$desktop = [Environment]::GetFolderPath('Desktop')
	$desktopShortcut = Join-Path $desktop 'cslol-go.lnk'
	Write-Host "Creating Desktop shortcut: $desktopShortcut -> $exe"
	New-Shortcut -path $desktopShortcut -target $exe -workingDir $InstallFolder
}

Write-Host "Installation complete. Installed to $InstallFolder"
Write-Host "Shortcuts created in:"
Write-Host " - $startShortcut"
if ($Desktop) { Write-Host " - $desktopShortcut" }

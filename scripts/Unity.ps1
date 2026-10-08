param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [ValidateSet("Validate", "BuildPlayer", "ConfigureExports", "BuildAll")][string]$Action = "Validate"
)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity Editor executable missing" }
if ($Action -eq "BuildPlayer" -and !(Test-Path -LiteralPath (Join-Path $projectRoot "Assets/WaterGunValidation"))) { throw "This project builds a Warudo plugin; use the export menu" }
$artifacts = Join-Path $projectRoot "Artifacts"
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$log = Join-Path $artifacts ($Action + ".log")
$quotedProject = '"' + $projectRoot + '"'
$quotedLog = '"' + $log + '"'
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @("-batchmode", "-quit", "-projectPath", $quotedProject, "-executeMethod", ("PortfolioProject." + $Action), "-logFile", $quotedLog)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)). See $log" }
if ($Action -eq "Validate" -and !(Select-String -LiteralPath $log -SimpleMatch "[Portfolio] VALIDATION PASSED" -Quiet)) { throw "Validation did not finish. See $log" }
Write-Output "Completed $Action. Log: $log"

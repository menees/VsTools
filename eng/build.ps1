param(
	[bool] $build = $true,
	[string[]] $configurations = @('Debug', 'Release'),
	[bool] $publish = $false,
	[string] $msBuildVerbosity = 'minimal'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = [IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Definition)
$repoPath = Resolve-Path (Join-Path $scriptPath '..')
$slnPath = Get-ChildItem -Path $repoPath -Filter *.sln

if ($build)
{
	foreach ($configuration in $configurations)
	{
		# Restore NuGet packages first
		Write-Host "`nRestoring $configuration packages"
		msbuild $slnPath /p:Configuration=$configuration /v:$msBuildVerbosity /nologo /t:Restore

		Write-Host "`nBuilding $configuration projects"
		msbuild $slnPath /p:Configuration=$configuration /v:$msBuildVerbosity /nologo
	}
}

if ($publish)
{
	$published = $false
	$artifactsPath = "$repoPath\artifacts"
	if (Test-Path $artifactsPath)
	{
		Remove-Item -Recurse -Force $artifactsPath
	}

	$ignore = mkdir $artifactsPath
	if ($ignore) { } # For PSUseDeclaredVarsMoreThanAssignments

	foreach ($configuration in $configurations)
	{
		if ($configuration -like '*Release*')
		{
			Write-Host "Publishing $configuration files to $artifactsPath"
			$vsixFiles = @(Get-ChildItem -r "$repoPath\src\**\bin\$configuration\*.vsix")
			foreach ($vsixFile in $vsixFiles)
			{
				$vsixName = [IO.Path]::GetFileName($vsixFile)
				Write-Host "Publishing $vsixName"
				$vsixTarget = "$artifactsPath\$vsixName"
				Copy-Item -Path $vsixFile -Destination $vsixTarget
				$published = $true
			}
		}
	}

	if ($published)
	{
		Write-Host "`n`n****** REMEMBER TO ADD A GITHUB RELEASE! ******"
	}
}

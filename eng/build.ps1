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

function GetVsixVersion($fileName)
{
	# We want the Identity Version attribute not the PackageManifest Version attribute.
	$line = @(Get-Content $fileName | Where-Object {$_ -like '*<Identity*Version="*"*'})[0]

	$result = $null
	if ($line)
	{
		$versionPrefix = 'Version="'
		$startIndex = $line.IndexOf($versionPrefix) +  + $versionPrefix.Length
		$endIndex = $line.IndexOf('"', $startIndex)
		$result = $line.Substring($startIndex, $endIndex - $startIndex)
	}

	return $result
}

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
	$version = GetVsixVersion "$repoPath\src\Menees.VsTools\source.extension.vsixmanifest"
	$published = $false
	if ($version)
	{
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
				Write-Host "Publishing version $version $configuration files to $artifactsPath"
				$vsixFiles = @(Get-ChildItem -r "$repoPath\src\**\bin\$configuration\*.vsix")
				foreach ($vsixFile in $vsixFiles)
				{
					$sourceVsixName = [IO.Path]::GetFileName($vsixFile)
					$targetVsixName = $sourceVsixName.Replace(".vsix", "." + $version + ".vsix")
					Write-Host "Publishing $targetVsixName"
					$vsixTarget = "$artifactsPath\$targetVsixName"
					Copy-Item -Path $vsixFile -Destination $vsixTarget
					$published = $true
				}
			}
		}
	}

	if ($published)
	{
		Write-Host "`n`n****** REMEMBER TO ADD A GITHUB RELEASE! ******"
	}
}

using System.Reflection;
using Menees.VsTools;

[assembly: AssemblyTitle("Menees.VsTools")]
[assembly: AssemblyDescription("Menees.VsTools")]
[assembly: AssemblyVersion(MainPackage.Version)]

// Note: When the VS/product year/version changes, also update:
// - MainPackage.Version.cs: Set Version to 20xx.0.0 (for year change).
// - Resource "110" in VSPackage.resx (for year change).
// - source.extension.vsixmanifest: <Identity>, <DisplayName>, <Tags>, <InstallationTarget>, and <Prerequisites>
// - SDK assembly references: Microsoft.VisualStudio.Shell.1X.0 in .csproj and .xaml files.
// - app.config: Required .NET version
[assembly: AssemblyProduct(MainPackage.Title + " 2019")]
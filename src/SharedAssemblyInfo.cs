#region Using Directives

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#endregion

/*
Each project's AssemblyInfo.cs should have the following attributes:
[assembly: AssemblyTitle("Menees.XXX")]
[assembly: AssemblyDescription("Menees.XXX")]
[assembly: AssemblyProduct("Menees XXX")]
[assembly: AssemblyVersion("X.Y.0.0")] and/or [assembly: AssemblyFileVersion("X.Y.0.0")]
*/

// At the beginning of each year, the copyright ending year should be incremented.
[assembly: AssemblyCopyright("Copyright © 2002-2020 Bill Menees")]
[assembly: AssemblyCompany("Bill Menees")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

#if !PCL
// Tell the framework that English language resources should be used as neutral resources.
// This eliminates some probing for satellite DLLs.
[assembly: NeutralResourcesLanguage("en", UltimateResourceFallbackLocation.MainAssembly)]
#endif

#if !PCL && !SKIP_CLS_COMPLIANCE
// Make sure all assemblies are CLS-compliant, so they can be used from any .NET language.
[assembly: CLSCompliant(true)]
#endif

// By default make everything invisible to COM.
// We'll expicitly make classes COM-visible if we need to.
[assembly: ComVisible(false)]

// Make sure COM-visible classes don't have the "_ClassName" interface created for them.
[assembly: ClassInterface(ClassInterfaceType.None)]

#if WPF
[assembly: System.Windows.ThemeInfo(System.Windows.ResourceDictionaryLocation.None, System.Windows.ResourceDictionaryLocation.SourceAssembly)]
#endif

namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Diagnostics.CodeAnalysis;

	#endregion

	internal static class Guids
	{
		#region Public Fields

		public const string MeneesVsToolsPackageString = "f9e42449-f3ad-495d-b631-5e69c5ef0331";

		public const string MeneesVsToolsCommandSetString = "10fe9051-8cf6-48fc-9062-94a4fb4cd480";

		public const string BaseConverterWindowPersistanceString = "a1c0c4ce-9e21-431f-8125-cc3586c65627";

		public const string TasksWindowPersistanceString = "3a9fa221-14d6-42d0-b9b4-b35a7bd2a7bb";

		public const string BaseConverterOptionsString = "247209a3-be9c-48e8-9678-41900e22be4c";

		public const string GeneralOptionsString = "bd511b61-46fe-4997-965b-c5408b6be977";

		public static readonly Guid MeneesVsToolsPackage = new Guid(MeneesVsToolsPackageString);

		public static readonly Guid MeneesVsToolsCommandSet = new Guid(MeneesVsToolsCommandSetString);

		#endregion
	}
}
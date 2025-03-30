namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	#region GuidFormat

	/// <summary>
	/// The supported formats for <see cref="Guid.ToString(string)"/>.
	/// </summary>
	public enum GuidFormat
	{
		/// <summary>
		/// 32 digits separated by hyphens:
		/// 00000000-0000-0000-0000-000000000000
		/// </summary>
		Dashes,

		/// <summary>
		/// 32 digits:
		/// 00000000000000000000000000000000
		/// </summary>
		Numbers,

		/// <summary>
		/// 32 digits separated by hyphens, enclosed in braces:
		/// {00000000-0000-0000-0000-000000000000}
		/// </summary>
		Braces,

		/// <summary>
		/// 32 digits separated by hyphens, enclosed in parentheses:
		/// (00000000-0000-0000-0000-000000000000)
		/// </summary>
		Parentheses,

		/// <summary>
		/// Four hexadecimal values enclosed in braces, where the fourth value is a subset of eight hexadecimal values that is also enclosed in braces:
		/// {0x00000000,0x0000,0x0000,{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00}}
		/// </summary>
		Structure,
	}

	#endregion

	#region BuildTiming

	internal enum BuildTiming
	{
		None,
		Details,
		Overall,
	}

	#endregion

	#region Command

	internal enum Command
	{
		// Note: These values must match the IDSymbol values used in the VSCT file.
		SortLines = 301,
		Trim = 302,
		Statistics = 303,
		StreamText = 304,
		CheckSpelling = 305,
		GenerateGuid = 306,
		ExecuteText = 307,
		ExecuteFile = 308,
		ToggleFiles = 309,
		ToggleReadOnly = 310,
		AddRegion = 311,
		CollapseAllRegions = 312,
		ExpandAllRegions = 313,
		ViewBaseConverter = 314,
		ListAllProjectProperties = 315,
		CommentSelection = 316,
		UncommentSelection = 317,
		SortMembers = 318,
		AddToDoComment = 319,
		ViewTasks = 320,
		ViewProjectDependencies = 321,

		CopySolutionRelativePath = 322,
		CopyProjectRelativePath = 323,
		CopyRepoRelativePath = 324,
		CopyParentPath = 325,
		CopyFullPath = 326,
		CopyNameOnly = 327,
		CopyUnixSolutionRelativePath = 328,
		CopyUnixProjectRelativePath = 329,
		CopyUnixRepoRelativePath = 330,
		CopyUnixParentPath = 331,
		CopyUnixFullPath = 332,

		CopyDocSolutionRelativePath = 333,
		CopyDocProjectRelativePath = 334,
		CopyDocRepoRelativePath = 335,
		CopyDocParentPath = 336,
		CopyDocFullPath = 337,
		CopyDocNameOnly = 338,
		CopyDocUnixSolutionRelativePath = 339,
		CopyDocUnixProjectRelativePath = 340,
		CopyDocUnixRepoRelativePath = 341,
		CopyDocUnixParentPath = 342,
		CopyDocUnixFullPath = 343,
	}

	#endregion

	#region Language

	internal enum Language
	{
		Unknown,
		CPlusPlus,
		HTML,
		XML,
		VB,
		CSharp,
		CSS,
		PlainText,
		SQL,
		IDL,
		VBScript,
		JavaScript,
		FSharp,
		TypeScript,
		PowerShell,
		Python,
		XAML,
		Razor,
		Less,
		Scss,

		// Note: When you add a language here, also add it to Tasks\ScanInfo.xml
		// (e.g., as a new <Languages> node under an existing <Style>).
	}

	#endregion

	#region UnixDriveFormat

	/// <summary>
	/// The supported formats when converting Windows drive-based paths to Unix format in <see cref="CopyInfoHandler"/>.
	/// </summary>
	internal enum UnixDriveFormat
	{
		/// <summary>
		/// E.g., C:\Test -> /c/Test
		/// </summary>
		LowerLetter,

		/// <summary>
		/// E.g., C:\Test -> /mnt/c/Test
		/// </summary>
		MountLowerLetter,

		/// <summary>
		/// E.g., C:\Test -> C:/Test
		/// </summary>
		UpperLetterColon,
	}

	#endregion
}

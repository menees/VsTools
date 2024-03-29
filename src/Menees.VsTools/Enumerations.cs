﻿namespace Menees.VsTools
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
}

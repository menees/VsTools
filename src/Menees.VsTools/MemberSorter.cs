namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using EnvDTE;
	using EnvDTE80;
	using Microsoft.VisualStudio.Shell;

	#endregion

	internal sealed class MemberSorter
	{
		#region Private Data Members

		// Note: This default sort order is mentioned in the Description attribute for Options.SortMembersOrder.
		private static readonly IEnumerable<Tuple<MemberProperty, bool>> StyleCopSortOrder = new[]
		{
			Tuple.Create(MemberProperty.Kind, true),
			Tuple.Create(MemberProperty.Access, true),
			Tuple.Create(MemberProperty.IsStatic, true),
			Tuple.Create(MemberProperty.KindModifier, true),
			Tuple.Create(MemberProperty.ConstModifier, true),
			Tuple.Create(MemberProperty.OverrideModifier, true),
			Tuple.Create(MemberProperty.Name, true),
			Tuple.Create(MemberProperty.ParameterCount, true),
		};

		private readonly TextDocumentHandler textHandler;
		private readonly List<Tuple<CodeElement2, List<CodeElement2>>> memberLists;
		private readonly TextPoint selectionStart;
		private readonly TextPoint selectionEnd;
		private readonly Language language;

		#endregion

		#region Constructors

		public MemberSorter(DTE dte, bool findMembers)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.textHandler = new TextDocumentHandler(dte);
			this.language = this.textHandler.Language;

			if (this.textHandler.HasNonEmptySelection
				&& this.textHandler.Document != null
				&& (this.language == Language.CSharp || this.language == Language.VB))
			{
				this.selectionStart = this.textHandler.Selection.TopPoint;
				this.selectionEnd = this.textHandler.Selection.BottomPoint;

				ProjectItem item = this.textHandler.Document.ProjectItem;
				if (item != null)
				{
					if (item.FileCodeModel is FileCodeModel2 codeModel && codeModel.ParseStatus == vsCMParseStatus.vsCMParseStatusComplete)
					{
						// For CommandProcessor.CanExecute, we don't actually want to find the members (since
						// CanExecute is called A LOT).  We just want to know if we got far enough to try.
						this.CanFindMembers = true;
						if (findMembers)
						{
							this.memberLists = FindMembers(codeModel.CodeElements, this.IsMemberSelected);
						}
					}
				}
			}
		}

		#endregion

		#region Public Properties

		public bool CanFindMembers { get; }

		public bool HasSelectedMembers
		{
			get
			{
				bool result = this.memberLists?.Count > 0;
				return result;
			}
		}

		#endregion

		#region Public Methods

		public static CodeElement2 FindMember(CodeElements elements, string elementName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			List<Tuple<CodeElement2, List<CodeElement2>>> typeMembersTuples = FindMembers(elements, e => e.Name == elementName);
			List<CodeElement2> matched = typeMembersTuples.SelectMany(tuple => tuple.Item2).ToList();

			CodeElement2 result = null;
			if (matched.Count == 1)
			{
				result = matched[0];
			}
			else if (matched.Count > 1)
			{
				throw new ArgumentException(
					$"Multiple elements were found named {elementName} (in {string.Join(", ", typeMembersTuples.Select(t => t.Item1.Name))}).");
			}

			return result;
		}

		public void SortMembers(Options options)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			IEnumerable<Tuple<MemberProperty, bool>> sortOrder = GetSortOrder(options) ?? StyleCopSortOrder;

			// Get a list of all selected members using our CodeMember type since Microsoft didn't define a common member base type.
			List<CodeMember> codeMembers = new List<CodeMember>();
			if (this.memberLists != null)
			{
				foreach (var tuple in this.memberLists)
				{
					codeMembers.AddRange(tuple.Item2.Select(e => new CodeMember(e, tuple.Item1, this.language)));
				}
			}

			bool execute = true;
			void SortMembers(List<CodeMember> list) => list.Sort((x, y) => x.CompareByMemberProperties(y, sortOrder));
			if (!options.OnlyShowSortMembersDialogWhenShiftIsPressed || Utilities.IsShiftPressed)
			{
				SortMembersDialog dialog = new SortMembersDialog();
				try
				{
					execute = dialog.Execute(codeMembers, options, SortMembers);
				}
				catch (Exception)
				{
					// Exceptions from any dialog event handler would leave the dialog open but orphaned
					// (i.e., VS would no longer treat it as a modal).  We need to force the dialog to close.
					dialog.Close();
					throw;
				}
			}
			else
			{
				SortMembers(codeMembers);
			}

			if (execute)
			{
				// When we get here the codeMembers list is either sorted or manually ordered the way the user wants.
				// So we just need to apply the members in the requested order.
				this.textHandler.Execute(
					"Sort Members",
					() =>
					{
						// We'll try to group the type members by the first sort property (if it's an enum property) to keep
						// the members within any existing #regions (assuming the #regions are grouped that way).
						Tuple<MemberProperty, bool> groupingProperty = sortOrder.First();
						if (groupingProperty.Item1 != MemberProperty.Kind && groupingProperty.Item1 != MemberProperty.Access)
						{
							groupingProperty = null;
						}

						foreach (var typeGroup in codeMembers.GroupBy(member => member.TypeElement))
						{
							CodeElement2 type = typeGroup.Key;
							IEnumerable<List<CodeMember>> reorderGroups = GroupMembers(typeGroup.ToList(), groupingProperty);
							foreach (var reorderGroup in reorderGroups)
							{
								this.ReorderMembers(type, reorderGroup, false);
							}
						}
					});
			}
		}

		#endregion

		#region Private Methods

		private static IEnumerable<Tuple<MemberProperty, bool>> GetSortOrder(Options options)
		{
			List<Tuple<MemberProperty, bool>> result = null;
			if (options != null)
			{
				string sortOrderText = options.SortMembersOrder;
				if (!string.IsNullOrEmpty(sortOrderText))
				{
					IEnumerable<string> tokens = sortOrderText.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
					foreach (string token in tokens)
					{
						string propertyName = token;
						bool ascending = token[0] != '-';
						if (!ascending)
						{
							propertyName = token.Substring(1);
						}

						if (!Enum.TryParse<MemberProperty>(propertyName, out MemberProperty property))
						{
							string message = "Unsupported sort property: " + propertyName + ". Supported properties: "
								+ string.Join(", ", Enum.GetNames(typeof(MemberProperty)).OrderBy(n => n));
							throw new NotSupportedException(message);
						}

						if (result == null)
						{
							result = new List<Tuple<MemberProperty, bool>>();
						}

						result.Add(Tuple.Create(property, ascending));
					}
				}
			}

			return result;
		}

		private static bool IsOrderedByGroupingProperty(List<CodeMember> members, Tuple<MemberProperty, bool> groupingProperty)
		{
			bool result = true;

			CodeMember previousMember = null;
			foreach (CodeMember member in members)
			{
				if (previousMember != null && previousMember.CompareByMemberProperty(member, groupingProperty) > 0)
				{
					result = false;
					break;
				}

				previousMember = member;
			}

			return result;
		}

		private static IEnumerable<List<CodeMember>> GroupMembers(List<CodeMember> userOrderedTypeMembers, Tuple<MemberProperty, bool> groupingProperty)
		{
			// If userOrderedTypeMembers AND startPointOrderedTypeMembers are both ordered by groupingProperty already,
			// then we can group by it to try to keep members within any existing #regions (assuming the #regions are grouped)
			// when ReorderMembers is called.
			bool canGroup = groupingProperty != null && IsOrderedByGroupingProperty(userOrderedTypeMembers, groupingProperty);
			if (canGroup)
			{
				// If member groups are interlaced (e.g., PropA, MethA, PropB, MethB), then we need to force an
				// overall reordering even if the group members are already in the correct order within the group.
				List<CodeMember> startPointOrderedTypeMembers = new List<CodeMember>(userOrderedTypeMembers);
				startPointOrderedTypeMembers.Sort((x, y) =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return x.CompareByStartPoint(y);
					});
				canGroup = IsOrderedByGroupingProperty(startPointOrderedTypeMembers, groupingProperty);
			}

			IEnumerable<List<CodeMember>> result;
			if (!canGroup)
			{
				// Either we didn't have a supported grouping property or the members weren't grouped that way
				// in the new and original lists, so we need to reorder members using a single group with all members.
				result = new[] { userOrderedTypeMembers };
			}
			else
			{
				// Replace each group separately.  Then the lowest line number of each member in a group
				// will determine where the group members are placed.  This helps keep members inside existing
				// #regions when they've been manually created by groups (e.g., #region Public Properties).
				var groups = userOrderedTypeMembers.GroupBy(member => member.GetValue(groupingProperty.Item1));
				var orderedGroups = groupingProperty.Item2 ? groups.OrderBy(group => group.Key) : groups.OrderByDescending(group => group.Key);
				result = orderedGroups.Select(group => group.ToList());
			}

			return result;
		}

		private static string GetCode(IEnumerable<CodeMember> members, out TextPoint minStartPoint)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			StringBuilder sb = new StringBuilder();
			minStartPoint = null;

			foreach (var member in members)
			{
				string code = member.GetCode(out TextPoint memberStartPoint);

				// Variables can be on adjacent lines unless they contain comments or are declared over multiple lines.
				if (sb.Length > 0 && (member.RequiresSeparatorLine || code.Contains('\n')))
				{
					sb.AppendLine();
				}

				sb.Append(code).AppendLine();

				if (minStartPoint == null || memberStartPoint.LessThan(minStartPoint))
				{
					minStartPoint = memberStartPoint;
				}
			}

			string result = sb.ToString();
			return result;
		}

		private static List<Tuple<CodeElement2, List<CodeElement2>>> FindMembers(CodeElements codeElements, Predicate<CodeElement2> includeMember)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			List<Tuple<CodeElement2, List<CodeElement2>>> result = new List<Tuple<CodeElement2, List<CodeElement2>>>();

			foreach (CodeElement2 element in codeElements)
			{
				switch (element.Kind)
				{
					case vsCMElement.vsCMElementNamespace:
						result.AddRange(FindMembers(((CodeNamespace)element).Members, includeMember));
						break;

					case vsCMElement.vsCMElementClass:
					case vsCMElement.vsCMElementModule: // No CodeModule type exists (for VB modules), but CodeClass2 works.
						result.AddRange(FindMembers(element, ((CodeClass2)element).Members, includeMember));
						break;

					case vsCMElement.vsCMElementStruct:
						result.AddRange(FindMembers(element, ((CodeStruct2)element).Members, includeMember));
						break;

					case vsCMElement.vsCMElementInterface:
						result.AddRange(FindMembers(element, ((CodeInterface2)element).Members, includeMember));
						break;

					case vsCMElement.vsCMElementEnum:
						result.AddRange(FindMembers(element, ((CodeEnum)element).Members, includeMember));
						break;
				}
			}

			return result;
		}

		private static List<Tuple<CodeElement2, List<CodeElement2>>> FindMembers(
			CodeElement2 type,
			CodeElements codeMembers,
			Predicate<CodeElement2> includeMember)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			List<Tuple<CodeElement2, List<CodeElement2>>> result = new List<Tuple<CodeElement2, List<CodeElement2>>>();

			List<CodeElement2> typeMembers = null;
			foreach (CodeElement2 member in codeMembers)
			{
				switch (member.Kind)
				{
					case vsCMElement.vsCMElementFunction:
					case vsCMElement.vsCMElementProperty:
					case vsCMElement.vsCMElementVariable:
					case vsCMElement.vsCMElementEvent:
						// Note: We can't include nested types and let the sorting logic try to move them too
						// because that would invalidate all of their CodeElement position info.  Instead, we'll
						// just recursively get their non-type members below.
						if (includeMember == null || includeMember(member))
						{
							if (typeMembers == null)
							{
								typeMembers = new List<CodeElement2>();
								result.Add(Tuple.Create(type, typeMembers));
							}

							typeMembers.Add(member);
						}

						break;
				}
			}

			// Recursively look for nested classes, structs, interfaces, and enums.
			result.AddRange(FindMembers(codeMembers, includeMember));

			return result;
		}

		private bool IsMemberSelected(CodeElement2 member)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			TextPoint memberStart = member.StartPoint;
			TextPoint memberEnd = member.EndPoint;

			bool result = (memberStart.GreaterThan(this.selectionStart) || memberStart.EqualTo(this.selectionStart))
				&& (memberEnd.LessThan(this.selectionEnd) || memberEnd.EqualTo(this.selectionEnd));
			return result;
		}

		private void ReorderMembers(CodeElement2 type, List<CodeMember> members, bool forceReorder)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string orderedCode = GetCode(members, out TextPoint orderedStartPoint);

			if (!forceReorder)
			{
				List<CodeMember> membersByStartPoint = new List<CodeMember>(members);
				membersByStartPoint.Sort((x, y) => x.CompareByStartPoint(y));
				string originalCode = GetCode(membersByStartPoint, out TextPoint originalStartPoint);

				// If "reordering" won't change anything, then skip the remove(s)+insert so the document
				// won't be "modified" and potentially checked out.
				forceReorder = orderedCode != originalCode;
			}

			if (forceReorder)
			{
				this.ReorderMembers(type, members, orderedCode, orderedStartPoint);
			}
		}

		private void ReorderMembers(CodeElement2 type, IEnumerable<CodeMember> members, string orderedCode, TextPoint startPoint)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Removing members will shift the startPoint back one line.
			// So we'll use the absolute offset to jump back to that insert point.
			int startPointOffset = 0;
			if (startPoint != null)
			{
				startPointOffset = startPoint.AbsoluteCharOffset;
			}

			FileCodeModel2 codeModel = this.textHandler.Document.ProjectItem.FileCodeModel as FileCodeModel2;
			codeModel.BeginBatch();
			try
			{
				foreach (CodeMember member in members)
				{
					member.Remove();
				}
			}
			finally
			{
				codeModel.EndBatch();
			}

			if (startPoint != null)
			{
				EditPoint startEdit = startPoint.CreateEditPoint();
				startEdit.MoveToAbsoluteOffset(startPointOffset);
				startEdit.StartOfLine();

				// If the line above startEdit isn't empty and isn't the start of the class/struct/interface/enum,
				// then insert a blank line so the sortedCode will be separated from the code above it.
				EditPoint lineAboveEdit = startEdit.CreateEditPoint();
				lineAboveEdit.LineUp();
				lineAboveEdit.StartOfLine();
				string lineText = lineAboveEdit.GetText(lineAboveEdit.LineLength).Trim();
				if (lineText.Length != 0
					&& lineAboveEdit.Line > type.StartPoint.Line
					&& (this.language != Language.CSharp || lineText != "{"))
				{
					startEdit.Insert(Environment.NewLine);
				}

				startEdit.Insert(orderedCode);

				// If the line after startEdit isn't empty and isn't the end of the class/struct/interface/enum,
				// then insert a blank line so the sortedCode will be separated from the code below it.
				startEdit.StartOfLine();
				lineText = startEdit.GetText(startEdit.LineLength).Trim();
				if (lineText.Length != 0
					&& startEdit.Line < type.EndPoint.Line
					&& (this.language != Language.CSharp || lineText != "}"))
				{
					startEdit.Insert(Environment.NewLine);
				}
			}
		}

		#endregion
	}
}

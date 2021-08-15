#region Using Directives

// If EnvDTE is used inside the namespace, then its EnvDTE.Language interface is used before VsTools.Language enum.
#pragma warning disable SA1200 // Using directives should be placed correctly
using EnvDTE;
#pragma warning restore SA1200 // Using directives should be placed correctly

#endregion

namespace Menees.VsTools.Sort
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Windows.Media;
	using EnvDTE80;
	using Microsoft.VisualStudio.Shell;

	#endregion

	// This type is required because CodeFunction, CodeProperty, CodeVariable, CodeEvent, etc. don't inherit from a common interface.
	// The {x,nq} suffix for No Quotes came from http://blogs.msdn.com/b/jaredpar/archive/2011/03/18/debuggerdisplay-attribute-best-practices.aspx
	[DebuggerDisplay("{Access} {IsStatic ? \"Static \" : \"\",nq}{Kind} {Name,nq}")]
	internal sealed class CodeMember
	{
		#region Private Data Members

		private static readonly Dictionary<string, string> VbTypeKinds = new()
		{
			{ "Struct", "Structure" },
		};

		private static readonly Dictionary<string, string> VbOverrideKinds = new()
		{
			{ "Abstract", "MustOverride" },
			{ "Override", "Overrides" },
			{ "Sealed", "NotOverridable" },
			{ "Virtual", "Overridable" },
		};

		// Note: The order of these strings is important.  CR LF pairs get collapsed before CR or LF.
		// Then any collapsed whitespace sequences are collapsed even more at the end.
		private static readonly string[] WhitespaceToCollapse = new[] { "\r\n", "\n", "\r", "\t", "  " };

		private readonly Language language;
		private CodeElement2 element;
		private CodeElements parameters;

		#endregion

		#region Constructors

		public CodeMember(CodeElement2 element, CodeElement2 type, VsTools.Language language)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.element = element;
			this.TypeElement = type;
			this.language = language;

			this.Name = element.Name;
			this.RequiresSeparatorLine = element.Kind != vsCMElement.vsCMElementVariable;
			switch (element.Kind)
			{
				case vsCMElement.vsCMElementFunction:
					CodeFunction2 function = (CodeFunction2)element;
					MemberKind kind;
					switch (function.FunctionKind)
					{
						case vsCMFunction.vsCMFunctionConstructor:
							kind = MemberKind.Constructor;
							break;
						case vsCMFunction.vsCMFunctionDestructor:
							kind = MemberKind.Destructor;
							break;
						case vsCMFunction.vsCMFunctionOperator:
							kind = MemberKind.Operator;
							break;
						default:
							kind = MemberKind.Function;
							break;
					}

					this.Initialize(
						kind,
						function.Access,
						function.IsShared,
						function.OverrideKind,
						functionKind: function.FunctionKind,
						parameters: function.Parameters);
					break;

				case vsCMElement.vsCMElementProperty:
					CodeProperty2 property = (CodeProperty2)element;
					this.Initialize(MemberKind.Property, property.Access, property.IsShared, property.OverrideKind, parameters: property.Parameters);
					break;

				case vsCMElement.vsCMElementVariable:
					CodeVariable2 variable = (CodeVariable2)element;
					this.Initialize(MemberKind.Variable, variable.Access, variable.IsShared, constKind: variable.ConstKind);
					break;

				case vsCMElement.vsCMElementEvent:
					CodeEvent evt = (CodeEvent)element;
					this.Initialize(MemberKind.Event, evt.Access, evt.IsShared, evt.OverrideKind);
					break;

				default:
					throw new NotSupportedException("Unsupported element kind: " + element.Kind);
			}
		}

		#endregion

		#region Public Properties

		public MemberKind Kind { get; private set; }

		public string Name { get; }

		public bool IsStatic { get; private set; }

		public MemberAccess Access { get; private set; }

		public int ParameterCount { get; private set; }

		public vsCMOverrideKind? OverrideModifier { get; private set; }

		public vsCMConstKind? ConstModifier { get; private set; }

		// This is currently only used for functions, so we'll leave it typed as vsCMFunction.
		// In the user-visible Options.SortMembersOrder, I want to leave this named KindModifier.
		public vsCMFunction? KindModifier { get; private set; }

		public bool RequiresSeparatorLine { get; }

		public CodeElement2 TypeElement { get; }

		public string TypeDescription
		{
			get
			{
				StringBuilder result = new();

				// Note: Our ImageNameConverter class depends on the TypeKind formatting.
				string typeKind = this.GetName(this.TypeKind, false, VbTypeKinds);
				if (this.language == Language.VB && typeKind == "Struct")
				{
					typeKind = "Structure";
				}
				else if (this.language == Language.CSharp)
				{
					typeKind = typeKind.ToLower();
				}

				result.Append(typeKind);
				result.Append(' ');
				result.Append(this.TypeElement.FullName);
				return result.ToString();
			}
		}

		public string ModifiersDescription
		{
			get
			{
				List<string> tokens = new();

				switch (this.TypeKind)
				{
					// Members in enums and interfaces are always public.
					case vsCMElement.vsCMElementClass:
					case vsCMElement.vsCMElementModule:
					case vsCMElement.vsCMElementStruct:
						{
							string access = this.Access.ToString().Replace("Or", " ");
							if (this.language == Language.VB)
							{
								access = access.Replace("Internal", "Friend");
							}

							tokens.Add(access);
						}

						break;
				}

				if (this.IsStatic)
				{
					tokens.Add(this.language == Language.VB ? "Shared" : "Static");
				}

				if (this.KindModifier != null)
				{
					if (this.language != Language.CSharp || this.KindModifier.Value != vsCMFunction.vsCMFunctionFunction)
					{
						tokens.Add(this.GetName(this.KindModifier.Value, true, null));
					}
				}

				// Interface members are always abstract virtual.
				if (this.OverrideModifier != null && !this.IsInterfaceMember)
				{
					tokens.Add(this.GetName(this.OverrideModifier.Value, true, VbOverrideKinds));
				}

				// Enum fields are always const.
				if (this.ConstModifier != null && !this.IsEnumMember)
				{
					tokens.Add(this.GetName(this.ConstModifier.Value, false, null));
				}

				string result = string.Join(" ", tokens);
				if (this.language == Language.CSharp)
				{
					result = result.ToLower();
				}

				return result;
			}
		}

		public string ParametersDescription
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				string result = null;

				if (this.parameters != null)
				{
					StringBuilder sb = new();
					foreach (CodeParameter2 parameter in this.parameters)
					{
						if (sb.Length > 0)
						{
							sb.Append(", ");
						}

						// If we used parameter.Name and parameter.Type.AsString, we'd end up with fully-qualified
						// type names (including for generic type parameters), and we wouldn't get all of the modifiers
						// (e.g., out and ref).  This way we get the parameter declaration as the user typed it.
						// We'll just collapse the whitespace.  (Note: This will still include any comments entered
						// in the "middle" of the parameter declaration.)
						string declaration = parameter.StartPoint.CreateEditPoint().GetText(parameter.EndPoint).Trim();
						foreach (string whitespace in WhitespaceToCollapse)
						{
							// Use a "do..while" loop when collapsing double spaces so we can shrink each
							// multiple whitespace sequence down to a single space (e.g., "\t\t\t" -> "   " -> " ").
							int startingLength;
							do
							{
								startingLength = declaration.Length;
								declaration = declaration.Replace(whitespace, " ");
							}
							while (whitespace[0] == ' ' && declaration.Length < startingLength);
						}

						sb.Append(declaration);
					}

					result = sb.ToString();
				}

				return result;
			}
		}

		public string ImageName
		{
			get
			{
				string result;
				switch (this.Kind)
				{
					case MemberKind.Variable:
						result = this.IsEnumMember ? "EnumItem" : "Field";
						break;
					case MemberKind.Constructor:
					case MemberKind.Destructor:
					case MemberKind.Function:
						result = "Method";
						break;
					default:
						result = this.Kind.ToString();
						break;
				}

				return result;
			}
		}

		#endregion

		#region Private Properties

		private vsCMElement TypeKind => this.TypeElement.Kind;

		private bool IsEnumMember
		{
			get
			{
				bool result = this.TypeKind == vsCMElement.vsCMElementEnum;
				return result;
			}
		}

		private bool IsInterfaceMember
		{
			get
			{
				bool result = this.TypeKind == vsCMElement.vsCMElementInterface;
				return result;
			}
		}

		#endregion

		#region Public Methods

		public string GetCode(out TextPoint startPoint)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var range = this.GetCodeRange();
			EditPoint startEdit = range.Item1.CreateEditPoint();
			string result = startEdit.GetLines(startEdit.Line, range.Item2.Line + 1);
			startPoint = startEdit;
			return result;
		}

		public void Remove()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Tuple<TextPoint, TextPoint> range = null;
			try
			{
				// To use RemoveMember, we'd have to cast to CodeClass, CodeStruct, CodeInterface, or CodeEnum.
				// However, RemoveMember won't remove a leading comment other than a DocComment,
				// and in VB RemoveMember doesn't remove the whitespace line after the member.
				// So to make our Remove consistent with GetCode, we'll use GetCodeRange and Delete.
				range = this.GetCodeRange();
			}
			catch (ArgumentException ex)
			{
				// In VS 2015 Update 2, an ArgumentException can occur while removing members with explicitly
				// implemented interface member names if the same named non-explicitly implemented member
				// was just removed.  The Roslyn code model seems to get out of sync on the explicit members,
				// and we have to look them back up to remove them.  This happens, for example, on the second
				// GetEnumerator when sorting these lines (from DictionarySet.cs):
				// 		public IEnumerator<T> GetEnumerator() { throw new NotImplementedException(); }
				// 		bool ISet< T >.Add(T item) { throw new NotImplementedException(); }
				// 		IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
				bool rethrow = this.element.InfoLocation != vsCMInfoLocation.vsCMInfoLocationProject;
				if (!rethrow)
				{
					ProjectItem projectItem = this.element.ProjectItem;
					FileCodeModel2 codeModel = (FileCodeModel2)projectItem.FileCodeModel;
					codeModel.Synchronize();
					this.element = MemberSorter.FindMember(codeModel.CodeElements, this.Name);
					if (this.element == null)
					{
						rethrow = true;
					}
					else
					{
						range = this.GetCodeRange();
					}
				}

				if (rethrow)
				{
					throw new ArgumentException($"Unable to remove or reorder element {this.Name} due to VS FileCodeModel2 limitations.", ex);
				}
			}

			if (range != null)
			{
				EditPoint startEdit = range.Item1.CreateEditPoint();
				startEdit.StartOfLine();

				EditPoint endEdit = range.Item2.CreateEditPoint();
				endEdit.LineDown();
				if (endEdit.GetLines(endEdit.Line, endEdit.Line + 1).Trim().Length == 0)
				{
					endEdit.LineDown();
				}

				endEdit.StartOfLine();
				startEdit.Delete(endEdit);
			}
		}

		public object GetValue(MemberProperty property)
		{
			object result;
			switch (property)
			{
				case MemberProperty.Kind:
					result = this.Kind;
					break;
				case MemberProperty.Name:
					result = this.Name;
					break;
				case MemberProperty.IsStatic:
					result = this.IsStatic;
					break;
				case MemberProperty.Access:
					result = this.Access;
					break;
				case MemberProperty.ParameterCount:
					result = this.ParameterCount;
					break;
				case MemberProperty.OverrideModifier:
					result = this.OverrideModifier;
					break;
				case MemberProperty.ConstModifier:
					result = this.ConstModifier;
					break;
				case MemberProperty.KindModifier:
					result = this.KindModifier;
					break;
				default:
					throw new NotSupportedException("Unsupported property: " + property);
			}

			return result;
		}

		public int CompareByMemberProperty(CodeMember other, Tuple<MemberProperty, bool> propertyOrder)
		{
			int result;
			switch (propertyOrder.Item1)
			{
				case MemberProperty.Kind:
					result = (int)this.Kind - (int)other.Kind;
					break;
				case MemberProperty.Name:
					result = string.Compare(this.Name, other.Name);
					break;
				case MemberProperty.IsStatic:
					// Static members come before instance members.
					result = (this.IsStatic ? -1 : 0) - (other.IsStatic ? -1 : 0);
					break;
				case MemberProperty.Access:
					result = (int)this.Access - (int)other.Access;
					break;
				case MemberProperty.ParameterCount:
					result = this.ParameterCount - other.ParameterCount;
					break;
				case MemberProperty.OverrideModifier:
					result = CompareModifiers((int?)this.OverrideModifier, (int?)other.OverrideModifier);
					break;
				case MemberProperty.ConstModifier:
					result = CompareModifiers((int?)this.ConstModifier, (int?)other.ConstModifier);
					break;
				case MemberProperty.KindModifier:
					result = CompareModifiers((int?)this.KindModifier, (int?)other.KindModifier);
					break;
				default:
					throw new NotSupportedException("Unsupported property: " + propertyOrder.Item1);
			}

			// Negate the result if we're supposed to sort this property in descending order.
			if (!propertyOrder.Item2)
			{
				result = -result;
			}

			return result;
		}

		public int CompareByMemberProperties(CodeMember other, IEnumerable<Tuple<MemberProperty, bool>> sortOrder)
		{
			int result = 0;
			foreach (Tuple<MemberProperty, bool> tuple in sortOrder)
			{
				result = this.CompareByMemberProperty(other, tuple);
				if (result != 0)
				{
					break;
				}
			}

			return result;
		}

		public int CompareByStartPoint(CodeMember other)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			int result = 0;

			TextPoint thisStart = this.element.StartPoint;
			TextPoint otherStart = other.element.StartPoint;
			if (thisStart.LessThan(otherStart))
			{
				result = -1;
			}
			else if (otherStart.LessThan(thisStart))
			{
				result = 1;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static int CompareModifiers(int? x, int? y)
		{
			// This value needs to be less than int.MaxValue (in case we add or subtract with it during comparison)
			// but more than any kind enum field value (e.g., vsCMFunction2.vsCMFunctionRaiseEvent).
			const int SortAtEnd = 0x1000000;
			int result = (x ?? SortAtEnd) - (y ?? SortAtEnd);
			return result;
		}

		private string GetName<TEnum>(TEnum value, bool treatLikeFlags, IDictionary<string, string> visualBasicNames)
		{
			List<string> tokens = new();
			if (!treatLikeFlags)
			{
				tokens.Add(value.ToString());
			}
			else
			{
				// Sometimes the TEnum values will be bitwise combinations (e.g., abstract virtual for vsCMOverrideKind),
				// but the TEnum type won't be tagged with the [Flags] attribute.  So we have to do the [Flags] logic
				// manually, and we have to use spaces instead of '|' separators.
				ulong valueNumber = Convert.ToUInt64(value);
				string[] valueNames = Enum.GetNames(typeof(TEnum));
				Array valueNumbers = Enum.GetValues(typeof(TEnum));
				for (int i = 0; i < valueNames.Length; i++)
				{
					ulong number = Convert.ToUInt64(valueNumbers.GetValue(i));
					if ((valueNumber & number) != 0)
					{
						string name = valueNames[i];
						tokens.Add(name);
					}
				}
			}

			StringBuilder sb = new();
			string typeName = typeof(TEnum).Name;
			foreach (string token in tokens)
			{
				string name = token;
				if (name.StartsWith(typeName))
				{
					name = name.Substring(typeName.Length);
				}

				if (this.language == Language.VB && visualBasicNames != null && visualBasicNames.TryGetValue(name, out string visualBasicName))
				{
					name = visualBasicName;
				}

				if (sb.Length > 0)
				{
					sb.Append(' ');
				}

				sb.Append(name);
			}

			string result = sb.ToString();
			return result;
		}

		private void Initialize(
			MemberKind kind,
			vsCMAccess access,
			bool isStatic,
			vsCMOverrideKind? overrideKind = null,
			vsCMConstKind? constKind = null,
			vsCMFunction? functionKind = null,
			CodeElements parameters = null)
		{
			this.Kind = kind;
			this.IsStatic = isStatic;
			switch (access)
			{
				case vsCMAccess.vsCMAccessAssemblyOrFamily:
					this.Access = MemberAccess.AssemblyOrFamily;
					break;
				case vsCMAccess.vsCMAccessDefault:
					this.Access = MemberAccess.Default;
					break;
				case vsCMAccess.vsCMAccessPrivate:
					this.Access = MemberAccess.Private;
					break;
				case vsCMAccess.vsCMAccessProject:
					this.Access = MemberAccess.Internal;
					break;
				case vsCMAccess.vsCMAccessProjectOrProtected:
					this.Access = MemberAccess.ProtectedOrInternal;
					break;
				case vsCMAccess.vsCMAccessProtected:
					this.Access = MemberAccess.Protected;
					break;
				case vsCMAccess.vsCMAccessPublic:
					this.Access = MemberAccess.Public;
					break;
				case vsCMAccess.vsCMAccessWithEvents:
					this.Access = MemberAccess.WithEvents;
					break;
				default:
					this.Access = MemberAccess.Unknown;
					break;
			}

			if (overrideKind != null && overrideKind.Value != vsCMOverrideKind.vsCMOverrideKindNone)
			{
				this.OverrideModifier = overrideKind.Value;
			}

			if (constKind != null && constKind.Value != vsCMConstKind.vsCMConstKindNone)
			{
				this.ConstModifier = constKind.Value;
			}

			if (functionKind != null && functionKind.Value != vsCMFunction.vsCMFunctionOther)
			{
				this.KindModifier = functionKind.Value;
			}

			ThreadHelper.ThrowIfNotOnUIThread();
			if (parameters != null)
			{
				this.parameters = parameters;
				this.ParameterCount = parameters.Count;
			}
		}

		private Tuple<TextPoint, TextPoint> GetCodeRange()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// The C# and VB code models don't return comments with vsCMPartWholeWithAttributes
			// (even though some of the member types have Comment and DocComment properties
			// and the code model's RemoveMember will remove the comments).  So we have to
			// grab any attached comment lines manually.
			TextPoint startPoint = this.element.GetStartPoint(vsCMPart.vsCMPartWholeWithAttributes);
			TextPoint endPoint = this.element.GetEndPoint(vsCMPart.vsCMPartWholeWithAttributes);
			string commentRegexPattern;
			switch (this.language)
			{
				case Language.CSharp:
					// Look for lines starting with optional whitespace followed by //, /*, */, or *.
					commentRegexPattern = @"^\s*(//|/\*|\*/|\*)";
					break;

				case Language.VB:
					// Look for lines starting with optional whitespace followed by '.
					commentRegexPattern = @"^\s*'";
					break;

				default:
					throw new NotSupportedException("Unsupported language: " + this.language);
			}

			EditPoint startEdit = startPoint.CreateEditPoint();
			startEdit.LineUp();
			while (!startEdit.AtStartOfDocument
				&& Regex.IsMatch(startEdit.GetLines(startEdit.Line, startEdit.Line + 1), commentRegexPattern))
			{
				startEdit.LineUp();
				startEdit.StartOfLine();
			}

			startEdit.LineDown();
			startEdit.StartOfLine();

			Tuple<TextPoint, TextPoint> result = Tuple.Create((TextPoint)startEdit, endPoint);
			return result;
		}

		#endregion
	}
}

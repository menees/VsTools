namespace Menees.VsTools.Sort
{
	#region LineOptions

	internal enum LineOptions
	{
		None = 0,
		CaseSensitive = 1,
		ByOrdinal = 2,
		Descending = 4,
		IgnoreWhitespace = 8,
		IgnorePunctuation = 16,
		EliminateDuplicates = 32,
		ByLength = 64,
		WholeLines = 128,
	}

	#endregion

	#region MemberAccess

	internal enum MemberAccess
	{
		// These member kinds are in order by StyleCop's preferences (SA1202):
		Unknown,
		Public,
		Internal,
		ProtectedOrInternal,
		Protected,
		Private,
		Default,
		AssemblyOrFamily,
		WithEvents,
	}

	#endregion

	#region MemberKind

	internal enum MemberKind
	{
		// These member kinds are in order by StyleCop's preferences (SA1201):
		// http://stackoverflow.com/questions/150479/order-of-items-in-classes-fields-properties-constructors-methods/310967#310967
		// http://www.stylecop.com/docs/Ordering%20Rules.html
		Unknown,
		Variable,
		Constructor,
		Destructor,
		Event,
		Property,
		Function,
		Operator,
	}

	#endregion

	#region MemberProperty

	internal enum MemberProperty
	{
		// Note: These enum field names are used in the Description attribute for Options.SortMembersOrder.
		Kind,
		Name,
		IsStatic,
		Access,
		ParameterCount,
		OverrideModifier,
		ConstModifier,
		KindModifier,
	}

	#endregion
}

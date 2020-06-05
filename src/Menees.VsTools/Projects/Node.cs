namespace Menees.VsTools.Projects
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal sealed class Node
	{
		#region Private Data Members

		private string category;

		#endregion

		#region Constructors

		public Node(string id, NodeType type)
		{
			this.Id = id;
			this.Type = type;
		}

		#endregion

		#region Public Properties

		public string Id { get; }

		public string Label => this.Id;

		public string Category
		{
			get => this.category ?? this.Type.ToString();
			set => this.category = value;
		}

		public NodeType Type { get; }

		public List<(Node, LinkType)> References { get; } = new List<(Node, LinkType)>();

		#endregion
	}
}

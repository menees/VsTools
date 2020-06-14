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
		#region Constructors

		public Node(string id, NodeType type, string reference)
		{
			this.Id = id;
			this.Type = type;
			this.Reference = reference;
		}

		#endregion

		#region Public Properties

		public string Id { get; }

		public string Label => this.Id;

		public string Category => this.Type.ToString();

		public NodeType Type { get; }

		public List<(Node, LinkType)> References { get; } = new List<(Node, LinkType)>();

		public bool IsRoot { get; set; }

		public string Reference { get; }

		#endregion
	}
}

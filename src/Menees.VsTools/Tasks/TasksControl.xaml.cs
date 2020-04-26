namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Navigation;
	using System.Windows.Shapes;
	using System.Xml.Linq;
	using Microsoft.VisualStudio.Shell;

	#endregion

	/// <summary>
	/// Interaction logic for TasksControl.xaml
	/// </summary>
	internal partial class TasksControl : UserControl
	{
		#region Private Data Members

		private readonly TasksWindow window;
		private bool initiallyEnabled;
		private DateTime lastSortMenuClosed;
		private bool isLoading;

		#endregion

		#region Constructors

		internal TasksControl(TasksWindow window)
		{
			this.InitializeComponent();
			this.window = window;
			this.isLoading = true;

			INotifyCollectionChanged changed = this.tasks.Items.SortDescriptions;
			changed.CollectionChanged += this.Sort_Changed;

			// The rest of the initialization must wait until the Loaded event handler
			// because we need access to the window's package, which won't be set
			// until after we and the window are finished constructing.
		}

		#endregion

		#region Private Properties

		private MainPackage Package
		{
			get
			{
				MainPackage result = null;

				if (this.window != null)
				{
					// This won't be set until after the TasksWindow constructor finishes.
					// It's not a constructor parameter, but VS sets it immediately after the
					// Package.CreateToolWindow (or FindToolWindow) call finishes.  So we have
					// to return null for a brief introductory period while the XAML is initializing.
					result = this.window.Package as MainPackage;
				}

				return result;
			}
		}

		private CommentTask SelectedTask
		{
			get
			{
				CommentTask result = this.tasks.SelectedItem as CommentTask;
				return result;
			}
		}

		#endregion

		#region Internal Methods

		internal static bool TryGetSortDescription(SortDescriptionCollection sorting, string propertyName, out SortDescription sortDescription, out int index)
		{
			bool result = false;
			index = -1;
			sortDescription = default;

			int count = sorting.Count;
			for (int i = 0; i < count; i++)
			{
				SortDescription current = sorting[i];
				if (current.PropertyName == propertyName)
				{
					result = true;
					sortDescription = current;
					index = i;
					break;
				}
			}

			return result;
		}

		#endregion

		#region Private Methods

		private void SortBy(string propertyName)
		{
			SortDescriptionCollection sorting = this.tasks.Items.SortDescriptions;
			if (TryGetSortDescription(sorting, propertyName, out SortDescription sort, out int index))
			{
				ListSortDirection direction = sort.Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
				sort = new SortDescription(propertyName, direction);
				sorting[index] = sort;
			}
			else
			{
				sort = new SortDescription(propertyName, ListSortDirection.Ascending);
				sorting.Add(sort);
			}
		}

		private void GoToSelectedTask()
		{
			CommentTask task = this.SelectedTask;
			if (task != null)
			{
				task.GoToComment();
			}
		}

		private void UpdateWarning(Options options)
		{
			string message = null;
			if (options.EnableCommentScans)
			{
				if (!this.initiallyEnabled)
				{
					message = "Task comment scanning will be enabled after Visual Studio is restarted.";
				}
			}
			else
			{
				if (this.initiallyEnabled)
				{
					message = "Task comment scanning has been paused and will be disabled after Visual Studio is restarted.";
				}
				else
				{
					message = "Task comment scanning is currently disabled (under Tools → Options → Menees VS Tools).";
				}
			}

			this.warning.Text = message;
			this.warningBorder.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
		}

		#endregion

		#region Private Event Handlers

		private void CopyAllTasks_Click(object sender, RoutedEventArgs e)
		{
			StringBuilder sb = new StringBuilder();

			using (StringWriter writer = new StringWriter(sb))
			{
				CsvUtility.WriteLine(writer, new object[] { "Comment", "Priority", "File", "Line", "Project" });
				foreach (CommentTask task in this.tasks.Items)
				{
					CsvUtility.WriteLine(writer, new object[] { task.Comment, task.Priority, task.FilePath, task.Line, task.Project });
				}
			}

			// .NET sets the text as Unicode text, but Excel wants CSV data as UTF-8 bytes.
			string csv = sb.ToString();
			DataObject data = new DataObject();
			data.SetText(csv);
			byte[] bytes = Encoding.UTF8.GetBytes(csv);
			using (var stream = new MemoryStream(bytes))
			{
				data.SetData(DataFormats.CommaSeparatedValue, stream);
				Clipboard.SetDataObject(data, true);
			}
		}

		private void CopyComment_Click(object sender, RoutedEventArgs e)
		{
			CommentTask task = this.SelectedTask;
			if (task != null)
			{
				Clipboard.SetText(task.Comment);
			}
		}

		private void CopyTask_Click(object sender, RoutedEventArgs e)
		{
			CommentTask task = this.SelectedTask;
			if (task != null)
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(task.Comment);
				sb.Append(task.Priority).AppendLine();
				sb.AppendLine(task.FilePath);
				sb.Append(task.Line).AppendLine();
				sb.AppendLine(task.Project);

				Clipboard.SetText(sb.ToString());
			}
		}

		private void ResetSort_Click(object sender, RoutedEventArgs e)
		{
			this.tasks.Items.SortDescriptions.Clear();
		}

		private void Sort_Changed(object sender, NotifyCollectionChangedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (sender is SortDescriptionCollection sorting)
			{
				this.resetSort.IsEnabled = sorting.Count > 0;

				foreach (MenuItem menuItem in this.sort.ContextMenu.Items.OfType<MenuItem>())
				{
					string propertyName = menuItem.Tag as string;
					if (!string.IsNullOrEmpty(propertyName))
					{
						if (TryGetSortDescription(sorting, propertyName, out SortDescription sort, out int index))
						{
							string imageName = sort.Direction == ListSortDirection.Ascending ? "SortAscending.png" : "SortDescending.png";
							Uri uri = ImageNameToSourceConverter.CreateResourceUri(imageName);
							BitmapImage source = new BitmapImage(uri);
							menuItem.Icon = new Image { Source = source };
						}
						else
						{
							menuItem.Icon = null;
						}
					}
				}

				XElement status = new XElement("TasksStatus");
				foreach (SortDescription sort in sorting)
				{
					XElement sortBy = new XElement(
						nameof(this.SortBy),
						new XAttribute("PropertyName", sort.PropertyName),
						new XAttribute("Direction", sort.Direction));
					status.Add(sortBy);
				}

				if (!this.isLoading)
				{
					Options options = MainPackage.TaskOptions;
					options.TasksStatusXml = status.ToString();
					options.SaveSettingsToStorage();
				}
			}
		}

		private void Sort_Click(object sender, RoutedEventArgs e)
		{
			// Clicking Sort while the menu is open should just close the menu and not open it again immediately.
			// Unfortunately, the ContextMenu.IsOpen property is already false by the time control gets here,
			// so we can only detect if it was just open by using its last closed time.
			if (DateTime.UtcNow - this.lastSortMenuClosed >= TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime))
			{
				e.Handled = true;

				if (this.sort.IsMouseOver)
				{
					// Turn this (left) Click event into a RightButtonUp event.
					// This allows the ContextMenuClosing event to fire later.
					// http://stackoverflow.com/a/27694260/1882616
					var mouseDownEvent = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
						{
							RoutedEvent = Mouse.MouseUpEvent,
							Source = sender,
						};

					InputManager.Current.ProcessInput(mouseDownEvent);
				}
				else
				{
					// If they focused the Sort button and pressed Enter, then just show the context menu.
					// This will NOT fire the ContextMenuClosing event later, but at least the placement is correct.
					ContextMenu menu = this.sort.ContextMenu;
					menu.Placement = PlacementMode.Bottom;
					menu.PlacementTarget = this.sort;
					menu.IsOpen = true;
				}
			}

			// Reset so that if they click Sort again really fast then we'll open the menu again.
			this.lastSortMenuClosed = DateTime.MinValue;
		}

		private void Sort_ContextMenuClosing(object sender, ContextMenuEventArgs e)
		{
			this.lastSortMenuClosed = DateTime.UtcNow;
		}

		private void SortBy_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem menuItem)
			{
				string propertyName = menuItem.Tag as string;
				if (!string.IsNullOrEmpty(propertyName))
				{
					this.SortBy(propertyName);
				}
			}
		}

		private void TaskProvider_TasksChanged(object sender, TasksChangedEventArgs e)
		{
			foreach (CommentTask task in e.RemovedTasks)
			{
				this.tasks.Items.Remove(task);
			}

			foreach (CommentTask task in e.AddedTasks)
			{
				this.tasks.Items.Add(task);
			}

			// This is necessary to make new items respect existing SortDescriptions.
			this.tasks.Items.Refresh();
		}

		private void Tasks_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			bool hasSelectedTask = this.SelectedTask != null;
			foreach (MenuItem menuItem in this.tasks.ContextMenu.Items.OfType<MenuItem>().Where(item => item.Tag as string == nameof(this.SelectedTask)))
			{
				menuItem.IsEnabled = hasSelectedTask;
			}
		}

		private void Tasks_DoubleClick(object sender, MouseButtonEventArgs e)
		{
			this.GoToSelectedTask();
		}

		private void Tasks_GoTo(object sender, RoutedEventArgs e)
		{
			this.GoToSelectedTask();
		}

		private void Tasks_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				this.GoToSelectedTask();
				e.Handled = true;
			}
		}

		private void TasksControl_Loaded(object sender, RoutedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// The Loaded event will be raised again whenever the tool window tab is changed,
			// so we must make sure this event handler isn't called again.
			this.Loaded -= this.TasksControl_Loaded;

			if (this.isLoading)
			{
				MainPackage package = this.Package;
				if (package == null)
				{
					throw new InvalidOperationException("The tasks control can't be loaded without its associated package.");
				}

				Options options = MainPackage.TaskOptions;
				if (options == null)
				{
					throw new InvalidOperationException("The tasks control can't be loaded without its associated options.");
				}

				CommentTaskProvider provider = package.TaskProvider;
				if (provider != null)
				{
					provider.TasksChanged += this.TaskProvider_TasksChanged;
				}

				this.initiallyEnabled = options.EnableCommentScans;
				this.UpdateWarning(options);

				options.Applied += (s, a) =>
				{
					if (this.IsLoaded)
					{
						this.UpdateWarning((Options)s);
					}
				};

				this.resetSort.IsEnabled = false;
				string statusXml = options.TasksStatusXml;
				if (!string.IsNullOrEmpty(statusXml))
				{
					SortDescriptionCollection sorting = this.tasks.Items.SortDescriptions;
					XElement status = XElement.Parse(statusXml);
					foreach (XElement sortBy in status.Elements(nameof(this.SortBy)))
					{
						SortDescription sort = new SortDescription(
							sortBy.GetAttributeValue("PropertyName"),
							sortBy.GetAttributeValue("Direction", ListSortDirection.Ascending));
						sorting.Add(sort);
					}
				}

				this.isLoading = false;
			}
		}

		#endregion
	}
}

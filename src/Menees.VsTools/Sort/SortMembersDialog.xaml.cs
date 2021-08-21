namespace Menees.VsTools.Sort
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Diagnostics;
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
	using System.Windows.Shapes;
	using Microsoft.VisualStudio.PlatformUI;

	#endregion

	internal partial class SortMembersDialog : DialogWindow
	{
		#region Private Data Members

		private ObservableCollection<CodeMember> observableMembers;
		private Action<List<CodeMember>> sortMembers;
		private Point? dragStart;

		#endregion

		#region Constructors

		public SortMembersDialog()
		{
			this.InitializeComponent();
			this.UpdateControlStates();
		}

		#endregion

		#region Public Methods

		public bool Execute(List<CodeMember> codeMembers, Options options, Action<List<CodeMember>> sortMembers)
		{
			this.observableMembers = new ObservableCollection<CodeMember>(codeMembers);
			this.list.ItemsSource = this.observableMembers;
			this.sortMembers = sortMembers;
			this.list.SelectAll();
			this.onlyShowWhenShiftIsPressed.IsChecked = options.OnlyShowSortMembersDialogWhenShiftIsPressed;

			// http://www.wpf-tutorial.com/listview-control/listview-grouping/
			// Note: We'll group by the TypeDescription property, which will be bound to the GroupItem.Name property.
			// Then the XAML will use the GroupItem.Name to extract the type kind image (via ImageNameToSourceConverter).
			CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(this.list.ItemsSource);
			PropertyGroupDescription groupDescription = new("TypeDescription");
			view.GroupDescriptions.Add(groupDescription);

			bool result = false;
			if (this.ShowModal().GetValueOrDefault())
			{
				codeMembers.Clear();
				codeMembers.AddRange(this.observableMembers);

				options.OnlyShowSortMembersDialogWhenShiftIsPressed = this.onlyShowWhenShiftIsPressed.IsChecked.GetValueOrDefault();
				options.SaveSettingsToStorage();

				result = true;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static bool IsInBottomHalfOfItem(DragEventArgs dragArgs, ListViewItem item)
		{
			bool result = false;

			Point dropPoint = dragArgs.GetPosition(item);
			double itemHeight = item.ActualHeight;

			// If Y is past itemHeight, then still treat it as "in" the bottom half.  That way when we get a target item
			// and the cursor is one pixel past it but not yet in the next item, then we won't draw the drop target line
			// above the current item.
			if (dropPoint.Y >= itemHeight / 2)
			{
				result = true;
			}

			return result;
		}

		private static T GetVisualChild<T>(Visual parent)
			where T : Visual
		{
			T child = null;

			int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < numVisuals; i++)
			{
				Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
				child = v as T;
				if (child == null)
				{
					child = GetVisualChild<T>(v);
				}

				if (child != null)
				{
					break;
				}
			}

			return child;
		}

		private ListViewItem GetItemTargetInfo(Point listViewPoint)
			=> Utilities.GetItemTarget<ListViewItem>(this.list, listViewPoint);

		private Tuple<ListViewItem, List<CodeMember>> GetDropTargetInfo(DragEventArgs e)
		{
			Tuple<ListViewItem, List<CodeMember>> result = null;

			Point dropPoint = e.GetPosition(this.list);
			ListViewItem listViewItem = this.GetItemTargetInfo(dropPoint);
			if (listViewItem != null)
			{
				if (listViewItem.Content is CodeMember targetMember && e.Data.GetDataPresent(typeof(List<CodeMember>)))
				{
					// Make sure they're dropping members within the same type they originated from.
					// We don't support dragging members into different types.
					List<CodeMember> selectedMembers = (List<CodeMember>)e.Data.GetData(typeof(List<CodeMember>));
					if (selectedMembers.Count > 0 && selectedMembers[0].TypeElement == targetMember.TypeElement)
					{
						result = Tuple.Create(listViewItem, selectedMembers);
					}
				}
			}

			return result;
		}

		private LineDropTargetAdorner GetAdorner(bool createIfNecessary)
		{
			AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.list);

			LineDropTargetAdorner result = (adornerLayer.GetAdorners(this.list) ?? Enumerable.Empty<Adorner>())
				.OfType<LineDropTargetAdorner>().FirstOrDefault();
			if (result == null && createIfNecessary)
			{
				result = new LineDropTargetAdorner(this.list);
				adornerLayer.Add(result);
			}

			return result;
		}

		private void ClearDragMembers()
		{
			this.dragStart = null;
			LineDropTargetAdorner adorner = this.GetAdorner(false);
			if (adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.list);
				adornerLayer.Remove(adorner);
			}
		}

		private void UpdateControlStates()
		{
			this.sortButton.IsEnabled = this.list.SelectedItems.Count >= 2;
		}

		#endregion

		#region Private Event Handlers

		private void OkayButton_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void Sort_Click(object sender, RoutedEventArgs e)
		{
			if (this.sortMembers != null && this.list.SelectedItems.Count > 0)
			{
				List<CodeMember> selectedMembers = this.list.SelectedItems.Cast<CodeMember>().ToList();
				Dictionary<CodeMember, int> selectedIndexes = selectedMembers
					.ToDictionary(member => member, member => this.observableMembers.IndexOf(member));

				foreach (var typeGroup in selectedMembers.GroupBy(member => member.TypeElement))
				{
					List<CodeMember> typeMembers = typeGroup.ToList();
					this.sortMembers(typeMembers);

					// Get the selected indexes for the current type members in index order (i.e., from top to bottom in the list).
					List<int> sortedIndexes = typeMembers.Select(member => selectedIndexes[member]).OrderBy(index => index).ToList();
					int itemCount = sortedIndexes.Count;

					// First, we have to take all the items out of the list.  The ListView's data binding
					// logic gets really confused if we ever have the same item in the list multiple times,
					// and it can't restore SelectedItems properly after that.  By nulling all the items
					// out first, we can replace the items in sorted order, and data binding stays happy.
					foreach (int index in sortedIndexes)
					{
						this.observableMembers[index] = null;
					}

					// Put the sorted members back in the type's selected ListView rows (but in sorted order).
					for (int i = 0; i < itemCount; i++)
					{
						CodeMember member = typeMembers[i];
						int index = sortedIndexes[i];
						this.observableMembers[index] = member;
					}
				}

				// Restore the list's selected items now that they're replaced in sorted order.
				// Note: this.list.SelectedItems should be empty now because we temporarily
				// nulled out all the selected items above.
				foreach (var pair in selectedIndexes)
				{
					this.list.SelectedItems.Add(pair.Key);
				}
			}
		}

		// Note: We have to use the Preview event because the ListViewItem (or some child content)
		// handles MouseLeftButtonDown and can prevent it from being raised on the ListView.
		private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Make sure we're in the client area of the control (i.e., not on a header or scroll bar)
			// and on a ListViewItem.
			Point point = e.GetPosition(this.list);
			ListViewItem item = this.GetItemTargetInfo(point);
			if (item != null)
			{
				// Note: I tried using this.CaptureMouse(), but it caused more problems than it solved.
				// I'll just let the call to DoDragDrop handle mouse capture.
				this.dragStart = e.GetPosition(this.list);
			}
		}

		private void List_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.ClearDragMembers();
		}

		private void List_MouseMove(object sender, MouseEventArgs e)
		{
			if (this.dragStart != null)
			{
				Point mousePosition = e.GetPosition(this.list);
				Vector diff = this.dragStart.Value - mousePosition;

				if (e.LeftButton == MouseButtonState.Pressed
					&& this.list.SelectedItems.Count > 0
					&& (Math.Abs(diff.X) > Math.Abs(SystemParameters.MinimumHorizontalDragDistance)
					|| Math.Abs(diff.Y) > Math.Abs(SystemParameters.MinimumVerticalDragDistance)))
				{
					List<CodeMember> selectedMembers = this.list.SelectedItems.Cast<CodeMember>().ToList();

					// Require all the selected members to be within the same type.  Dragging members
					// from multiple types would be ambiguous since we can only drop them in one type.
					if (selectedMembers.GroupBy(member => member.TypeElement).Count() == 1)
					{
						try
						{
							DragDrop.DoDragDrop(this.list, selectedMembers, DragDropEffects.Move);
						}
						finally
						{
							this.ClearDragMembers();
						}
					}
				}
			}
		}

		private void List_DragOver(object sender, DragEventArgs e)
		{
			e.Effects = DragDropEffects.None;

			Tuple<ListViewItem, List<CodeMember>> dropTargetInfo = this.GetDropTargetInfo(e);
			if (dropTargetInfo != null)
			{
				e.Effects = e.AllowedEffects;
				LineDropTargetAdorner adorner = this.GetAdorner(true);

				ListViewItem item = dropTargetInfo.Item1;
				double position = this.list.PointFromScreen(item.PointToScreen(default)).Y;
				position += IsInBottomHalfOfItem(e, item) ? item.ActualHeight : -1;

				// Our adorner won't be clipped, so we need to draw it short enough to not overlap the scroll bar, etc.
				ScrollViewer viewer = GetVisualChild<ScrollViewer>(this.list);
				double scrollBarWidth = viewer.ComputedVerticalScrollBarVisibility == Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0;
				double width = Math.Min(item.ActualWidth, viewer.ActualWidth - scrollBarWidth);
				adorner.Show(position, width);
			}
			else
			{
				LineDropTargetAdorner adorner = this.GetAdorner(false);
				if (adorner != null)
				{
					adorner.Hide();
				}
			}

			// We have to indicate that we've handled the DragOver and set the desired Effects.
			// This tells the system it doesn't need to use the default DragOver handler, and it lets
			// the default GiveFeedback handler show the proper cursor when dropping isn't allowed.
			e.Handled = true;
		}

		private void List_Drop(object sender, DragEventArgs e)
		{
			Tuple<ListViewItem, List<CodeMember>> dropTargetInfo = this.GetDropTargetInfo(e);
			if (dropTargetInfo != null)
			{
				this.ClearDragMembers();

				ListViewItem listItem = dropTargetInfo.Item1;
				List<CodeMember> selectedMembers = dropTargetInfo.Item2;

				int targetIndex = this.list.Items.IndexOf(listItem.Content);
				if (targetIndex >= 0)
				{
					// If the drop point is in the bottom half of the ListViewItem, then increment targetIndex
					// so we'll insert the items after the target ListViewItem instead of before it.  This allows
					// people to drop between items and (almost) after the last item as expected.
					if (IsInBottomHalfOfItem(e, listItem))
					{
						targetIndex++;
					}

					// The selected members may not be in top-to-bottom order in the list (depending on the order
					// they were clicked and/or added to the ListView's SelectedItems collection), so we need to
					// get them in visual order so we can re-insert them that way.
					List<Tuple<CodeMember, int>> orderedSelection = selectedMembers
						.Select(member => Tuple.Create(member, this.list.Items.IndexOf(member)))
						.OrderBy(tuple => tuple.Item2).ToList();
					int numberOfSelectedItemsBeforeTarget = orderedSelection.Count(tuple => tuple.Item2 < targetIndex);
					targetIndex -= numberOfSelectedItemsBeforeTarget;

					foreach (var tuple in orderedSelection)
					{
						CodeMember member = tuple.Item1;
						this.observableMembers.Remove(member);
					}

					foreach (var tuple in orderedSelection)
					{
						CodeMember member = tuple.Item1;
						this.observableMembers.Insert(targetIndex++, member);
						this.list.SelectedItems.Add(member);
					}
				}
			}
		}

		private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.UpdateControlStates();
		}

		#endregion

		#region Private Types

		// https://social.msdn.microsoft.com/Forums/vstudio/en-US/39dd2aa4-9f42-4edd-9fe7-600f718e5277/adorner-on-listview?forum=wpf
		internal sealed class LineDropTargetAdorner : Adorner
		{
			#region Private Data Members

			private double position;
			private double width;

			#endregion

			#region Constructors

			public LineDropTargetAdorner(UIElement adornedElement)
				: base(adornedElement)
			{
				this.position = double.NaN;
				this.width = double.NaN;
			}

			#endregion

			#region Public Methods

			public void Hide()
			{
				this.Show(double.NaN, double.NaN);
			}

			public void Show(double position, double width)
			{
				this.position = position;
				this.width = width;
				this.InvalidateVisual();
			}

			#endregion

			#region Protected Methods

			protected override void OnRender(DrawingContext drawingContext)
			{
				if (!double.IsNaN(this.position))
				{
					const int LeftGap = 4;
					Point left = new(LeftGap, this.position);

					double rightEnd = double.IsNaN(this.width) ? this.AdornedElement.RenderSize.Width : this.width;
					Point right = new(rightEnd, this.position);
					Pen pen = new(SystemColors.HotTrackBrush, 1);
					drawingContext.DrawLine(pen, left, right);
				}
			}

			#endregion
		}

		#endregion
	}
}

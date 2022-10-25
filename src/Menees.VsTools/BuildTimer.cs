namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using EnvDTE;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	#endregion

	internal sealed class BuildTimer : IDisposable
	{
		#region Private Data Members

		private readonly MainPackage package;
		private readonly DTE dte;
		private readonly Dictionary<string, DateTime> projectConfigBegan = new(StringComparer.CurrentCultureIgnoreCase);

		private BuildEvents buildEvents;
		private DateTime? buildBegan;
		private vsBuildAction buildAction;

		#endregion

		#region Constructors

		public BuildTimer(MainPackage package)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.package = package;
			this.dte = (DTE)this.GetService(typeof(SDTE));

			// We must hold a reference to the returned BuildEvents instance.  Otherwise, we won't get callbacks/events.
			// https://social.msdn.microsoft.com/Forums/vstudio/en-US/1d5da1f9-fb03-4d78-804b-d708557d3331/advisebuildstatuscallback?forum=vsx
			this.buildEvents = this.dte.Events.BuildEvents;
			this.buildEvents.OnBuildBegin += this.BuildEvents_OnBuildBegin;
			this.buildEvents.OnBuildDone += this.BuildEvents_OnBuildDone;
			this.buildEvents.OnBuildProjConfigBegin += this.BuildEvents_OnBuildProjConfigBegin;
			this.buildEvents.OnBuildProjConfigDone += this.BuildEvents_OnBuildProjConfigDone;
		}

		#endregion

		#region Private Properties

		private static BuildTiming Timing
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return MainPackage.GeneralOptions.BuildTiming;
			}
		}

		private bool IsTimedBuild
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return this.buildBegan != null && Timing != BuildTiming.None;
			}
		}

		#endregion

		#region Public Methods

		public void Dispose()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (this.buildEvents != null)
			{
				this.buildEvents.OnBuildBegin -= this.BuildEvents_OnBuildBegin;
				this.buildEvents.OnBuildDone -= this.BuildEvents_OnBuildDone;
				this.buildEvents.OnBuildProjConfigBegin -= this.BuildEvents_OnBuildProjConfigBegin;
				this.buildEvents.OnBuildProjConfigDone -= this.BuildEvents_OnBuildProjConfigDone;

				this.buildEvents = null;
			}
		}

		#endregion

		#region Private Methods

		private static string GetProjConfigDisplayName(string project, string projectConfig, string platform)
		{
			string result = $"Project: {Path.GetFileNameWithoutExtension(project)}, Configuration: {projectConfig} {platform}";
			return result;
		}

		private object GetService(Type serviceType) => this.package.ServiceProvider.GetService(serviceType);

		private void Clear(DateTime? buildBegan, vsBuildAction buildAction)
		{
			this.projectConfigBegan.Clear();
			this.buildBegan = buildBegan;
			this.buildAction = buildAction;
		}

		private void OutputString(string value)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// https://github.com/VsixCommunity/Community.VisualStudio.Toolkit/discussions/34?sort=top#discussioncomment-1062803
			IVsOutputWindowPane output = this.package.GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
			if (output is IVsOutputWindowPaneNoPump noPump)
			{
				noPump.OutputStringNoPump(value);
			}
			else
			{
				int hr = output.OutputStringThreadSafe(value);
				if (ErrorHandler.Failed(hr))
				{
					Debug.WriteLine($"HRESULT {hr:X8} returned when trying to output: {value}");
				}
			}
		}

		private void OutputTime(TimeSpan time, string displayName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string action;
			switch (this.buildAction)
			{
				case vsBuildAction.vsBuildActionBuild:
					action = "Build";
					break;

				case vsBuildAction.vsBuildActionClean:
					action = "Clean";
					break;

				case vsBuildAction.vsBuildActionDeploy:
					action = "Deploy";
					break;

				case vsBuildAction.vsBuildActionRebuildAll:
					action = "Rebuild";
					break;

				default:
					action = this.buildAction.ToString();
					break;
			}

			string target = null;
			if (Timing == BuildTiming.Details)
			{
				target = string.IsNullOrEmpty(displayName) ? " Overall" : $" For {displayName}";
			}

			string formattedTime;
			if (time < TimeSpan.FromMinutes(1))
			{
				formattedTime = $"{time.TotalSeconds:f3} s";
			}
			else
			{
				// Truncate time to whole seconds since the build took at least one minute.
				formattedTime = TimeSpan.FromTicks(time.Ticks - (time.Ticks % TimeSpan.TicksPerSecond)).ToString();
			}

			string message = $"------ {action} Time{target}: {formattedTime} ------\r\n";
			this.OutputString(message);
		}

		#endregion

		#region Private Event Handlers

		private void BuildEvents_OnBuildBegin(vsBuildScope scope, vsBuildAction action)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (this.buildBegan == null && Timing != BuildTiming.None)
			{
				this.Clear(DateTime.UtcNow, action);
			}
		}

		private void BuildEvents_OnBuildDone(vsBuildScope scope, vsBuildAction action)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (this.IsTimedBuild)
			{
				// If 0 or 1 projects built in Details mode, then we don't need to give an overall build time.
				if (Timing == BuildTiming.Overall || this.projectConfigBegan.Count >= 2)
				{
					TimeSpan overallTime = DateTime.UtcNow - this.buildBegan.Value;
					this.OutputTime(overallTime, null);
				}

				this.Clear(null, default);
			}
		}

		private void BuildEvents_OnBuildProjConfigBegin(string project, string projectConfig, string platform, string solutionConfig)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (this.IsTimedBuild)
			{
				string displayName = GetProjConfigDisplayName(project, projectConfig, platform);
				this.projectConfigBegan[displayName] = DateTime.UtcNow;
			}
		}

		private void BuildEvents_OnBuildProjConfigDone(string project, string projectConfig, string platform, string solutionConfig, bool success)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (this.IsTimedBuild && Timing == BuildTiming.Details)
			{
				DateTime done = DateTime.UtcNow;
				string displayName = GetProjConfigDisplayName(project, projectConfig, platform);
				if (this.projectConfigBegan.TryGetValue(displayName, out DateTime began))
				{
					TimeSpan time = done - began;
					this.OutputTime(time, displayName);
				}
			}
		}

		#endregion
	}
}

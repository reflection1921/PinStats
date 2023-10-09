﻿using Microsoft.UI.Xaml.Input;
using PinStats.Helpers;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Threading.Tasks;
using WinUIEx;
using Timer = System.Timers.Timer;

namespace PinStats.Resources;

public partial class TaskbarUsageResources
{
	private readonly static PerformanceCounter CpuPerformanceCounter;
	private readonly static PrivateFontCollection PrivateFontCollection = new();

	private readonly Timer _timer;
	private readonly Image _iconImage;

	static TaskbarUsageResources()
	{
		CpuPerformanceCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
		PrivateFontCollection.AddFontFile("Fonts/Pretendard-ExtraLight.ttf");
	}

	public TaskbarUsageResources()
	{
		InitializeComponent();
		UpdateSetupStartupProgramMenuFLyoutItemTextProperty();

		// TODO: add a setting to change the interval of the timer.
		_timer = new() { Interval = 250 };
		_timer.Elapsed += OnCpuUsageTimerTick;
		_timer.Start();

		_iconImage = Image.FromFile("Assets/cpu.png").GetThumbnailImage(64, 64, null, IntPtr.Zero);
		TaskbarIconCpuUsage.ForceCreate();
	}

	private void UpdateSetupStartupProgramMenuFLyoutItemTextProperty()
	{
		var isStartupProgram = StartupHelper.IsStartupProgram;
		if (isStartupProgram) MenuFlyoutItemSetupStartupProgram.Text = "Remove from Startup";
		else MenuFlyoutItemSetupStartupProgram.Text = "Add to Startup";
	}

	private bool _updatingImage = false;
	private void OnCpuUsageTimerTick(object sender, object e)
	{
		var cpuUsage = CpuPerformanceCounter.NextValue();
		cpuUsage = Math.Min(cpuUsage, 100);
		ReportWindow.CpuUsageViewModel.AddUsageInformation((int)cpuUsage);

		var cpuUsageText = cpuUsage.ToString("N0");
		if(cpuUsage >= 100) cpuUsageText = "M"; // CPU usage got occasionally 100% or more. So, I decided to use "M" instead of "100".

		if (_updatingImage) return;
		_updatingImage = true;
		DispatcherQueue.TryEnqueue(async () =>
		{
			var image = _iconImage;
			using var bitmap = new Bitmap(image);
			using var graphics = Graphics.FromImage(bitmap);

			await Task.Run(() =>
			{
				var font = new Font(PrivateFontCollection.Families[0], 12);
				var stringFormat = new StringFormat
				{
					Alignment = StringAlignment.Center,
					LineAlignment = StringAlignment.Center
				};
				var rect = new RectangleF(0, 2, image.Width, image.Height);
				graphics.DrawString(cpuUsageText, font, Brushes.Black, rect, stringFormat);
			});

			TaskbarIconCpuUsage.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
			TaskbarIconCpuUsage.ToolTipText = $"CPU Usage: {cpuUsage:N0}%";
			_updatingImage = false;
		});
	}

	private void OnCpuTaskbarIconLeftClicked(XamlUICommand sender, ExecuteRequestedEventArgs args)
	{
		var reportWindow = new ReportWindow();
		var scale = (double)reportWindow.GetDpiForWindow() / 96; // 96 is the default DPI of Windows.
		var offsetX = (reportWindow.Width / 2) * scale;
		var positionY = TaskBarHelper.GetTaskBarTop() - (reportWindow.Height * scale);

		var cursorPosition = CursorHelper.GetCursorPosition();
		reportWindow.Move((int)(cursorPosition.X - offsetX), (int)(positionY));
		reportWindow.Activate();
		reportWindow.BringToFront();
	}

	private void OnCloseProgramMenuFlyoutItemClicked(XamlUICommand sender, ExecuteRequestedEventArgs args) => Environment.Exit(0);

	private void OnSetupStartupProgramMenuFlyoutItemClicked(XamlUICommand sender, ExecuteRequestedEventArgs args)
	{
		StartupHelper.SetupStartupProgram();
		UpdateSetupStartupProgramMenuFLyoutItemTextProperty();
	}
}
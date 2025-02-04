using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PinStats.ViewModels;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.Security.DataProtection;
using WinUIEx;

namespace PinStats;

public sealed partial class ReportWindow
{
	private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;

	public enum DWM_WINDOW_CORNER_PREFERENCE
	{
		DWMWCP_DEFAULT = 0,
		DWMWCP_DONOTROUND = 1,
		DWMWCP_ROUND = 2,
		DWMWCP_ROUNDSMALL = 3
	}

	[LibraryImport("dwmapi.dll")]
	private static partial int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref uint pvAttribute, uint cbAttribute);

	public readonly static UsageViewModel CpuUsageViewModel = new();
	public readonly static UsageViewModel GpuUsageViewModel = new();
	private DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
	public ReportWindow()
	{
		InitializeComponent();

		SystemBackdrop = new MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
		AppWindow.IsShownInSwitchers = false;
		(AppWindow.Presenter as OverlappedPresenter).SetBorderAndTitleBar(true, false);

		// AppWindow that manually set the border and title bar is not rounded.
		IntPtr hwnd = this.GetWindowHandle();
		uint attribute = (uint)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
		DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref attribute, sizeof(uint));

		Activated += OnActivated;

		var lastUsageTarget = Configuration.GetValue<string>("LastUsageTarget") ?? "CPU";

		UsageViewModel usageViewModel = null;
		if (lastUsageTarget == "CPU")
		{
			usageViewModel = CpuUsageViewModel;
			RadioButtonCpu.IsChecked = true;
		}
		else if (lastUsageTarget == "GPU")
		{
			usageViewModel = GpuUsageViewModel;
			RadioButtonGpu.IsChecked = true;
		}
		usageViewModel?.RefreshSync(); // Renew the "sync" of the UsageViewModel to prevent the chart from not being properly displayed.
		CartesianChartUsage.DataContext = usageViewModel;

		RefreshHardwareInformation();
		_refreshTimer.Tick += OnRefreshTimerTick;
		_refreshTimer.Start();

		var gpuNames = HardwareMonitor.GetGpuHardwareNames();
		if (gpuNames.Count > 1)
		{
			foreach (var name in gpuNames)
				ComboBoxGpuList.Items.Add(name);
			ComboBoxGpuList.SelectedIndex = Configuration.GetValue<int?>("GpuIndex") ?? 0;
		}
		else ButtonSelectGpu.Visibility = Visibility.Collapsed;

		var hasBattery = HardwareMonitor.HasBattery();
		if (!hasBattery)
		{
			GridBattery.Visibility = Visibility.Collapsed;
			GridBatteryHealth.Visibility = Visibility.Collapsed;
		}
		else
		{
			var batteryHealthPercent = HardwareMonitor.GetAverageBatteryHealthPercent();
			var hasBatteryHealth = batteryHealthPercent != null;
			if (!hasBatteryHealth) GridBatteryHealth.Visibility = Visibility.Collapsed;
		}
	}

	private void RefreshHardwareInformation()
	{
		TextBlockCpuName.Text = HardwareMonitor.GetCpuName();
		TextBlockGpuName.Text = HardwareMonitor.GetCurrentGpuName();

		Task.Run(() =>
		{
			HardwareMonitor.UpdateCpuHardwares();
			HardwareMonitor.UpdateMemoryHardwares();
			HardwareMonitor.UpdateNetworkHardwares();
			HardwareMonitor.UpdateBatteryHardwares();
			HardwareMonitor.UpdateCurrentGpuHardware();

			var cpuUage = HardwareMonitor.GetAverageCpuUsage();
			var cpuTemperature = HardwareMonitor.GetAverageCpuTemperature();

			var cpuInformationText = $"{cpuUage:N0}%";
			var cpuTempertureText = cpuTemperature != null ? (" / " + cpuTemperature.Value.ToString("N0") + "�C") : "";
			var cpuPowerText = HardwareMonitor.GetTotalCpuPackagePower() != 0 ? (" / " + HardwareMonitor.GetTotalCpuPackagePower().ToString("N0") + " W") : "";
			cpuInformationText += cpuTempertureText + cpuPowerText;


			var gpuUage = HardwareMonitor.GetCurrentGpuUsage();
			var gpuTemperature = HardwareMonitor.GetCurrentGpuTemperature();
			var gpuPower = HardwareMonitor.GetCurrentGpuPower();

			var gpuInformationText = $"{gpuUage:N0}%";
			var gpuTempertureText = gpuTemperature != null ? (" / " + gpuTemperature.Value.ToString("N0") + "�C") : "";
			var gpuPowerText = gpuPower != 0 ? (" / " + gpuPower.ToString("N0") + " W") : "";
			gpuInformationText += gpuTempertureText + gpuPowerText;


			string batteryInformationText = null;
			if (HardwareMonitor.HasBattery())
			{
				var batteryPercentage = HardwareMonitor.GetTotalBatteryPercent();
				var batteryChargeRate = HardwareMonitor.GetTotalBatteryChargeRate();

				batteryInformationText = $"{batteryPercentage:N0}%";

				string batteryChargeRatePrefix = string.Empty;
				if (batteryChargeRate.Value > 0) batteryChargeRatePrefix = "+";
				var batteryChargeRateText = batteryChargeRate != null ? (" / " + batteryChargeRatePrefix + batteryChargeRate.Value.ToString("N1") + " W") : "";
				batteryInformationText += batteryChargeRateText;

			}

			string batteryHealthInformationText = null;
			var batteryHealthPercent = HardwareMonitor.GetAverageBatteryHealthPercent();
			var hasBatteryHealth = batteryHealthPercent != null;
			if (hasBatteryHealth) batteryHealthInformationText = $"{batteryHealthPercent:N0}%";


			var memoryInformationText = HardwareMonitor.GetMemoryInformationText();


			var networkUploadSpeed = (float)HardwareMonitor.GetNetworkTotalUploadSpeedInBytes() / 1024;
			var networkDownloadSpeed = (float)HardwareMonitor.GetNetworkTotalDownloadSpeedInBytes() / 1024;
			var networkInformationText = $"U: {networkUploadSpeed:N0} KB/s D: {networkDownloadSpeed:N0} KB/s";

			DispatcherQueue.TryEnqueue(() =>
			{
				TextBlockCpuInformation.Text = cpuInformationText;
				TextBlockGpuInformation.Text = gpuInformationText;
				TextBlockMemoryInformation.Text = memoryInformationText;
				TextBlockNetworkInformation.Text = networkInformationText;
				if (batteryInformationText != null) TextBlockBatteryInformation.Text = batteryInformationText;
				if (batteryHealthInformationText != null) TextBlockBatteryHealthInformation.Text = batteryHealthInformationText;
			});
		});

	}

	private void OnActivated(object sender, WindowActivatedEventArgs args)
	{
		// Close the window when the window lost its focus.
		if (args.WindowActivationState == WindowActivationState.Deactivated) Close();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		_refreshTimer.Stop();
		_refreshTimer = null;
	}

	private void OnRefreshTimerTick(object sender, object e) => RefreshHardwareInformation();

	private void OnRadioButtonClicked(object sender, RoutedEventArgs e)
	{
		var radioButton = sender as RadioButton;
		RadioButtonCpu.IsChecked = false;
		RadioButtonGpu.IsChecked = false;
		radioButton.IsChecked = true;
		UsageViewModel usageViewModel = null;
		if (radioButton == RadioButtonCpu)
		{
			usageViewModel = CpuUsageViewModel;
			Configuration.SetValue("LastUsageTarget", "CPU");
		}
		else if (radioButton == RadioButtonGpu)
		{
			usageViewModel = GpuUsageViewModel;
			Configuration.SetValue("LastUsageTarget", "GPU");
		}
		usageViewModel?.RefreshSync(); // Renew the "sync" of the UsageViewModel to prevent the chart from not being properly displayed.
		CartesianChartUsage.DataContext = usageViewModel;
	}

	private void OnGpuListComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		var comboBox = sender as ComboBox;
		var index = comboBox.SelectedIndex;
		Configuration.SetValue("GpuIndex", index);
		RefreshHardwareInformation();
	}
}

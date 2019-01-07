using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PhotoAlbumWorkflow
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		Settings settings = null;

		public SettingsWindow()
		{
			InitializeComponent();
			if (null == settings)
			{
				settings = new Settings();
			}
			System.Collections.Specialized.StringCollection locations = null;
			if (settings.Locations == null || settings.Locations.Count == 0)
			{
				locations = Settings.Default.Locations;
			}
			else
			{
				locations = settings.Locations;
			}

			var sb = new StringBuilder();
			foreach (var locationString in locations)
			{
				sb.AppendLine(locationString);
			}
			LocationsTextBox.Text = sb.ToString();
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			bool unFoundLocation = false;
			var tempLocations = LocationsTextBox.Text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			foreach (var tLocation in tempLocations)
			{
				var resolvedLocation = Environment.ExpandEnvironmentVariables(tLocation);
				if (!Directory.Exists(resolvedLocation))
				{
					unFoundLocation = true;
					MessageBox.Show("Unable to find " + tLocation, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					break;
				}
			}

			if (unFoundLocation)
			{
				return;
			}

			settings.Locations.Clear();
			settings.Locations.AddRange(tempLocations);
			settings.Save();
			Close();
		}
	}
}

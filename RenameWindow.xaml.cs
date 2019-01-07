using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PhotoAlbumWorkflow
{
	/// <summary>
	/// Interaction logic for RenameWindow.xaml
	/// </summary>
	public partial class RenameWindow : Window
	{
		private Action<Object> figureOutStartDelegate;
		private String album;
		private Dictionary<String, CancellationTokenSource> cancelIndex;
		private ICollection<DataHolder> items;

		public RenameWindow()
		{
			InitializeComponent();
			figureOutStartDelegate = new Action<Object>(delegate(object state) {try{ figureOutStartNumber(state); } catch(TaskCanceledException) {}});
			cancelIndex = new Dictionary<string,CancellationTokenSource>();
			prefixTextBox.TextChanged += new TextChangedEventHandler(prefixTextBox_TextChanged);
		}

		void prefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			startingTextBox.Text = "01";
			LaunchTaskIfNeeded();
		}

		/// <summary>
		/// Cleans up the outstanding tasks of finding the best starting number and launches a new one if needed
		/// </summary>
		private void LaunchTaskIfNeeded()
		{
			string prefix = prefixTextBox.Text;
			foreach (var itemPair in cancelIndex)
			{
				if (prefix != itemPair.Key)
				{
					itemPair.Value.Cancel();
				}
			}

			bool needToCreateNewRequest = true;
			if (cancelIndex.ContainsKey(prefix))
			{
				if (cancelIndex[prefix].IsCancellationRequested)
				{
					cancelIndex[prefix].Dispose();
				}
				else
				{
					needToCreateNewRequest = false;
				}
			}
			
			if (needToCreateNewRequest)
			{
				cancelIndex[prefix] = new CancellationTokenSource();
				Task.Factory.StartNew(figureOutStartDelegate, prefix, cancelIndex[prefix].Token);
			}
		}

		/// <summary>
		/// The Album name
		/// </summary>
		public String Album
		{
			get
			{
				return album;
			}
			set
			{
				album = value;
				prefixTextBox.Text = album + ' ';
			}
		}

		/// <summary>
		/// The items to look at when figuring places to look for existing files with similar names
		/// </summary>
		public ICollection<DataHolder> Items
		{
			get { return items; }
			set
			{
				items = value;
				LaunchTaskIfNeeded();
			}
		}

		/// <summary>
		/// See if the prefix exists in well known directories and find if there is already a numbering scheme.
		/// If so, start from the end.
		/// </summary>
		private void figureOutStartNumber(Object state)
		{
			String prefix = state.ToString();
			if (String.IsNullOrWhiteSpace(prefix)) return;
			ConcurrentBag<DirectoryInfo> directoryPaths = new ConcurrentBag<DirectoryInfo>();
			CancellationTokenSource cancelSource;
			if (cancelIndex.ContainsKey(prefix))
			{
				cancelSource = cancelIndex[prefix];
			}
			else
			{
				return;
			}

			var settings = new Settings();
			foreach (var loc in settings.Locations)
			{
				var expandedPath = Environment.ExpandEnvironmentVariables(loc);
				var di = new DirectoryInfo(expandedPath);
				SearchDirAndMatchingChildren(prefix, directoryPaths, cancelSource, di);
			}

			String lastAddedDirectoryPath = null;
			var end = DateTimeOffset.UtcNow.AddMilliseconds(500);
			while (DateTimeOffset.UtcNow < end && null == Items) Thread.Yield();
			foreach (var item in Items)
			{
				if (cancelSource.IsCancellationRequested)
				{
					throw new OperationCanceledException();
				}
				String itemDirectoryPath = Path.GetDirectoryName(item.FileName);
				if (itemDirectoryPath != lastAddedDirectoryPath)
				{
					DirectoryInfo itemDirectory = new DirectoryInfo(itemDirectoryPath);
					if (!directoryPaths.Contains(itemDirectory))
					{
						Task.Factory.StartNew(() => { try { FindSuggestedStart(prefix, itemDirectory); } catch (TaskCanceledException) { } },
							cancelSource.Token);
						directoryPaths.Add(itemDirectory);
						lastAddedDirectoryPath = itemDirectoryPath;
					}
				}
			}
		}

		/// <summary>
		/// Search the given directory and a child directory that has the same prefix as the one being looked for.
		/// </summary>
		/// <param name="prefix">The name of a child directory to look in.</param>
		/// <param name="directoryPaths">Current existing paths that have been looked in.</param>
		/// <param name="cancelSource">The cancelation source.</param>
		/// <param name="parentDir">The directory for sure to look in.</param>
		private void SearchDirAndMatchingChildren(String prefix, ConcurrentBag<DirectoryInfo> directoryPaths, CancellationTokenSource cancelSource, DirectoryInfo parentDir)
		{
			String prefixTrimmed = prefix.Trim();
			foreach (var directInfo in parentDir.EnumerateDirectories())
			{
				if (directInfo.Name.Equals(prefixTrimmed, StringComparison.OrdinalIgnoreCase) || directInfo.Name.Equals(prefix, StringComparison.OrdinalIgnoreCase))
				{
					directoryPaths.Add(directInfo);
					var di = directInfo;
					Task.Factory.StartNew(() => { try { FindSuggestedStart(prefix, di); } catch (TaskCanceledException) { } },
						cancelSource.Token);
				}
			}

			directoryPaths.Add(parentDir);
			Task.Factory.StartNew(() => { try { FindSuggestedStart(prefix, parentDir); } catch (TaskCanceledException) { } },
				cancelSource.Token);
		}

		/// <summary>
		/// Given the prefix and the directory, try to find the Max number that newly added pictures should begin with.
		/// </summary>
		/// <param name="prefix"></param>
		/// <param name="directory"></param>
		public void FindSuggestedStart(String prefix, DirectoryInfo directory)
		{

			CancellationTokenSource cancelSource;
			if (cancelIndex.ContainsKey(prefix))
			{
				cancelSource = cancelIndex[prefix];
			}
			else
			{
				return;
			}

			int max = 0;
			try
			{
				foreach (var existingFile in directory.EnumerateFiles(prefix + "*"))
				{
					if (cancelSource.IsCancellationRequested)
					{
						throw new OperationCanceledException();
					}
					String justFile = System.IO.Path.GetFileNameWithoutExtension(existingFile.Name);
					String possibleNumber = justFile.Substring(prefix.Length);
					int value;
					if (Int32.TryParse(possibleNumber, out value))
					{
						if (value > max)
						{
							max = value;
						}
					}
				}
			}
			catch (System.IO.IOException) { }
			int suggestedStart = max + 1;
			int count = suggestedStart + Items.Count;
			int recommend = Convert.ToInt32(Math.Max(2, Math.Floor(Math.Log10(count))));

			if (0 < max && !cancelSource.IsCancellationRequested)
			{
				Dispatcher.BeginInvoke((Action<int, int, string>)delegate(int setTo, int digits, string prefixForThisTask)
				{
					if (prefixTextBox.Text == prefixForThisTask)
					{
						int currentNumber = 0;
						if (int.TryParse(startingTextBox.Text, out currentNumber))
						{
							if (setTo > currentNumber)
							{
								startingTextBox.Text = String.Format("{0:D" + digits + "}", setTo);
							}
						}
						else
						{
							startingTextBox.Text = String.Format("{0:D" + digits + "}", setTo);
						}
					}
				}, suggestedStart, recommend, prefix);
			}
		}

		private void cancelButton_Click(object sender, RoutedEventArgs e)
		{
			foreach(var source in cancelIndex.Values)
			{
				source.Cancel();
				source.Dispose();
			}
			cancelIndex.Clear();
			Close();
		}

		/// <summary>
		/// Renames the pictures and if sucessful closes the window.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void renameButton_Click(object sender, RoutedEventArgs e)
		{
			int startNumber = Int32.Parse(startingTextBox.Text);
			int digits = startingTextBox.Text.Trim().Length;
			String prefix = prefixTextBox.Text;
			String format = prefix + "{0:D" + digits + '}';
			// copying the collection of items to an array, so that it's easy to index into them. Doing so gives each item a unique number.
			DataHolder[] indexedItems = new DataHolder[Items.Count];
			Items.CopyTo(indexedItems, 0);
			Parallel.For(0, indexedItems.Length, i =>
			{
				var item = indexedItems[i];
				String orgItemName = item.FileName;
				String directoryName = System.IO.Path.GetDirectoryName(orgItemName);
				String extention = System.IO.Path.GetExtension(orgItemName);
				int currentNumber = i + startNumber;
				String newPath = System.IO.Path.Combine(directoryName, String.Format(format, currentNumber) + extention);
				DateTimeOffset end = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(5));
				bool notMoved = true;
				// Had to add this loop because WPF leaves a handle open to the file for the thumbnail, so there needs to be some retries
				do
				{
					if (DateTimeOffset.UtcNow < end)
					{
						try
						{
							System.IO.File.Move(sourceFileName: orgItemName, destFileName: newPath);
							notMoved = false;
						}
						catch (System.IO.IOException) { System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(200)); }
					}
					else
					{
						System.IO.File.Move(sourceFileName: orgItemName, destFileName: newPath);
						notMoved = false;
					}
				} while (notMoved);

				// Update the value that the ListBox is viewing on the UI thread so that the change will be displayed when the window is closed
				Dispatcher.BeginInvoke(
					(Action<DataHolder, String>)delegate(DataHolder _item, String _newPath)
					{ _item.FileName = _newPath; },
					item, newPath);
			});

			Close();
		}
	}
}

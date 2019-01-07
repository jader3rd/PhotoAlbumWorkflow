using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace PhotoAlbumWorkflow
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	[Guid("95B6E8D3-A724-4496-87C4-209B91F7C3B7"), ComVisible(true), CLSCompliant(false)]
	public partial class MainWindow : Window, IOutstandingTracker
	{
		private const String PaddingIfdQuery = @"/app1/ifd/PaddingSchema:Padding";
		private const String PaddingExifQuery = @"/app1/ifd/exif/PaddingSchema:Padding";
		private const String PaddingXmpQuery = @"/xmp/PaddingSchema:Padding";
		public static readonly TimeSpan MaxCreationTakenDiff = TimeSpan.FromHours(1);
		public static readonly DateTime MinAcceptableDateTaken = DateTime.Parse("1/1/2001");
		private String albumName;
		ListBoxData viewModel;
		private UIElement[] toDisable;
		private int outstandingWork = 0;

		/// <summary>
		/// Sets a file attribute
		/// </summary>
		/// <returns>false if there is an error</returns>
		/// <remarks>Using so that I can pass in my already grabbed filestream.</remarks>
		[DllImport("KERNEL32.dll", SetLastError = true)]
		internal unsafe static extern bool SetFileTime(SafeFileHandle hFile, FILE_TIME* creationTime,
					   FILE_TIME* lastAccessTime, FILE_TIME* lastWriteTime);

		[StructLayout(LayoutKind.Sequential)]
		internal struct FILE_TIME
		{
			public FILE_TIME(long fileTime)
			{
				ftTimeLow = (uint)fileTime;
				ftTimeHigh = (uint)(fileTime >> 32);
			}

			public long ToTicks()
			{
				return ((long)ftTimeHigh << 32) + ftTimeLow;
			}

			internal uint ftTimeLow;
			internal uint ftTimeHigh;
		}

		public String AlbumName
		{
			get { return albumName; }
			private set
			{
				albumName = value;
				Title = "Photo Album Workflow - " + albumName;
			}
		}

		public MainWindow()
		{
			InitializeComponent();
			albumName = String.Empty;
			viewModel = new ListBoxData();
			toDisable = new UIElement[3] { SetTitleButton, RenameButton, SetTimeButton };
		}

		void contentListBox_DragLeave(object sender, DragEventArgs e)
		{
		}

		void contentListBox_DragEnter(object sender, DragEventArgs e)
		{
			if ((e.AllowedEffects & DragDropEffects.Link) == DragDropEffects.Link)
			{
				e.Effects = DragDropEffects.Link;
			}
			else
			{
				e.Effects = DragDropEffects.None;
			}
			e.Handled = true;
		}

		void contentListBox_Drop(object sender, DragEventArgs e)
		{
			String[] fileNames = (String[])e.Data.GetData(DataFormats.FileDrop, true);
			addGoodFiles(fileNames);
			e.Handled = true;
		}

		/// <summary>
		/// Adds the files to the collection which are pictures which can hold metadata
		/// </summary>
		/// <param name="fileNames"></param>
		private void addGoodFiles(String[] fileNames)
		{
			var uiScheduler = TaskScheduler.Current;
			foreach (String fileName in fileNames)
			{
				if (hasPossibleExtention(fileName))
				{
					if (String.IsNullOrEmpty(AlbumName))
					{
						DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(fileName));
						AlbumName = di.Name;
					}
					var pair = new DataHolder(fileName, this);
					viewModel.Collection.Add(pair);

					// Let all of the items get added to the collection, and then pump the UI thread with "background" tasks for getting metadata
					Task.Factory.StartNew(() => setDatesAndHighlights(pair),
						CancellationToken.None,
						TaskCreationOptions.None,
						uiScheduler
						);
				}
			}
		}

		/// <summary>
		/// Extract different metadata from the file and store it in the program. Set the highlight as needed
		/// </summary>
		/// <param name="holder"></param>
		/// <remarks>It would make sense to run this asynchronously, but since the BitmapCache is involved it works best on the UI thread.</remarks>
		private void setDatesAndHighlights(DataHolder holder)
		{
			holder.DateCreated = File.GetCreationTime(holder.FileName);
			holder.DateModified = File.GetLastWriteTime(holder.FileName);
			DateTime dateTake = DateTime.MinValue;
			using (FileStream fs = new FileStream(holder.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				BitmapDecoder decoder = BitmapDecoder.Create(fs,
					BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.IgnoreColorProfile,
					BitmapCacheOption.OnDemand);
				var data = (BitmapMetadata)decoder.Frames[0].Metadata;

				String dateTakenDebugString = data.DateTaken;
				if (DateTime.TryParse(dateTakenDebugString, out dateTake))
				{
					holder.DateTaken = dateTake;
				}
			}
			if (holder.DateTaken > MinAcceptableDateTaken)
			{
				holder.LowestDate = holder.DateTaken;
			}
			else
			{
				holder.LowestDate = holder.DateCreated;
			}
			if (holder.DateCreated < holder.LowestDate)
			{
				holder.LowestDate = holder.DateCreated;
			}
			if (holder.DateModified < holder.LowestDate)
			{
				holder.LowestDate = holder.DateModified;
			}

			if (holder.DateTaken > MinAcceptableDateTaken)
			{
				if (holder.DateTaken > holder.LowestDate.Add(MaxCreationTakenDiff))
				{
					holder.DatesOutOfBalance = true;
					holder.Highlight = Brushes.Yellow;
				}
			}

			if ((!holder.DatesOutOfBalance) && holder.DateCreated > holder.LowestDate.Add(MaxCreationTakenDiff))
			{
				holder.DatesOutOfBalance = true;
				holder.Highlight = Brushes.Yellow;
			}
		}

		private bool hasPossibleExtention(string fileName)
		{
			if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".wdp", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Click event for the set title button
		/// </summary>
		/// <remarks>Turn into a command later</remarks>
		private void SetTitle_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				foreach (var item in viewModel.Collection)
				{
					setTitle(item.FileName);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Sets the Title metadata field to the file name of the picture.
		/// </summary>
		/// <param name="fileName"></param>
		private void setTitle(string fileName)
		{
			using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
			{
				BitmapDecoder decoder = BitmapDecoder.Create(fs,
					BitmapCreateOptions.IgnoreImageCache,
					BitmapCacheOption.OnDemand);

				BitmapEncoder encoder = CreateEncoder(decoder);
				if (null == encoder) return;

				InPlaceBitmapMetadataWriter bitmeta = decoder.Frames[0].CreateInPlaceBitmapMetadataWriter();

				String newTitle = Path.GetFileNameWithoutExtension(fileName);
				bitmeta.Title = newTitle;
				if (!bitmeta.TrySave())
				{
					// Need to add more padding
					// This requires writing out a whole new file
					ExpandFileAndSave(fs, encoder, newMeta => newMeta.Title = newTitle);
					
				}
			}
		}

		private void ExpandFileAndSave(FileStream originalFile, BitmapEncoder encoder, Action<BitmapMetadata> manipulation)
		{
			var tempPath = Path.GetTempFileName();
			using (FileStream fsTemp = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, (int)originalFile.Length, FileOptions.DeleteOnClose))
			{
				originalFile.Seek(0, SeekOrigin.Begin);

				byte[] buffer = new byte[(int)originalFile.Length];
				originalFile.Read(buffer, 0, buffer.Length);
				fsTemp.Write(buffer, 0, buffer.Length);

				originalFile.Seek(0, SeekOrigin.Begin);
				fsTemp.Seek(0, SeekOrigin.Begin);
				buffer = null;

				var decoder = BitmapDecoder.Create(fsTemp,
					BitmapCreateOptions.IgnoreImageCache,
					BitmapCacheOption.OnDemand);
				var bitmeta = decoder.Frames[0].CreateInPlaceBitmapMetadataWriter();

				BitmapMetadata newMeta = CreateMetadata(decoder, bitmeta);
				manipulation.Invoke(newMeta);

				encoder.Frames.Add(BitmapFrame.Create(
					decoder.Frames[0],
					decoder.Frames[0].Thumbnail,
					newMeta,
					decoder.Frames[0].ColorContexts));

				for (int frameNum = 1; frameNum < decoder.Frames.Count; frameNum++)
				{
					encoder.Frames.Add(decoder.Frames[frameNum]);
				}

				encoder.Save(originalFile);
			}
		}

		/// <summary>
		/// Creates the proper encoder based off of the decoder.
		/// </summary>
		/// <param name="decoder"></param>
		/// <returns>Null if no encoder can be found.</returns>
		private static BitmapEncoder CreateEncoder(BitmapDecoder decoder)
		{
			BitmapEncoder encoder;
			if (decoder.CodecInfo.FileExtensions.Contains("jpg"))
			{
				encoder = new JpegBitmapEncoder();
			}
			else if (decoder.CodecInfo.FileExtensions.Contains("png"))
			{
				encoder = new PngBitmapEncoder();
			}
			else if (decoder.CodecInfo.FileExtensions.Contains("tif"))
			{
				encoder = new TiffBitmapEncoder();
			}
			else if (decoder.CodecInfo.FileExtensions.Contains("gif"))
			{
				encoder = new GifBitmapEncoder();
			}
			else if (decoder.CodecInfo.FileExtensions.Contains("wdp"))
			{
				encoder = new WmpBitmapEncoder();
			}
			else
			{
				encoder = null;
			}
			return encoder;
		}

		/// <summary>
		/// Create a new Metadata object and give it appropriate padding.
		/// </summary>
		/// <param name="decoder">The decoder to generate the clone from.</param>
		/// <param name="bitmeta">The original metadata which didn't have enough padding.</param>
		/// <returns></returns>
		private static BitmapMetadata CreateMetadata(BitmapDecoder decoder, InPlaceBitmapMetadataWriter bitmeta)
		{
			BitmapMetadata newMeta = decoder.Frames[0].Metadata.Clone() as BitmapMetadata;
			uint paddingIncrement = 1024;
			uint paddingAmount = paddingIncrement;

			Object queryResult = bitmeta.GetQuery(PaddingIfdQuery);
			if (null != queryResult)
			{
				var existingPadding = Convert.ToUInt32(queryResult);
				paddingAmount = existingPadding + paddingIncrement;
			}
			newMeta.SetQuery(PaddingIfdQuery, paddingAmount);

			queryResult = bitmeta.GetQuery(PaddingExifQuery);
			if (null != queryResult)
			{
				var existingPadding = Convert.ToUInt32(queryResult);
				paddingAmount = existingPadding + paddingIncrement;
			}
			else
			{
				paddingAmount = paddingIncrement;
			}
			newMeta.SetQuery(PaddingExifQuery, paddingAmount);

			queryResult = bitmeta.GetQuery(PaddingXmpQuery);
			if (null != queryResult)
			{
				var existingPadding = Convert.ToUInt32(queryResult);
				paddingAmount = existingPadding + paddingIncrement;
			}
			else
			{
				paddingAmount = paddingIncrement;
			}
			newMeta.SetQuery(PaddingXmpQuery, paddingAmount);
			return newMeta;
		}

		private void Rename_Click(object sender, RoutedEventArgs e)
		{
			RenameWindow rw = new RenameWindow();
			rw.Album = AlbumName;
			rw.Items = viewModel.Collection;
			rw.ShowDialog();
		}

		private void Browse_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.CheckPathExists = true;
			ofd.Filter = "Image Files|*.jpg;*.jpeg;*.gif;*.tiff;*.bmp;*.png;*.wdp";
			ofd.Multiselect = true;
			bool? result = ofd.ShowDialog(this);
			if (true == result)
			{
				addGoodFiles(ofd.FileNames);
			}
		}

		private void Clear_Click(object sender, RoutedEventArgs e)
		{
			viewModel.Collection.Clear();
			AlbumName = null;
		}

		private void Settings_Click(object sender, RoutedEventArgs e)
		{
			var sw = new SettingsWindow();
			sw.ShowDialog();
		}

		/// <summary>
		/// Bind the data context for the list box when the window is loaded. Not done in constructor
		/// so that the designer can set it's own data context.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			contentListBox.DataContext = viewModel;
		}

		/// <summary>
		/// Event handler for when the image is loaded. This is used so that the thumbnails can be loaded on demand
		/// </summary>
		private void Image_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.Image)
			{
				var img = (System.Windows.Controls.Image)sender;
				if (img.DataContext is DataHolder)
				{
					((DataHolder)img.DataContext).StartReadingThumbnailFromDisk();
				}
			}
		}

		/// <summary>
		/// Event Handler to set the DateCreated and DateTaken to the lowest possible time stamp.
		/// </summary>
		private void SetTime_Click(object sender, RoutedEventArgs e)
		{
			foreach (var holder in viewModel.Collection)
			{
				if (holder.DatesOutOfBalance)
				{
					using (FileStream fs = new FileStream(holder.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
					{
						var diff = holder.DateCreated.Subtract(holder.LowestDate).Duration();
						if (diff > MaxCreationTakenDiff)
						{
							unsafe
							{
								FILE_TIME ft = new FILE_TIME(holder.LowestDate.ToFileTimeUtc());
								bool r = SetFileTime(fs.SafeFileHandle, &ft, null, null);
								if (!r)
								{
									int errorCode = Marshal.GetLastWin32Error();
									Marshal.ThrowExceptionForHR(errorCode);
								}
							}
							holder.DateCreated = holder.LowestDate;
						}

						diff = holder.DateTaken.Subtract(holder.LowestDate).Duration();
						if (diff > MaxCreationTakenDiff && holder.DateTaken > MinAcceptableDateTaken)
						{
							BitmapDecoder decoder = BitmapDecoder.Create(fs,
								BitmapCreateOptions.None,
								BitmapCacheOption.OnDemand);

							BitmapEncoder encoder = CreateEncoder(decoder);
							if (null == encoder) return;

							InPlaceBitmapMetadataWriter bitmeta = decoder.Frames[0].CreateInPlaceBitmapMetadataWriter();

							String dateString = holder.LowestDate.ToString();
							bitmeta.DateTaken = dateString;
							if (!bitmeta.TrySave())
							{
								ExpandFileAndSave(fs, encoder, metaData => metaData.DateTaken = dateString);
							}
							holder.DateTaken = holder.LowestDate;
						}
					}

					holder.DatesOutOfBalance = false;
					holder.Highlight = null;
				}
			}
		}

		#region IOutstandingTracker Members

		public void IncrementOutstandingWork()
		{
			if (1 == Interlocked.Increment(ref outstandingWork))
			{
				foreach (var element in toDisable)
				{
					element.IsEnabled = false;
				}
			}
		}

		public void DecrementOutstandingWork()
		{
			if (0 == Interlocked.Decrement(ref outstandingWork))
			{
				foreach (var element in toDisable)
				{
					element.IsEnabled = true;
				}
			}
		}

		#endregion
	}

	/// <summary>
	/// Class which holds the data which will appear in the ListBox. Contains the full path to the pictures
	/// </summary>
	public class DataHolder : INotifyPropertyChanged
	{
		private string _fileName;
		private ImageSource _asyncImage;
		private IOutstandingTracker workTracker;
		private Brush _highlight;
		private DateTime _dateTaken;
		private DateTime _dateCreated;
		private DateTime _dateModified;

		public DataHolder(String fileName, IOutstandingTracker tracker)
		{
			_fileName = fileName;
			_asyncImage = null;
			workTracker = tracker;
			_dateTaken = DateTime.MaxValue;
			_dateModified = DateTime.MaxValue;
			_dateCreated = DateTime.MaxValue;
			LowestDate = DateTime.MaxValue;
		}

		/// <summary>
		/// The full file path to the pictures
		/// </summary>
		public String FileName
		{
			get { return _fileName; }
			set
			{
				_fileName = value;
				OnPropertyChanged("FileName");
			}
		}

		/// <summary>
		/// The ImageSource that's to be used for the Image object. Starts out as null and then is loaded on demand by calling StartReadingThumbnailFromDisk
		/// </summary>
		public ImageSource AsyncImage
		{
			get { return _asyncImage; }
			set
			{
				_asyncImage = value;
				OnPropertyChanged("AsyncImage");
			}
		}

		public DateTime DateTaken
		{
			get { return _dateTaken; }
			set
			{
				_dateTaken = value;
				OnPropertyChanged("DateTaken");
			}
		}

		public DateTime DateCreated
		{
			get { return _dateCreated; }
			set
			{
				_dateCreated = value;
				OnPropertyChanged("DateCreated");
			}
		}

		public DateTime DateModified
		{
			get { return _dateModified; }
			set
			{
				_dateModified = value;
				OnPropertyChanged("DateModified");
			}
		}

		public DateTime LowestDate { get; set; }

		public Brush Highlight
		{
			get { return _highlight; }
			set
			{
				if (_highlight != value)
				{
					_highlight = value;
					OnPropertyChanged("Highlight");
				}
			}
		}

		/// <summary>
		/// To be set when discovered dates are in need of correction
		/// </summary>
		public bool DatesOutOfBalance { get; set; }

		/// <summary>
		/// If the thumbnail hasn't been loaded, start loading it and assign the result to the AsyncImage property on the calling thread
		/// </summary>
		public void StartReadingThumbnailFromDisk()
		{
			if (null == AsyncImage)
			{
				workTracker.IncrementOutstandingWork();
				var scheduler = TaskScheduler.FromCurrentSynchronizationContext();

				Task.Factory.StartNew(() => ReadThumbnailFromDisk())
					.ContinueWith(t =>
					{
						AsyncImage = t.Result;
						workTracker.DecrementOutstandingWork();
					},
					scheduler);
			}
		}

		private BitmapImage ReadThumbnailFromDisk()
		{
			BitmapImage result = new BitmapImage();
			result.BeginInit();
			result.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
			result.CacheOption = BitmapCacheOption.OnLoad;
			result.UriSource = new Uri(FileName);
			result.DecodePixelHeight = 32;

			result.EndInit();
			result.Freeze();
			return result;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged(String propertyName)
		{
			var handler = PropertyChanged;
			if (null != handler)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}

	/// <summary>
	/// The class that the ListBox binds to by setting it as it's DataContext
	/// </summary>
	public class ListBoxData : INotifyPropertyChanged
	{
		private ObservableCollection<DataHolder> model = new ObservableCollection<DataHolder>();

		public ObservableCollection<DataHolder> Collection
		{
			get { return model; }
			set
			{
				model = value;
				OnPropertyChanged("Collection");
			}
		}

		protected void OnPropertyChanged(String propertyName)
		{
			var handler = PropertyChanged;
			if (null != handler)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}

	/// <summary>
	/// Basically calls Path.GetFileName. Used so the WPF XAML can do the transformation
	/// </summary>
	[ValueConversion(typeof(String), typeof(String))]
	public class PathToFileNameConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			String fullPath = value.ToString();
			String fileName = Path.GetFileName(fullPath);
			return fileName;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return null;
		}
	}
}

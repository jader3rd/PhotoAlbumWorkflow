﻿//      *********    DO NOT MODIFY THIS FILE     *********
//      This file is regenerated by a design tool. Making
//      changes to this file can cause errors.
namespace Expression.Blend.SampleData.SampleImages
{
	using System; 

// To significantly reduce the sample data footprint in your production application, you can set
// the DISABLE_SAMPLE_DATA conditional compilation constant and disable sample data at runtime.
#if DISABLE_SAMPLE_DATA
	internal class SampleImages { }
#else

	public class SampleImages : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
			}
		}

		public SampleImages()
		{
			try
			{
				System.Uri resourceUri = new System.Uri("/PhotoAlbumWorkflow;component/SampleData/SampleImages/SampleImages.xaml", System.UriKind.Relative);
				if (System.Windows.Application.GetResourceStream(resourceUri) != null)
				{
					System.Windows.Application.LoadComponent(this, resourceUri);
				}
			}
			catch (System.Exception)
			{
			}
		}

		private ItemCollection _Collection = new ItemCollection();

		public ItemCollection Collection
		{
			get
			{
				return this._Collection;
			}
		}
	}

	public class Item : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
			}
		}

		private string _FileName = string.Empty;

		public string FileName
		{
			get
			{
				return this._FileName;
			}

			set
			{
				if (this._FileName != value)
				{
					this._FileName = value;
					this.OnPropertyChanged("FileName");
				}
			}
		}

		private System.Windows.Media.ImageSource _Thumbnail = null;

		public System.Windows.Media.ImageSource Thumbnail
		{
			get
			{
				return this._Thumbnail;
			}

			set
			{
				if (this._Thumbnail != value)
				{
					this._Thumbnail = value;
					this.OnPropertyChanged("Thumbnail");
				}
			}
		}
	}

	public class ItemCollection : System.Collections.ObjectModel.ObservableCollection<Item>
	{ 
	}
#endif
}

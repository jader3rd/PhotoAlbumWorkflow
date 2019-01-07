using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhotoAlbumWorkflow
{
	public interface IOutstandingTracker
	{
		void IncrementOutstandingWork();
		void DecrementOutstandingWork();
	}
}

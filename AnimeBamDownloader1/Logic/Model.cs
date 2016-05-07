using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimeBamDownloader1.Logic
{
    
    public class DownloaderTask
    {
        public int downloaderID { get; set; }
        public int episode_id { get; set; }
        public int series_id { get; set; }
        public string quality { get; set; }
        public int download_size { get; set; }
        public int total_size { get; set; }
        public double speed { get; set; }
        public DownloaderStatus status { get; set; }
    }

    public enum EpisodeStatus
    {
        Arrived = 1,
        CommingSoon
    }

    public enum DownloaderStatus
    {
        None=0,
        Working,
        Stopped,
        Completed
    }

    public class Series
    {
        public int series_id { get; set; }
        public Uri url { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string status { get; set; }
        public Uri thumbnail_url { get; set; }
        public List<string> genre { get; set; }

        public Series()
        {
            genre = new List<string>();
        }
    }

    public class Episode
    {
        public int episode_id { get; set; }
        public int series_id { get; set; }
        public Uri url { get; set; }
        public string name { get; set; }
        public string animetitle { get; set; }
        public List<string> types { get; set; }
        public bool isChecked { get; set; }

        public Episode()
        {
            types = new List<string>();
        }
    }
}

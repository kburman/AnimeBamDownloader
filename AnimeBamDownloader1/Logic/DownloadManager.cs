using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimeBamDownloader1.Logic
{
    class DownloaderTask
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
    /// <summary>
    /// It need to update its status on sqlite table.
    /// we can control download only through this class
    /// </summary>
    public class DownloadManager
    {
        public event EventHandler DownloadCompleted;
        private static DownloadManager _instance_ = new DownloadManager();
        private List<BackgroundWorker> workers;
        private int maxParallelWorkers = 3;
        private int currParallelWorkers = 0;

        private DownloadManager()
        {
            workers = new List<BackgroundWorker>();
            // reset all task status to waiting
            SQLiteCommand cmd = new SQLiteCommand("UPDATE download_task SET status=@status WHERE status=@oldstatus");
            cmd.Parameters.AddWithValue("@status", (int)DownloaderStatus.Working);
            cmd.Parameters.AddWithValue("@oldstatus", (int)DownloaderStatus.Stopped);
            DBHelper.getInstance().executeNonQuery(cmd);
        }

        public static DownloadManager getInstance()
        {
            return _instance_;
        }

        public void addToDownloadList(int series_id, int episode_id)
        {

        }


        public void start()
        {
            while (currParallelWorkers < maxParallelWorkers)
            {

            }
        }
        
    }
}

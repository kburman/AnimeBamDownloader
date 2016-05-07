using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimeBamDownloader1.Logic
{
    public class DownloadManager
    {

        List<SeriesDownloader> _series_downloader;
        public DownloadManager()
        {
            _series_downloader = new List<SeriesDownloader>();
        }

        /// <summary>
        /// Re check the sql table for if any downloader has to be stopped
        /// or to be started
        /// </summary>
        public void reloadDownloadStaus()
        {
            // if series is start in sql then start if not already
            // if stop then stop if exitising
            using (var cmd = new SQLiteCommand("SELECT * FROM series_download_list"))
            {
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    while (reader.Read())
                    {
                        var item = _series_downloader.SingleOrDefault(x => x.SeriesDownloadListID == reader.GetInt32(0));

                        // add if it is not there
                        if (item == null)
                        {
                            var downloader = new SeriesDownloader(reader.GetInt32(0));
                            _series_downloader.Add(downloader);
                            item = downloader;
                        }

                        DownloaderStatus status = (DownloaderStatus)Enum.Parse(typeof(DownloaderStatus), reader.GetValue(3).ToString());
                        switch (status)
                        {
                            case DownloaderStatus.Working:
                                item.Start();
                                break;
                            case DownloaderStatus.Stopped:
                                item.Stop();
                                break;
                        }

                    }
                }
            }
        }

    }

    public class SeriesDownloader
    {
        // 1> if series is ongoing then refresh download list
        // 2> get to download episode download

        List<EpisodeDownloader> list;
        private const int maxParalleDownload = 3;
        private int currParallelDownload = 0;

        public int SeriesDownloadListID { get; set; }
        public int SeriesID { get; private set; }
        public string DownloadFolder { get; set; }
        public DownloaderStatus Status { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_SeriesDownloadListID">ID from table series_download_id</param>
        public SeriesDownloader(int _SeriesDownloadListID)
        {
            SeriesDownloadListID = _SeriesDownloadListID;
            list = new List<EpisodeDownloader>();
            using (var cmd = new SQLiteCommand("SELECT * FROM series_download_list WHERE series_download_list.id = @id"))
            {
                cmd.Parameters.AddWithValue("@id", _SeriesDownloadListID);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    if (reader.Read())
                    {
                        SeriesID = reader.GetInt32(1);
                        DownloadFolder = reader.GetString(2);
                        Status = (DownloaderStatus)Enum.Parse(typeof(DownloaderStatus), reader.GetValue(3).ToString());
                        
                    }
                    else
                    {
                        throw new Exception("No row with id " + _SeriesDownloadListID + " exits in series_download_list");
                    }
                }
            }

        }
        /// <summary>
        /// Start Downloading
        /// </summary>
        public void Start()
        {
            if (Status == DownloaderStatus.Working || Status == DownloaderStatus.Completed) return;
            Console.WriteLine(String.Format("Series Downloader : Start , {0}", SeriesID));


        }

        /// <summary>
        /// Stop Downloading
        /// </summary>
        public void Stop()
        {
            if (Status == DownloaderStatus.Stopped) return;
            Console.WriteLine(String.Format("Series Downloader : Stop , {0}", SeriesID));
            while (StartOneEpisodeDownload()) ;
        }


        private int getDownloadTaskID()
        {
            // first look in download_task table for any to start
            using (var cmd = new SQLiteCommand("SELECT download_task.download_task_id FROM episode_list  LEFT JOIN  download_task ON episode_list.series_id = download_task.series_id AND episode_list.episode_id = download_task.episode_id WHERE episode_list.series_id = @sid AND download_task.status = @dstatus AND download_task.download_task_id is not NULL LIMIT 1"))
            {
                cmd.Parameters.AddWithValue("@sid", SeriesID);
                cmd.Parameters.AddWithValue("@dstatus", (int)DownloaderStatus.Stopped);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    if (reader.Read())
                    {
                        return reader.GetInt32(0);
                    }
                }
            }

            // find any one which is not started yet 
            // add it to list
            using (var cmd = new SQLiteCommand("SELECT * FROM episode_list  LEFT JOIN  download_task ON episode_list.series_id = download_task.series_id AND episode_list.episode_id = download_task.episode_id WHERE episode_list.series_id = @sid AND download_task.download_task_id is NULL LIMIT 1"))
            {
                cmd.Parameters.AddWithValue("@sid", SeriesID);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    if (reader.Read())
                    {
                        return addDownloadTask(SeriesID, (int)reader[0]);
                        
                    }
                }
            }

            return -1;
        }

        private int addDownloadTask(int series_id, int episode_id)
        {
            using (var cmd = new SQLiteCommand("INSERT INTO download_task (series_id, episode_id, status) VALUES (@sid, @eid, @status)"))
            {
                cmd.Parameters.AddWithValue("@sid", series_id);
                cmd.Parameters.AddWithValue("@eid", episode_id);
                cmd.Parameters.AddWithValue("@status", (int)DownloaderStatus.Stopped);
                DBHelper.getInstance().executeNonQuery(cmd);
                return DBHelper.getInstance().getLastRowId();
            }
        }


        private bool StartOneEpisodeDownload()
        {
            try
            {
                int downloadTaskId = getDownloadTaskID();
                if (downloadTaskId == -1) return false;
                var a = new EpisodeDownloader(downloadTaskId, DownloadFolder);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SeriesDownloader:StartOneEpisodeDownloader() [Error] " + ex.Message);
                return false;
            }
        }
    }

    public class EpisodeDownloader
    {
        public int DownloadTaskId { get; private set; }
        public string SaveFolder { get; private set; }
        public string FileName { get; private set; }

        public EpisodeDownloader(int _downloadTaskId, string _downloadFolder)
        {
            DownloadTaskId = _downloadTaskId;
            SaveFolder = Path.GetDirectoryName(_downloadFolder);
            FileName = Path.GetFileName(_downloadFolder);
            DBHelper.getInstance().setDownloadTaskStatus(_downloadTaskId, DownloaderStatus.Working);

        }
    }

    public class FileDownloader
    {
        public FileDownloader(Uri od)
        {

        }
    }
}

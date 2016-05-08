using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
                        
                    }
                    else
                    {
                        throw new Exception("No row with id " + _SeriesDownloadListID + " exits in series_download_list");
                    }
                }
            }
            resetAllWorkingToStopped();

        }
        /// <summary>
        /// Start Downloading
        /// </summary>
        public void Start()
        {
            if (Status == DownloaderStatus.Working || Status == DownloaderStatus.Completed) return;
            Console.WriteLine(String.Format("Series Downloader : Start , {0}", SeriesID));
            while (StartOneEpisodeDownload()) ;


        }

        /// <summary>
        /// Stop Downloading
        /// </summary>
        public void Stop()
        {
            if (Status == DownloaderStatus.Stopped) return;
            Console.WriteLine(String.Format("Series Downloader : Stop , {0}", SeriesID));
        }

        private void resetAllWorkingToStopped()
        {
            using (var cmd = new SQLiteCommand("UPDATE download_task SET status=@newstatus WHERE status=@oldstatus AND series_id = @sid"))
            {
                cmd.Parameters.AddWithValue("@oldstatus", (int)DownloaderStatus.Working);
                cmd.Parameters.AddWithValue("@newstatus", (int)DownloaderStatus.Stopped);
                cmd.Parameters.AddWithValue("@sid", SeriesID);
                DBHelper.getInstance().executeNonQuery(cmd);
            }
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
                        return addDownloadTask(SeriesID, (int)reader.GetInt32(0));
                        
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
            if (currParallelDownload >= maxParalleDownload) return false;
            try
            {
                int downloadTaskId = getDownloadTaskID();
                if (downloadTaskId == -1) return false;
                var a = new EpisodeDownloader(downloadTaskId, DownloadFolder);
                a.DownloadCompleted += EpisodeDownloadComplete;
                a.DownloadFailed += EpisodeDownloadFailed;
                a.Start();
                currParallelDownload++;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SeriesDownloader:StartOneEpisodeDownloader() [Error] " + ex.Message);
                return false;
            }
        }

        private void EpisodeDownloadFailed(object sender, EventArgs e)
        {
            var obj = (EpisodeDownloader)sender;
        }

        private void EpisodeDownloadComplete(object sender, EventArgs e)
        {
            var obj = (EpisodeDownloader)sender;
        }
    }

    public class EpisodeDownloader
    {
        public int DownloadTaskId { get; private set; }
        public string SaveFolder { get; private set; }
        public string EpisodeName { get; private set; }
        public string AnimeTitle { get; private set; }
        public Uri EpisodeUri { get; private set; }
        public int EpisodeID { get; private set; }
        public event EventHandler DownloadCompleted;
        public event EventHandler DownloadFailed;

        public EpisodeDownloader(int _downloadTaskId, string _downloadFolder)
        {
            Console.WriteLine("> Episode Downloader Started For : {0}", _downloadTaskId);
            DownloadTaskId = _downloadTaskId;
            SaveFolder = _downloadFolder;
            DBHelper.getInstance().setDownloadTaskStatus(_downloadTaskId, DownloaderStatus.Working);
            using (var cmd = new SQLiteCommand("SELECT episode_list.animetitle, episode_list.url FROM episode_list LEFT JOIN download_task ON episode_list.series_id = download_task.series_id AND episode_list.episode_id = download_task.episode_id WHERE download_task.download_task_id = @did"))
            {
                cmd.Parameters.AddWithValue("@did", _downloadTaskId);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    reader.Read();
                    AnimeTitle = reader.GetString(0);
                    EpisodeUri = new Uri(reader.GetString(1));
                    Console.WriteLine("> Episode Downloader Episode Name : {0}", reader.GetString(0));

                }
            }
        }

        public void Start()
        {
            Thread thread1 = new Thread(_threadStart);
            thread1.Start();
        }

        private void _threadStart()
        {
            try
            {
                var embeedUri = getIFrameSrc(EpisodeUri);
                var downloadLinks = getDownloadLinks(embeedUri).OrderByDescending(x => x.Value);
                
                foreach (var item in downloadLinks)
                {
                    Console.WriteLine("Download Link for {0}: quality: {1}p , link: {2}", AnimeTitle, item.Value, item.Key.ToString());
                    // check if link works or move to other
                    bool doTry = true;
                    while (doTry)
                    {
                        try
                        {
                            var res = getHttpRes(item.Key, EpisodeUri);
                            res.Close();
                            if (res.StatusCode == HttpStatusCode.OK)
                            {
                                Console.WriteLine(String.Format("Quality : {0}p is working", item.Value));
                                string file_path = Path.Combine(SaveFolder, MakeValidFileName(String.Format("{0}-{1}p.mp4", AnimeTitle, item.Value)));
                                Directory.CreateDirectory(Path.GetDirectoryName(file_path));
                                SetQuality(DownloadTaskId, item.Value);
                                DownloadFile(item.Key, EpisodeUri, file_path);
                            }
                            else
                            {
                                Console.WriteLine(String.Format("Quality : {0}p is not working", item.Value));
                                doTry = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Downloading File error : " + ex.Message);
                            Thread.Sleep(1000);
                        }
                    }
                }

                DBHelper.getInstance().setDownloadTaskStatus(DownloadTaskId, DownloaderStatus.Completed);
            }
            catch (Exception ex)
            {
                DBHelper.getInstance().setDownloadTaskStatus(DownloadTaskId, DownloaderStatus.Stopped);
                Console.WriteLine(String.Format("DownloadTaskID:{0} Error: {1}", DownloadTaskId, ex.Message));
            }
        }

        private string MakeValidFileName(string filename)
        {
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
            // Builds a string out of valid chars
            var validFilename = new string(filename.Where(ch => !invalidFileNameChars.Contains(ch)).ToArray());
            return validFilename;
        }

        private void DownloadFile(Uri link, Uri referer, string file_path)
        {
            // download file
            long fileSize = 0;
            long existLen = 0;
            int bufferSize = 1024;
            bufferSize *= 1000;
            var full_path = file_path;
            // if file exits load its length
            if (File.Exists(full_path)) existLen = new FileInfo(full_path).Length;
            FileStream writeStream;

            if (existLen > 0)
            {
                writeStream = new FileStream(full_path, System.IO.FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            }
            else
            {
                writeStream = new FileStream(full_path, System.IO.FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            }

            HttpWebRequest httpReq;
            HttpWebResponse httpRes;
            httpReq = (HttpWebRequest)HttpWebRequest.Create(link);
            httpReq.Referer = referer.ToString();
            httpReq.AddRange((int)existLen);
            httpRes = (HttpWebResponse)httpReq.GetResponse();
            Stream resStream = httpRes.GetResponseStream();

            fileSize = httpRes.ContentLength;

            int byteSize;
            byte[] downBuffer = new byte[bufferSize];
            Stopwatch sw = new Stopwatch();

            sw.Start();
            while ((byteSize = resStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
            {
                sw.Stop();
                double speed = 0;
                try
                {
                    speed = byteSize / sw.ElapsedMilliseconds*1.0;
                    speed = speed * 100;
                }
                catch (Exception ex)
                {
                }
                writeStream.Write(downBuffer, 0, byteSize);
                UpdateDownloadTask(DownloadTaskId, writeStream.Position, fileSize, speed);
                sw.Restart();

            }
        }

        private void UpdateDownloadTask(int downloadTaskId, long downloadedLength, long totalLength, double speed)
        {
            using (var cmd = new SQLiteCommand("UPDATE download_task SET downloaded_size = @dsz, total_size = @tsz, speed = @speed WHERE download_task_id = @id "))
            {
                cmd.Parameters.AddWithValue("@dsz", downloadedLength);
                cmd.Parameters.AddWithValue("@tsz", totalLength);
                cmd.Parameters.AddWithValue("@speed", speed);
                cmd.Parameters.AddWithValue("@id", downloadTaskId);
                DBHelper.getInstance().executeNonQuery(cmd);
            }
        }

        private void SetQuality(int downloadTaskId, int quality)
        {
            using (var cmd = new SQLiteCommand("UPDATE download_task SET quality = @q WHERE download_task_id = @id"))
            {
                cmd.Parameters.AddWithValue("@q", quality);
                cmd.Parameters.AddWithValue("@id", downloadTaskId);
                DBHelper.getInstance().executeNonQuery(cmd);
            }
        }


        private HttpWebResponse getHttpRes(Uri link, Uri referer)
        {
            HttpWebRequest httpReq;
            HttpWebResponse httpRes;
            httpReq = (HttpWebRequest)HttpWebRequest.Create(link);
            httpReq.Referer = referer.ToString();
            httpRes = (HttpWebResponse)httpReq.GetResponse();
            Console.WriteLine(String.Format("If Link is working uri : {0} refer {1} code {2}", link, referer, httpRes.StatusCode));

            return httpRes;

        }

        private Dictionary<Uri, int> getDownloadLinks(Uri link)
        {
            Dictionary<Uri, int> list = new Dictionary<Uri, int>();
            var web = new HtmlAgilityPack.HtmlWeb();
            var doc = web.Load(link.ToString());
            var scriptTag = doc.DocumentNode.SelectSingleNode("/html/body/script[1]");
            var script = scriptTag.InnerHtml;
            Regex re1 = new Regex(@"var videoSources = \[(.+)\]");
            var match1 = re1.Match(script);
            if (match1.Success)
            {
                string source = match1.Groups[1].Value;
                var re2 = new Regex(@"file: ""([\w:\/\.]+)"", label: ""([\d]+)p""");
                var match2 = re2.Match(source);
                while (match2.Success)
                {
                    list.Add(new Uri(match2.Groups[1].Value), int.Parse(match2.Groups[2].Value));
                    match2 = match2.NextMatch();
                }
            }
            else
            {
                Console.WriteLine(String.Format("Cant find videoSources in {0}", link.ToString()));
            }
            return list;
        }

        private Uri getIFrameSrc(Uri baseUri)
        {
            var web = new HtmlAgilityPack.HtmlWeb();
            var doc = web.Load(baseUri.ToString());
            var iframe = doc.DocumentNode.SelectNodes("//iframe");
            return new Uri(baseUri, iframe[0].Attributes["src"].Value);
        }
    }

    public class FileDownloader
    {
        public Uri DownloadUri { get; private set; }
        public string SavePath { get; private set; }

        public FileDownloader(Uri downloadUri, string fs_path)
        {
            DownloadUri = downloadUri;
            SavePath = fs_path;
        }
    }
}

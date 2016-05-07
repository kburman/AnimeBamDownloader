using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AnimeBamDownloader1.Logic
{

    public class SeriesDownloaderOld
    {
        public int series_id { get; private set; }
        
        private BackgroundWorker worker = new BackgroundWorker();


        public SeriesDownloaderOld(int series_download_list_id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM series_download_list WHERE series_download_list.id = @id"))
            {
                cmd.Parameters.AddWithValue("@id", series_download_list_id);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    
                }
            }
            resetAllWorkingToStopped();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            int downloadTask = (int)e.Argument;
            downloadIt(downloadTask);
        }

        private Uri getEpisodeURL(int downloadTaskId)
        {
            using (var cmd = new SQLiteCommand("SELECT episode_list.url FROM download_task LEFT JOIN episode_list ON download_task.series_id = episode_list.series_id AND download_task.episode_id = episode_list.episode_id WHERE download_task.download_task_id = @did"))
            {
                cmd.Parameters.AddWithValue("@did", downloadTaskId);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    if (reader.Read())
                    {
                        return new Uri(reader.GetString(0));
                        // update status to working
                        //DBHelper.getInstance().setDownloadTaskStatus(downloadTaskId, DownloaderStatus.Working);
                        // now get download url

                    }
                    else
                    {

                    }
                }
            }

            return null;
        }

        private Dictionary<Uri, int> getDownloadLinks(Uri episodeUri)
        {
            var d = new Dictionary<Uri, int>();
            // get html code
            var web = new HtmlAgilityPack.HtmlWeb();
            var doc = web.Load(episodeUri.ToString());
            var embedLink = doc.DocumentNode.SelectNodes("//iframe");
            var doc1 = web.Load(new Uri(episodeUri, embedLink[0].Attributes["src"].Value).ToString());
            var scriptTag = doc1.DocumentNode.SelectSingleNode("/html/body/script[1]");
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
                    d.Add(new Uri(match2.Groups[1].Value), int.Parse(match2.Groups[2].Value));
                    match2 = match2.NextMatch();
                }
            }

            return d;
        }

        private void downloadIt(int downloadTaskId)
        {
            var url = getEpisodeURL(downloadTaskId);
            DBHelper.getInstance().setDownloadTaskStatus(downloadTaskId, DownloaderStatus.Working);
            var links = getDownloadLinks(url).OrderByDescending(x => x.Value).Select(p => p.Key).ToList();
            bool downloaded = false;
            int index = 0;
            while (!downloaded && index < links.Count)
            {
                var link = links[index];
                var fs_path = Path.Combine(DBHelper.getInstance().getDownloadDir(downloadTaskId),downloadTaskId.ToString() + ".mp4");
                try
                {
                    downloadFile(link.ToString(), fs_path, url);
                }
                catch (Exception ex)
                {
                    
                }
                index++;
            }
        }

        public static void downloadFile(string sourceURL, string destinationPath, Uri baseUri)
        {
            long fileSize = 0;
            int bufferSize = 1024;
            bufferSize *= 1000;
            long existLen = 0;

            System.IO.FileStream saveFileStream;
            if (System.IO.File.Exists(destinationPath))
            {
                System.IO.FileInfo destinationFileInfo = new System.IO.FileInfo(destinationPath);
                existLen = destinationFileInfo.Length;
            }

            if (existLen > 0)
                saveFileStream = new System.IO.FileStream(destinationPath,
                                                          System.IO.FileMode.Append,
                                                          System.IO.FileAccess.Write,
                                                          System.IO.FileShare.ReadWrite);
            else
                saveFileStream = new System.IO.FileStream(destinationPath,
                                                          System.IO.FileMode.Create,
                                                          System.IO.FileAccess.Write,
                                                          System.IO.FileShare.ReadWrite);

            System.Net.HttpWebRequest httpReq;
            System.Net.HttpWebResponse httpRes;
            httpReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(sourceURL);
            httpReq.AddRange((int)existLen);
            httpReq.Referer = baseUri.ToString();
            System.IO.Stream resStream;
            httpRes = (System.Net.HttpWebResponse)httpReq.GetResponse();
            resStream = httpRes.GetResponseStream();

            fileSize = httpRes.ContentLength;

            int byteSize;
            byte[] downBuffer = new byte[bufferSize];

            while ((byteSize = resStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
            {
                saveFileStream.Write(downBuffer, 0, byteSize);
            }
        }

        private void resetAllWorkingToStopped()
        {
            using (var cmd = new SQLiteCommand("UPDATE download_task SET status=@newstatus WHERE status=@oldstatus AND series_id = @sid"))
            {
                cmd.Parameters.AddWithValue("@oldstatus", (int)DownloaderStatus.Working);
                cmd.Parameters.AddWithValue("@newstatus", (int)DownloaderStatus.Stopped);
                cmd.Parameters.AddWithValue("@sid", series_id);
                DBHelper.getInstance().executeNonQuery(cmd);
            }
        }

        public void start()
        {
            worker.RunWorkerAsync(getEpisodeToDownload());
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns>DownloadTaskID</returns>
        private int getEpisodeToDownload()
        {
            // first find any stopped download
            using (var cmd = new SQLiteCommand("SELECT download_task.download_task_id FROM episode_list  LEFT JOIN  download_task ON episode_list.series_id = download_task.series_id AND episode_list.episode_id = download_task.episode_id WHERE episode_list.series_id = @sid AND download_task.status = @dstatus AND download_task.download_task_id is not NULL LIMIT 1"))
            {
                cmd.Parameters.AddWithValue("@sid", series_id);
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
                cmd.Parameters.AddWithValue("@sid", series_id);
                using (var reader = DBHelper.getInstance().executeQuery(cmd))
                {
                    if (reader.Read())
                    {
                        return addDownloadTask(reader.GetInt32(1), reader.GetInt32(0));
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
    }


}

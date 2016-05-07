using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AnimeBamDownloader1.Logic
{
    public class DBHelper
    {
        private const string APP_FOLDER_NAME = "AnimeBamDownloader";
        private const string DB_FILE = "app_db.sqlite";
        public SQLiteConnection _conn;
        private static DBHelper _instance_ = new DBHelper();

        public DBHelper()
        {
            createDBIfNeeded();
            string connString = String.Format("Data Source={0};Version=3;", getDBPath());
            _conn = new SQLiteConnection(connString);
            _conn.Open();
            
        }
        public static DBHelper getInstance()
        {
            return _instance_;
        }
        private string getDBPath()
        {
            return Path.Combine(getAppDataDir(), DB_FILE);
        }
        private string getAppDataDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), APP_FOLDER_NAME);
        }
        public SQLiteDataReader executeQuery(SQLiteCommand cmd)
        {
            cmd.Connection = _conn;
            return cmd.ExecuteReader();
        }
        public int executeNonQuery(SQLiteCommand cmd)
        {
            cmd.Connection = _conn;
            return cmd.ExecuteNonQuery();
        }

        private bool tableExits(SQLiteConnection conn, string table_name)
        {
            string sql_cmd = String.Format("SELECT name FROM sqlite_master WHERE type='table' AND name='{0}';", table_name);
            SQLiteCommand cmd = new SQLiteCommand(sql_cmd, conn);
            return cmd.ExecuteScalar() != null;
        }


        private void createDBIfNeeded()
        {
            string dbPath = getDBPath();
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            if (!File.Exists(dbPath)) SQLiteConnection.CreateFile(dbPath);
            SQLiteConnection conn = new SQLiteConnection(String.Format("Data Source={0};Version=3;", getDBPath()));
            conn.Open();
            
            if (!tableExits(conn, "series_info"))
            {
                string create_cmd = @"CREATE TABLE `series_info` (
	                                    `series_id`	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
	                                    `url`	TEXT NOT NULL,
	                                    `name`	TEXT,
	                                    `status`	TEXT,
	                                    `thumbnail_url`	TEXT,
	                                    `description`	TEXT,
	                                    `genre`	TEXT
                                    )";
                SQLiteCommand cmd = new SQLiteCommand(create_cmd, conn);
                cmd.ExecuteNonQuery();
            }

            if (!tableExits(conn, "episode_list"))
            {
                string create_cmd = @"CREATE TABLE `episode_list` (
	                                    `episode_id`	INTEGER NOT NULL DEFAULT 0,
	                                    `series_id`	INTEGER NOT NULL,
	                                    `url`	TEXT,
	                                    `name`	TEXT,
	                                    `animetitle`	TEXT,
	                                    `types`	TEXT,
	                                    `checked`	INTEGER NOT NULL DEFAULT 0
                                    )";
                SQLiteCommand cmd = new SQLiteCommand(create_cmd, conn);
                cmd.ExecuteNonQuery();
            }

            if (!tableExits(conn, "series_download_list"))
            {
                string create_cmd = @"CREATE TABLE `series_download_list` (
	                                    `id`	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
	                                    `series_id`	INTEGER NOT NULL,
	                                    `save_folder`	TEXT NOT NULL,
	                                    `status`	INTEGER NOT NULL DEFAULT 0
                                    )";
                SQLiteCommand cmd = new SQLiteCommand(create_cmd, conn);
                cmd.ExecuteNonQuery();
            }

            if (!tableExits(conn, "download_task"))
            {
                string create_cmd = @"CREATE TABLE `download_task` (
	                                    `download_task_id`	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
	                                    `series_id`	INTEGER NOT NULL,
	                                    `episode_id`	INTEGER NOT NULL,
	                                    `quality`	TEXT,
	                                    `downloaded_size`	INTEGER,
	                                    `total_size`	INTEGER,
	                                    `speed`	REAL,
	                                    `status`	INTEGER
                                    )";
                SQLiteCommand cmd = new SQLiteCommand(create_cmd, conn);
                cmd.ExecuteNonQuery();
            }

            conn.Close();
            conn.Dispose();
        }

        public int getLastRowId()
        {
            var cmd = new SQLiteCommand("select last_insert_rowid()", _conn);
            var id = (long)cmd.ExecuteScalar();
            cmd.Dispose();
            return (int)id;
        }

        public string getLocalThumbnailPath(int series_id)
        {
            string fs_path = "";
            try
            {
                string image_path = Path.Combine(getAppDataDir(), "thumbnail_cache", series_id.ToString() + ".jpg");
                Directory.CreateDirectory(Path.GetDirectoryName(image_path));
                if (File.Exists(image_path))
                {
                    fs_path = image_path;
                }
                else
                {
                    using (var cmd = new SQLiteCommand("select series_info.thumbnail_url from series_info where series_info.series_id=@id"))
                    {
                        cmd.Parameters.AddWithValue("@id", series_id);
                        using (var reader = executeQuery(cmd))
                        {
                            if (reader.Read())
                            {
                                using (var webClint = new WebClient())
                                {
                                    int count = 0;
                                    bool done = false;
                                    while (!done && count++ < 5)
                                    {
                                        try
                                        {
                                            webClint.DownloadFile(reader.GetString(0), image_path);
                                            done = true;
                                            fs_path = image_path;
                                        }
                                        catch (Exception ex)
                                        {

                                        }
                                    }
                                }
                            }
                            reader.Close();
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {

            }

            return fs_path;
        }

        public int insertSeries(Series series, List<Episode> episodeList)
        {

            string insert_cmd1 = @"INSERT INTO series_info(url, name, status, thumbnail_url, description, genre)
                                    VALUES (@url, @name, @status, @thumbnail, @desc, @genre)";
            var cmd1 = new SQLiteCommand(insert_cmd1, _conn);
            cmd1.Parameters.AddWithValue("@url", series.url.ToString());
            cmd1.Parameters.AddWithValue("@name", series.name);
            cmd1.Parameters.AddWithValue("@status", series.status);
            cmd1.Parameters.AddWithValue("@thumbnail", series.thumbnail_url.ToString());
            cmd1.Parameters.AddWithValue("@desc", series.description);
            cmd1.Parameters.AddWithValue("@genre", String.Join(",", series.genre));
            cmd1.ExecuteNonQuery();
            cmd1.Dispose();

            int series_id = getLastRowId();


            var transaction = _conn.BeginTransaction();
            var episode_id = 0;
            foreach (var episode in episodeList)
            {
                if (!episode.isChecked) continue;
                string insert_cmd2 = @"INSERT INTO episode_list (episode_id, series_id, url, name, animetitle, types, checked)
                                            VALUES (@eid, @sid, @url, @name, @atitle, @types, @checked)";
                using (var cmd2 = new SQLiteCommand(insert_cmd2, _conn))
                {
                    cmd2.Parameters.AddWithValue("@eid", episode_id);
                    cmd2.Parameters.AddWithValue("@sid", series_id);
                    cmd2.Parameters.AddWithValue("@url", episode.url.ToString());
                    cmd2.Parameters.AddWithValue("@name", episode.name);
                    cmd2.Parameters.AddWithValue("@atitle", episode.animetitle);
                    cmd2.Parameters.AddWithValue("@types", String.Join(",", episode.types));
                    cmd2.Parameters.AddWithValue("@checked", episode.isChecked ? 1 : 0);
                    cmd2.ExecuteNonQuery();
                    episode_id++;
                }
            }
            transaction.Commit();
            return series_id;

        }

        public int addToDownloadList(int series_id, string save_location)
        {
            string insert_cmd1 = @"INSERT INTO series_download_list(series_id, save_folder, status)
                                    VALUES (@sid, @sf, @status)";

            using (var cmd1 = new SQLiteCommand(insert_cmd1, _conn))
            {
                cmd1.Parameters.AddWithValue("@sid", series_id);
                cmd1.Parameters.AddWithValue("@sf", save_location);
                cmd1.Parameters.AddWithValue("@status", DownloaderStatus.Working);
                cmd1.ExecuteNonQuery();
            }
            return getLastRowId();
        }

        public void setDownloadTaskStatus(int id, DownloaderStatus status)
        {
            using (var cmd1 = new SQLiteCommand("UPDATE download_task SET status=@ns WHERE download_task_id=@id"))
            {
                cmd1.Parameters.AddWithValue("@ns", DownloaderStatus.Working);
                cmd1.Parameters.AddWithValue("@id", id);
                executeNonQuery(cmd1);
            }
        }

        private Episode getEpisodeDetail(int series_id, int episode_id)
        {
            Episode e = new Episode();
            using (var cmd = new SQLiteCommand("SELECT * from episode_list WHERE episode_list.series_id = @sid AND episode_list.episode_id = @eid"))
            {
                cmd.Parameters.AddWithValue("@sid", series_id);
                cmd.Parameters.AddWithValue("@eid", episode_id);
                using (var reader = executeQuery(cmd))
                {
                    e.name = reader.GetString(3);
                    e.animetitle = reader.GetString(4);
                    e.episode_id = episode_id;
                    e.series_id = series_id;
                    e.isChecked = reader.GetBoolean(6);
                    e.url = new Uri(reader.GetString(2));
                    e.types = reader.GetString(5).Split(',').ToList();
                }
            }
            return e;
        }

        public string getDownloadDir(int seriesId)
        {
            using (var cmd1 = new SQLiteCommand("SELECT series_download_list.save_folder FROM series_download_list LEFT JOIN download_task ON download_task.series_id = series_download_list.series_id WHERE series_download_list.series_id = @id"))
            {
                cmd1.Parameters.AddWithValue("@id", seriesId);
                using (var reader = executeQuery(cmd1))
                {
                    if (reader.Read())
                    {
                        return reader.GetString(0);
                    }
                    else
                    {
                        return getAppDataDir();
                    }
                }
            }
        }
        

    }
}

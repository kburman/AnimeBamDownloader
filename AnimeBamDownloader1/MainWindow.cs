using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AnimeBamDownloader1
{
    public partial class MainWindow : Form
    {
        Logic.DBHelper dbhelper = Logic.DBHelper.getInstance();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            AddNew newWindow = new AddNew();
            var res = newWindow.ShowDialog();
            if (res == DialogResult.OK)
            {
                reloadDownloadList();
            }
            else
            {
                
            }

            reloadDownloadList();
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            reloadDownloadList();
            reloadLowerWindow();
        }

        private void reloadDownloadList()
        {
            using (var cmd = new SQLiteCommand("select series_download_list.id, series_download_list.status, series_download_list.save_folder, series_info.name, series_info.series_id from series_download_list inner join series_info on series_download_list.series_id = series_info.series_id"))
            {
                using (var reader = Logic.DBHelper.getInstance().executeQuery(cmd))
                {
                    listView1.Items.Clear();
                    while (reader.Read())
                    {
                        ListViewItem itm = new ListViewItem(reader.GetInt32(0).ToString());
                        var dstatus = Enum.Parse(typeof(Logic.DownloaderStatus), reader.GetInt32(1).ToString());
                        itm.SubItems.Add(reader.GetString(3));
                        itm.SubItems.Add(dstatus.ToString());
                        itm.SubItems.Add(reader.GetString(2));
                        itm.Tag = reader.GetValue(4);
                        listView1.Items.Add(itm);
                    }
                    reader.Close();
                }
            }
            

        }

        private void reloadLowerWindow()
        {
            if (listView1.SelectedItems.Count == 0) return;
            reloadDescription();
            reloadEpisodeList();
        }

        private void reloadDescription()
        {
            if (listView1.SelectedItems.Count == 0) return;
            int series_id = Int32.Parse(listView1.SelectedItems[0].Tag.ToString());

            using (var cmd = new SQLiteCommand("select * from series_info where series_info.series_id = @sid"))
            {
                cmd.Parameters.AddWithValue("@sid", series_id);
                using (var reader = Logic.DBHelper.getInstance().executeQuery(cmd))
                {
                    if (reader.Read())
                    {
                        label1.Text = reader.GetString(2);
                        label2.Text = reader.GetString(3);
                        label3.Text = "Genre : " + reader.GetString(6);
                        label4.Text = reader.GetString(5);
                        pictureBox1.ImageLocation = dbhelper.getLocalThumbnailPath(series_id);
                    }
                    reader.Close();
                }
            }
            
        }

        private void reloadEpisodeList()
        {
            if (listView1.SelectedItems.Count == 0) return;
            int series_id = Int32.Parse(listView1.SelectedItems[0].Tag.ToString());
            using (var cmd = new SQLiteCommand("select * from episode_list left join download_task on episode_list.series_id = download_task.series_id and episode_list.episode_id = download_task.episode_id where episode_list.series_id = @sid"))
            {
                cmd.Parameters.AddWithValue("@sid", series_id);
                using (var reader = Logic.DBHelper.getInstance().executeQuery(cmd))
                {
                    try
                    {
                        listView2.BeginUpdate();
                        listView2.Items.Clear();
                        while (reader.Read())
                        {
                            ListViewItem i = new ListViewItem(reader.GetInt32(0).ToString());
                            var obj = reader.GetValue(7).ToString();
                            i.SubItems.Add(reader.GetValue(4).ToString()); // Name
                            i.SubItems.Add(reader.GetValue(3).ToString()); // Anime Title
                            i.SubItems.Add(reader.GetValue(2).ToString()); // URL
                                                                           // download status
                            if (reader.GetValue(14).ToString() == "")
                            {
                                i.SubItems.Add("");
                            }
                            else
                            {
                                var a = Enum.Parse(typeof(Logic.DownloaderStatus), reader.GetValue(14).ToString());
                                i.SubItems.Add(a.ToString());
                            }

                            i.SubItems.Add(reader.GetValue(11).ToString()); // downloaded size
                            i.SubItems.Add(reader.GetValue(12).ToString()); // total size
                            i.SubItems.Add(reader.GetValue(13).ToString()); // speed
                            listView2.Items.Add(i);
                        }
                    }
                    finally
                    {
                        listView2.EndUpdate();
                        reader.Close();
                    }
                }
            }

           
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            reloadLowerWindow();
        }



        private void updateStatusOfSeries(int series_id, Logic.DownloaderStatus status)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE series_download_list SET status=@status WHERE id=@id"))
            {
                cmd.Parameters.AddWithValue("@status", (int)status);
                cmd.Parameters.AddWithValue("@id", series_id);
                Logic.DBHelper.getInstance().executeNonQuery(cmd);

            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;
            try
            {
                int download_series_id = int.Parse(listView1.SelectedItems[0].Text);
                updateStatusOfSeries(download_series_id, Logic.DownloaderStatus.Working);
                reloadDownloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;
            try
            {
                int download_series_id = int.Parse(listView1.SelectedItems[0].Text);
                updateStatusOfSeries(download_series_id, Logic.DownloaderStatus.Stopped);
                reloadDownloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void startDownloaderForToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var a = new AnimeBamDownloader1.Logic.SeriesDownloader(1);
                a.start();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}

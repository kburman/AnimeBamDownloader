using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AnimeBamDownloader1
{
    public partial class AddNew : Form
    {
        private Logic.Series _series;
        private List<Logic.Episode> _episodeList;
        public AddNew()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                this.Enabled = false;
                HtmlWeb web = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = web.Load(tbUrl.Text);
                var series = Logic.Parser.getSeriesInfo(doc, new Uri(tbUrl.Text));
                var episodeList = Logic.Parser.getEpisodeList(doc, new Uri(tbUrl.Text));

                _series = series;
                _episodeList = episodeList;


                pictureBox1.ImageLocation = series.thumbnail_url.ToString();
                lblTitle.Text = series.name;
                lblStatus.Text = series.status;
                lblEpisodeCount.Text = String.Format("Episode Count : {0}", episodeList.Count);
                lblDescription.Text = series.description;

                lvEpisodeList.Items.Clear();
                foreach (var item in episodeList)
                {
                    String[] arr = new String[] { item.name,
                              item.animetitle,
                              String.Join(",", item.types)
                    };
                    var itm = new ListViewItem(arr);
                    itm.Checked = true;
                    lvEpisodeList.Items.Add(itm);
                }
                BtnAdd.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                BtnAdd.Enabled = false;
            }
            finally
            {
                this.Enabled = true;
            }
            
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dig = new FolderBrowserDialog();
            dig.RootFolder = Environment.SpecialFolder.MyComputer;
            dig.Description = "Select where you want to save videos";
            if (dig.ShowDialog() == DialogResult.OK)
            {
                var series_id = Logic.DBHelper.getInstance().insertSeries(_series, _episodeList);
                Logic.DBHelper.getInstance().addToDownloadList(series_id, dig.SelectedPath);
                Logic.DBHelper.getInstance().getLocalThumbnailPath(_series.series_id);

            }
        }
    }
}

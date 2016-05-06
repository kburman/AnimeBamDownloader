using HtmlAgilityPack;
using System;
using System.Collections.Generic;

namespace AnimeBamDownloader1.Logic
{
    public class Parser
    {
        public static Series getSeriesInfo(HtmlAgilityPack.HtmlDocument doc, Uri baseUri)
        {
            Series obj = new Series();
            HtmlNode titleNode = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/div/div/div[1]/h1");
            HtmlNode thumbnailNode = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/div/a/img");
            HtmlNode episodeCount = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/div/div/div[1]/p/span[1]");
            HtmlNode status = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/div/div/div[1]/p/span[2]");
            HtmlNodeCollection genre = doc.DocumentNode.SelectNodes("/html/body/div[2]/div/div/div/div/div[2]/ul/li/a");
            HtmlNode description = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/div/div/div[2]/p");

            obj.name = titleNode.InnerText;
            obj.description = description.InnerText;
            obj.thumbnail_url = new Uri(baseUri, thumbnailNode.Attributes["src"].Value);
            obj.status = status.InnerText.Split(':')[1].Trim();
            obj.url = baseUri;

            foreach (var item in genre)
            {
                obj.genre.Add(item.InnerText);
            }

            return obj;
        }

        public static List<Episode> getEpisodeList(HtmlAgilityPack.HtmlDocument doc, Uri baseUri)
        {
            List<Episode> lst = new List<Episode>();
            HtmlNodeCollection episodeList = doc.DocumentNode.SelectNodes("/html/body/div[3]/div/div[1]/div[2]/ul/li");
            foreach (var item in episodeList)
            {
                Episode e = new Episode();
                HtmlNode link = item.SelectSingleNode("./div/a[@class='anm_det_pop']");
                HtmlNode name = item.SelectSingleNode("./div/a[@class='anm_det_pop']/strong");
                HtmlNode animTitle = item.SelectSingleNode("./div/i[@class='anititle']");
                HtmlNodeCollection types = item.SelectNodes("./div/i[contains(@class, 'btn-xs')]");

                e.name = name.InnerText;
                e.animetitle = animTitle.InnerText;
                e.url = new Uri(baseUri, link.Attributes["href"].Value);
                e.isChecked = true;

                if (types != null && types.Count > 0)
                {
                    foreach (var item1 in types)
                    {
                        e.types.Add(item1.InnerText);
                    }
                }

                lst.Add(e);
            }

            return lst;
        }
    }
}

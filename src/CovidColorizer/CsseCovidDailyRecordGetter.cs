namespace CovidColorizer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Octokit;
    partial class Program
    {

        class CsseCovidDataFilename : IComparable<CsseCovidDataFilename>
        {
            public CsseCovidDataFilename(RepositoryContent entry)
            {
                string[] halves = entry.Name.Split('.');
                // string[] components = halves[0].Split('-');

                // var m = Convert.ToInt16(components[0]);
                // var d = Convert.ToInt16(components[1]);
                // var y = Convert.ToInt16(components[2]);
                DateStamp = DateTime.ParseExact(halves[0], "mm-dd-yyyy", new System.Globalization.CultureInfo("en-US"));
                DownloadUrl = entry.DownloadUrl;
            }

            public int CompareTo(CsseCovidDataFilename other)
            {
                if (other == null)
                {
                    // we are greater than null
                    return 1;
                }
                return this.DateStamp.CompareTo(other.DateStamp);
            }
            public DateTime DateStamp { get; protected set; }
            public string DownloadUrl { get; protected set; }
        }
        /// <summary>
        /// Retrieves the CSSE John Hopkins CSV data.
        /// </summary>
        class CsseCovidDailyRecordGetter
        {
            public async Task<string> GetLatestFilename()
            {
                var contents = from entry in await Client
                    .Repository
                    .Content
                    .GetAllContents("CSSEGISandData", "COVID-19", "csse_covid_19_data/csse_covid_19_daily_reports")
                    where entry.Name.EndsWith(".csv")
                    select entry;
                var files = new List<CsseCovidDataFilename>();
                foreach (var entry in contents)
                {
                    files.Add(new CsseCovidDataFilename(entry));
                }

                files.Sort();
                var newestFile = files.Last();

                Console.WriteLine("last entry: {0}", files[files.Count - 1].DownloadUrl);
                if (newestFile == null)
                {
                    return null;
                }
                return newestFile.DownloadUrl;
            }

            public async Task Go()
            {
                var url = GetLatestFilename();
                var http = new HttpClient();
                Contents = await http.GetStringAsync(await url);
            }
            public string Contents { get; set; }
            private GitHubClient Client = new GitHubClient(new ProductHeaderValue("covid-colorizer"));
        }
    }
}

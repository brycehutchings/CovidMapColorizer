namespace CovidPivot
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Xml.Linq;
    using CsvHelper;
    using CsvHelper.Configuration.Attributes;

    class Program
    {
        class CsseCovidDailyRecord
        {
            [Name("FIPS")]
            public string Fips { get; set; }

            [Name("Admin2")]
            public string County { get; set; }

            [Name("Province_State")]
            public string ProvinceState { get; set; }

            [Name("Country_Region")]
            public string CountryRegion { get; set; }

            [Name("Last_Update")]
            public DateTime LastUpdate { get; set; }

            [Name("Lat")]
            public float Lat { get; set; }

            [Name("Long_")]
            public float Longitude { get; set; }

            [Name("Confirmed")]
            public int Confirmed { get; set; }

            [Name("Deaths")]
            public int Deaths { get; set; }

            [Name("Recovered")]
            public int Recovered { get; set; }

            [Name("Active")]
            public int Active { get; set; }

            [Name("Combined_Key")]
            public string CombinedKey { get; set; }
        }

        static void Main(string[] args)
        {
            // SVG:https://en.wikipedia.org/wiki/File:USA_Counties.svg
            // County population: https://www2.census.gov/programs-surveys/popest/tables/2010-2019/counties/totals/co-est2019-annres.xlsx
            // Census County reference: https://www2.census.gov/geo/docs/reference/codes/files/national_county.txt

            var countyRecords = LoadCsseCovidDailyRecords(@"C:\git\COVID-19\csse_covid_19_data\csse_covid_19_daily_reports\03-25-2020.csv");
            ColorizeCountySvg(countyRecords, @"Usa_counties_large.svg", @"Usa_counties_large_covid_colorized.svg");
        }

        static void ColorizeCountySvg(Dictionary<string, CsseCovidDailyRecord> dailyCovidRecords, string originalSvgPath, string newColorizedSvgPath)
        {
            XDocument countySvgMap = XDocument.Load(originalSvgPath);

            var ns = XNamespace.Get("http://www.w3.org/2000/svg");
            foreach (XElement countyPath in countySvgMap.Element(ns + "svg").Element(ns + "g").Elements(ns + "path"))
            {
                var fips = (string)countyPath.Attribute("id");
                if (fips.Length <= 1 || fips[0] != 'c')
                {
                    Console.Error.WriteLine($"County path has unexpected FIPS id '{fips}'");
                }

                fips = fips.Substring(1); // Skip the 'c' to get the FIPS value that matches the Ccse covid data.

                CsseCovidDailyRecord countyCovidRecord;
                if (dailyCovidRecords.TryGetValue(fips, out countyCovidRecord))
                {
                    // Hack in a color for now as proof of concept.
                    var r = Math.Min((int)(countyCovidRecord.Confirmed / (float)500 * 255), 255);
                    var g = 255 - r;
                    var color = "#" + r.ToString("X2") + g.ToString("X2") + "FF";

                    countyPath.SetAttributeValue("style", "stroke:green; fill: " + color);

                    // TODO: Add Death/Confirmed to tool tip.
                }
                else
                {
                    Console.WriteLine($"Missing county: {countyCovidRecord.CombinedKey}");
                }
            }

            countySvgMap.Save(newColorizedSvgPath);
        }

        static Dictionary<string, CsseCovidDailyRecord> LoadCsseCovidDailyRecords(string csvPath)
        {
            var countyRecords = new Dictionary<string, CsseCovidDailyRecord>();

            using (var reader = new StreamReader(csvPath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                foreach (var countyRecord in csv.GetRecords<CsseCovidDailyRecord>())
                {
                    if (countyRecord.CountryRegion != "US")
                    {
                        continue; // Ignore record for non-US county (countries).
                    }

                    if (string.IsNullOrEmpty(countyRecord.Fips))
                    {
                        // TODO: Fix up the limited bad data here.
                        Console.Error.WriteLine($"Ignoring US record with empty FIPS ({countyRecord.CombinedKey}).");
                        continue;
                    }

                    if (!countyRecords.TryAdd(countyRecord.Fips, countyRecord))
                    {
                        //
                        // Unfortunately there are duplicates like FIPS 35013 listed as both Dona Ana and Doña Ana with different confirmed counts.
                        //
                        Console.Error.WriteLine($"Merging Duplicate FIPS {countyRecord.Fips} ({countyRecord.CombinedKey}).");

                        var existingCountyRecord = countyRecords[countyRecord.Fips];
                        existingCountyRecord.Confirmed = Math.Max(existingCountyRecord.Confirmed, countyRecord.Confirmed);
                        existingCountyRecord.Deaths = Math.Max(existingCountyRecord.Deaths, countyRecord.Deaths);
                        existingCountyRecord.Recovered = Math.Max(existingCountyRecord.Recovered, countyRecord.Recovered);
                        existingCountyRecord.Active = Math.Max(existingCountyRecord.Active, countyRecord.Active);
                    }

                }
            }

            return countyRecords;
        }
    }
}

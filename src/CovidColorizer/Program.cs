namespace CovidColorizer
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using System.Linq;
    using System.Xml.Linq;
    using CsvHelper;

    partial class Program
    {
        private static readonly Color[] ColorGradient = { Color.Yellow, Color.Red, Color.Fuchsia };

        class CountyData
        {
            public float? Rate { get; set; }

            public string TitleSuffix { get; set; }
        }

        static void Main(string[] args)
        {
            // SVG:https://en.wikipedia.org/wiki/File:USA_Counties.svg
            // County population: https://www2.census.gov/programs-surveys/popest/tables/2010-2019/counties/totals/co-est2019-annres.xlsx
            // Census County reference: https://www2.census.gov/geo/docs/reference/codes/files/national_county.txt

            var getter = new CsseCovidDailyRecordGetter();
            getter.Go().Wait();

            var countyPercentCovid = new Dictionary<string, CountyData>();
            Func<CountyData, Color> getFillColor;

            var countyCovidRecords = CsseCovidDailyRecord.ReadCsvFromString(getter.Contents);

            int mode = 1; // This is obviously a hack for now until I do command-line parsing. 0=Normalized to population 1=Rate of change.
            if (mode == 1)
            {
                var countyPopluations = CountyPopulation.LoadCsv(@"../../data/PEP_2018_PEPANNRES_with_ann.csv");

                // Point in time mode
                // Stat: confirmed or dead
                // Modes: Absolute or Per10K
                foreach (var record in countyCovidRecords)
                {
                    int countyPopulation;
                    if (!countyPopluations.TryGetValue(record.Key, out countyPopulation))
                    {
                        Console.Error.WriteLine($"Unable to find county population for {record.Value.CombinedKey}");
                        continue;
                    }

                    float linearValue = record.Value.Confirmed / (float)countyPopulation;
                    string titleSuffix = $"({(int)(linearValue * 100000)} per 100,000 confirmed)";

                    countyPercentCovid.Add(record.Key, new CountyData { Rate = linearValue, TitleSuffix = titleSuffix });
                }

                // Using the county with the highest rate as the maximum will drown out most of the data, so ignore the worst 1%.
                var countiesWithHit = countyPercentCovid.Where(kvp => kvp.Value.Rate > 0).OrderBy(kvp => kvp.Value.Rate).ToList();
                float maxValue = countiesWithHit.Skip((int)(countiesWithHit.Count * 0.99f)).First().Value.Rate.Value;

                Color noneColor = Color.FromArgb(255, 255, 224); // Very light yellow for counties without anything.
                getFillColor = (countyData) => countyData.Rate == 0 ? noneColor : LinearColorize(countyData.Rate.Value, 0, maxValue);
            }
            else
            {
                // Compare mode
                var countyCovidRecordsOld = CsseCovidDailyRecord.ReadCsv(@"C:\git\COVID-19\csse_covid_19_data\csse_covid_19_daily_reports\03-23-2020.csv");
                foreach (var record in countyCovidRecords)
                {
                    CsseCovidDailyRecord oldRecord;
                    if (!countyCovidRecordsOld.TryGetValue(record.Key, out oldRecord))
                    {
                        continue; // This county had no data in the older report.
                    }

                    if (record.Value.Confirmed == 0)
                    {
                        // No cases.
                        string titleSuffix = $"(none)";

                        // FIXME: USes float.MinValue to indicate no new cases in the colorizer stage. I need to move to a custom struct.
                        countyPercentCovid.Add(record.Key, new CountyData { Rate = float.MinValue, TitleSuffix = titleSuffix });
                    }
                    else if (oldRecord.Confirmed == 0)
                    {
                        string titleSuffix = $"({record.Value.Confirmed} new cases with none previously)";
                        countyPercentCovid.Add(record.Key, new CountyData { TitleSuffix = titleSuffix });
                    }
                    else if (record.Value.Confirmed <= oldRecord.Confirmed)
                    {
                        // No new cases.
                        string titleSuffix = $"(no change with {record.Value.Confirmed} cases)";
                        countyPercentCovid.Add(record.Key, new CountyData { Rate = 0, TitleSuffix = titleSuffix });
                    }
                    else
                    {
                        int changeInCount = record.Value.Confirmed - oldRecord.Confirmed;
                        float linearValue = changeInCount / (float)oldRecord.Confirmed;

                        string titleSuffix = $"({changeInCount} new cases. {(linearValue * 100):###}% increase)";

                        countyPercentCovid.Add(record.Key, new CountyData { Rate = linearValue, TitleSuffix = titleSuffix });
                    }
                }

                // Using the county with the highest rate as the maximum will drown out most of the data, so ignore the worst 1%.
                var countiesWithHit = countyPercentCovid.Where(kvp => kvp.Value.Rate > 0).OrderBy(kvp => kvp.Value.Rate).ToList();
                float maxValue = countiesWithHit.Skip((int)(countiesWithHit.Count * 0.99f)).First().Value.Rate.Value;

                Color noneColor = Color.FromArgb(0xFF, 0xFF, 0xF0);             // There are no cases.
                Color noPriorRecordColor = Color.FromArgb(0xFF, 0xFF, 0x90);    // There are cases but there weren't any in the older record.
                Color noChangeColor = Color.FromArgb(0xFF, 0xFF, 0xC0);         // There are cases but it didn't change from the older record.

                getFillColor = (countyData) => !countyData.Rate.HasValue ? noPriorRecordColor :
                    countyData.Rate == 0 ? noChangeColor :
                    countyData.Rate == float.MinValue ? noneColor :
                    LinearColorize(countyData.Rate.Value, 0, maxValue);
            }

            var colorizer = new SvgUSCountyColorizer(@"C:\git\CovidMapColorizer\data\Usa_counties_large.svg");
            colorizer.Colorize(
                countyPercentCovid.ToDictionary(
                    r => r.Key,
                    r => new CountySvgData
                    {
                        FillColor = getFillColor(r.Value),
                        TitleSuffix = r.Value.TitleSuffix
                    }),
                @"Usa_counties_large_covid_colorized.svg");
        }

        private static Color LinearColorize(double value, double min, double max)
        {
            int colorGradientRanges = ColorGradient.Length - 1;

            // Normalize to an index, but fractional.
            double rawIndex = (value - min) / max * colorGradientRanges;

            double colorGradientIndexLow = Math.Floor(rawIndex);
            double t = rawIndex - colorGradientIndexLow;
            double invT = 1 - t;

            Color lowColor = ColorGradient[Math.Min((int)colorGradientIndexLow, ColorGradient.Length - 1)];
            Color highColor = ColorGradient[Math.Min((int)colorGradientIndexLow + 1, ColorGradient.Length - 1)];

            // Turn into HTML color
            Color linearizedColor = Color.FromArgb(
                (int)Math.Round(lowColor.R * invT + highColor.R * t),
                (int)Math.Round(lowColor.G * invT + highColor.G * t),
                (int)Math.Round(lowColor.B * invT + highColor.B * t));
            return linearizedColor;
        }
    }
}

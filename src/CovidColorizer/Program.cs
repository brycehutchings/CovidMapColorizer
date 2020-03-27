namespace CovidColorizer
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using CsvHelper;

    partial class Program
    {
        private static readonly Color[] ColorGradient = { Color.Yellow, Color.Red, Color.Fuchsia };

        class CountyData
        {
            public float Rate { get; set; }

            public string TitleSuffix { get; set; }
        }

        static void Main(string[] args)
        {
            // SVG:https://en.wikipedia.org/wiki/File:USA_Counties.svg
            // County population: https://www2.census.gov/programs-surveys/popest/tables/2010-2019/counties/totals/co-est2019-annres.xlsx
            // Census County reference: https://www2.census.gov/geo/docs/reference/codes/files/national_county.txt

            // Modes:
            // Color normalized by population.
            // Color by rate of change (with avg).

            var countyPopluations = CountyPopulation.LoadCsv(@"C:\git\CovidMapColorizer\data\PEP_2018_PEPANNRES_with_ann.csv");

            var countyCovidRecords = CsseCovidDailyRecord.LoadRecordsFromCsv(@"C:\git\COVID-19\csse_covid_19_data\csse_covid_19_daily_reports\03-26-2020.csv");

            var countyPercentCovid = new Dictionary<string, CountyData>();
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
            float maxValue = countiesWithHit.Skip((int)(countiesWithHit.Count * 0.99f)).First().Value.Rate;

            var colorizer = new SvgUSCountyColorizer(@"C:\git\CovidMapColorizer\data\Usa_counties_large.svg");
            colorizer.Colorize(
                countyPercentCovid.ToDictionary(
                    r => r.Key,
                    r => new CountySvgData
                    {
                        FillColor = LinearColorize(r.Value.Rate, 0, maxValue),
                        TitleSuffix = r.Value.TitleSuffix
                    }),
                @"Usa_counties_large_covid_colorized.svg");
        }

        private static Color LinearColorize(double value, double min, double max)
        {
            if (value == min)
            {
                return Color.FromArgb(255, 255, 224); // Very light yellow for counties without anything.
            }

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

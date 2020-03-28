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
        // private static readonly Color[] ColorGradient = { Color.Yellow, Color.Red, Color.Fuchsia };
        private static readonly Color[] ColorGradient = { Color.LightYellow, Color.DarkOrange, Color.Red, Color.DarkRed  };

        class CountyData
        {
            public double? Rate { get; set; }

            public string TitleSuffix { get; set; }
        }


        // Incidence Rate = new cases over a period of time.
        // Incidence Proportion = new cases over a period of time / population size.
        // Prevalence = proportion affected
        enum GenerationMode
        {
            /// <summary>
            /// Prevalence is the proportion of a particular population found to be affected by COVID.
            /// </summary>
            Prevalence,

            /// <summary>
            /// Frequency/probability with which a disease or other incident occurs over a specified time period
            /// </summary>
            IncidenceRate,
        }

        // Options: ExcludeCriteria=Population, Confirmed, Dead ExcludeValue=##

        static void Main(string[] args)
        {
            // SVG:https://en.wikipedia.org/wiki/File:USA_Counties.svg
            // County population: https://www2.census.gov/programs-surveys/popest/tables/2010-2019/counties/totals/co-est2019-annres.xlsx
            // Census County reference: https://www2.census.gov/geo/docs/reference/codes/files/national_county.txt

            var countyPercentCovid = new Dictionary<string, CountyData>();
            Func<CountyData, Color> getFillColor;

            var countyCovidRecords = CsseCovidDailyRecord.ReadCsv(@"C:\git\COVID-19\csse_covid_19_data\csse_covid_19_daily_reports\03-27-2020.csv");

            Func<CsseCovidDailyRecord, int> getCountyMetric;
            string pluralMetricName;
#if false
            getCountyMetric = (record) => record.Deaths;
            pluralMetricName = "deaths";
#else
            getCountyMetric = (record) => record.Confirmed;
            pluralMetricName = "confirmed";
#endif

            int mode = 0; // This is obviously a hack for now until I do command-line parsing. 0=Normalized to population 1=Rate of change.
            if (mode == 1)
            {
                var countyPopluations = CountyPopulation.LoadCsv(@"C:\git\CovidMapColorizer\data\PEP_2018_PEPANNRES_with_ann.csv");

                // Point in time mode
                // Stat: confirmed or dead
                // Modes: Absolute or Per10K or Dead/Confirmed(fatality rate) or Dead/Population (mortality rate)
                foreach (var record in countyCovidRecords)
                {
                    int countyPopulation;
                    if (!countyPopluations.TryGetValue(record.Key, out countyPopulation))
                    {
                        Console.Error.WriteLine($"Unable to find county population for {record.Value.CombinedKey}");
                        continue;
                    }

#if false
                    float linearValue = getCountyMetric(record.Value) / (float)countyPopulation;
                    string titleSuffix = $"({(int)(linearValue * 100000)} {pluralMetricName} per 100,000)";
#else
                    if (record.Value.Confirmed < 50)
                    {
                        continue;
                    }

                    float linearValue;
                    string titleSuffix;
                    if (record.Value.Confirmed > 0) {
                        linearValue = record.Value.Deaths / (float)record.Value.Confirmed;
                        titleSuffix = $"({record.Value.Deaths} {pluralMetricName} = {(linearValue * 100):#0.0}% fatality rate)";
                    }
                    else
                    {
                        linearValue = 0;
                        titleSuffix = $"(no confirmed cases)";
                    }
#endif

                    countyPercentCovid.Add(record.Key, new CountyData { Rate = linearValue, TitleSuffix = titleSuffix });
                }

                // Using the county with the highest rate as the maximum will drown out most of the data, so ignore the worst 1%.
#if false
                var countiesWithHit = countyPercentCovid.Where(kvp => kvp.Value.Rate > 0).OrderBy(kvp => kvp.Value.Rate).ToList();
                float maxValue = countiesWithHit.Skip((int)(countiesWithHit.Count * 0.99f)).First().Value.Rate.Value;
#else
                float maxValue = 0.1f;
#endif

                Color noneColor = Color.FromArgb(255, 255, 224); // Very light yellow for counties without anything.
                getFillColor = (countyData) => countyData.Rate == 0 ? noneColor : LinearColorize(countyData.Rate.Value, 0, maxValue);
            }
            else
            {
                // Compare mode
                var countyCovidRecordsOld = CsseCovidDailyRecord.ReadCsv(@"C:\git\COVID-19\csse_covid_19_data\csse_covid_19_daily_reports\03-25-2020.csv");
                var totalDaysDifferent = 4;
                foreach (var record in countyCovidRecords)
                {
                    CsseCovidDailyRecord oldRecord;
                    if (!countyCovidRecordsOld.TryGetValue(record.Key, out oldRecord))
                    {
                        continue; // This county had no data in the older report.
                    }

                    var oldMetric = getCountyMetric(oldRecord);
                    var newMetric = getCountyMetric(record.Value);

                    if (newMetric == 0)
                    {
                        // No cases.
                        string titleSuffix = $"(none)";

                        // FIXME: Uses float.MinValue to indicate no new cases in the colorizer stage. I need to move to a custom struct.
                        countyPercentCovid.Add(record.Key, new CountyData { Rate = float.MinValue, TitleSuffix = titleSuffix });
                    }
                    else if (oldMetric == 0)
                    {
                        string titleSuffix = $"({newMetric} new {pluralMetricName} with none previously)";
                        countyPercentCovid.Add(record.Key, new CountyData { TitleSuffix = titleSuffix });
                    }
                    else if (newMetric <= oldMetric)
                    {
                        // No new cases.
                        string titleSuffix = $"(no change with {newMetric} {pluralMetricName})";
                        countyPercentCovid.Add(record.Key, new CountyData { Rate = 0, TitleSuffix = titleSuffix });
                    }
                    else
                    {
                        int changeInCount = newMetric - oldMetric;
                        double absolutePercentIncrease = changeInCount / (double)oldMetric;
                        double dailyPercentIncrease = Math.Pow(1 + absolutePercentIncrease, 1 / (double)totalDaysDifferent) - 1;

                        string titleSuffix = $"({changeInCount} new cases. {dailyPercentIncrease * 100:#0.#}% daily increase)";

                        countyPercentCovid.Add(record.Key, new CountyData { Rate = dailyPercentIncrease, TitleSuffix = titleSuffix });
                    }
                }

                // Using the county with the highest rate as the maximum will drown out most of the data, so ignore the worst 1%.
                // var countiesWithHit = countyPercentCovid.Where(kvp => kvp.Value.Rate > 0).OrderBy(kvp => kvp.Value.Rate).ToList();
                // double maxValue = countiesWithHit.Skip((int)(countiesWithHit.Count * 0.99)).First().Value.Rate.Value;
                double maxValue = countyPercentCovid.Max(kvp => kvp.Value?.Rate).Value;

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

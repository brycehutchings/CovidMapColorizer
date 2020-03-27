namespace CovidColorizer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using CsvHelper;
    using CsvHelper.Configuration.Attributes;

    partial class Program
    {
        /// <summary>
        /// Parses and cleans up the CSSE John Hopkins CSV data.
        /// </summary>
        class CsseCovidDailyRecord
        {
            public static Dictionary<string, CsseCovidDailyRecord> LoadRecordsFromCsv(string csvPath)
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
            public float Confirmed { get; set; }

            [Name("Deaths")]
            public float Deaths { get; set; }

            [Name("Recovered")]
            public float Recovered { get; set; }

            [Name("Active")]
            public float Active { get; set; }

            [Name("Combined_Key")]
            public string CombinedKey { get; set; }
        }
    }
}

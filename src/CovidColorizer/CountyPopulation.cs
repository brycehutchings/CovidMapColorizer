namespace CovidColorizer
{
    using CsvHelper;
    using CsvHelper.Configuration.Attributes;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    static class CountyPopulation
    {
        private struct CsvRecord
        {
            [Name("GEO.id2")]
            public string Fips { get; set; }

            // The latest population data available in the CSV is 2018.
            [Name("respop72018")]
            public int Population { get; set; }
        }

        public static IReadOnlyDictionary<string, int> LoadCsv(string censusPopulationCsv)
        {
            var countyRecords = new Dictionary<string, int>();

            using (var reader = new StreamReader(censusPopulationCsv))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                foreach (var countyRecord in csv.GetRecords<CsvRecord>())
                {
                    countyRecords.Add(countyRecord.Fips, countyRecord.Population);
                }
            }

            return countyRecords;
        }
    }
}

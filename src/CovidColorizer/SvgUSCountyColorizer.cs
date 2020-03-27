namespace CovidColorizer
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Xml.Linq;

    class CountySvgData
    {
        public Color FillColor { get; set; }

        public string TitleSuffix { get; set; }
    }

    class SvgUSCountyColorizer
    {
        private readonly XDocument _countySvgMap;

        public SvgUSCountyColorizer(string originalSvgPath)
        {
            _countySvgMap = XDocument.Load(originalSvgPath);
        }

        // FIXME: Mutates XDocument... better to just load each time.
        public void Colorize(Dictionary<string, CountySvgData> allCountyData, string newColorizedSvgPath)
        {
            var ns = XNamespace.Get("http://www.w3.org/2000/svg");
            foreach (XElement countyPath in _countySvgMap.Element(ns + "svg").Element(ns + "g").Elements(ns + "path"))
            {
                var fips = (string)countyPath.Attribute("id");
                if (fips.Length <= 1 || fips[0] != 'c')
                {
                    Console.Error.WriteLine($"County path has unexpected FIPS id '{fips}'");
                }

                fips = fips.Substring(1); // Skip the 'c' to get the FIPS value that matches the Ccse covid data.

                CountySvgData countyData;
                if (allCountyData.TryGetValue(fips, out countyData))
                {
                    countyPath.SetAttributeValue("style", "stroke:black; fill: " + MakeCssColor(countyData.FillColor));
                    var titleElement = countyPath.Element(ns + "title");
                    titleElement.SetValue(titleElement.Value + " " + countyData.TitleSuffix);
                }
                else
                {
                    Console.WriteLine($"Missing county: {fips}");
                }
            }

            _countySvgMap.Save(newColorizedSvgPath);
        }

        private static string MakeCssColor(Color c) => String.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
    }
}

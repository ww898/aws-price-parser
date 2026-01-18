using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AwsPriceParser
{
    public static class SpotJson
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        private static class Schema
        {
            public record Config(string rate, string[] valueColumns, string[] currencies, Regions[] regions);

            public record InstanceTypes(string type, Sizes[] sizes);

            public record Prices(string USD);

            public record Regions(string region, Dictionary<string, string> footnotes, InstanceTypes[] instanceTypes);

            public record Root(double vers, Config config);

            public record Sizes(string size, ValueColumns[] valueColumns);

            public record ValueColumns(string name, Prices prices);
        }

        private static readonly Dictionary<string, string> OsNameMap = new()
            {
                { "mswin", "Windows" },
                { "linux", "Linux" },
            };

        public static Dictionary<string, Dictionary<string, Dictionary<string, double>>> Read(
            FileInfo file,
            Predicate<string> filterRegion,
            Predicate<string> filterInstanceType,
            Predicate<string> filterOperationSystem)
        {
            Schema.Root root;
            using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                root = JsonSerializer.Deserialize<Schema.Root>(stream)!;

            if (Math.Abs(root.vers - 0.01) >= Definitions.Δ)
                throw new FormatException("Invalid version, 0.01 expected");
            var config = root.config;
            if (!config.currencies.Contains(nameof(Schema.Prices.USD)))
                throw new FormatException("USD is not supported");

            var data = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
            foreach (var (region, footnotes, instanceTypes) in config.regions)
                if (filterRegion(region))
                {
                    var footnotesSet = footnotes.Keys;
                    bool TryGetCurrencyValue(string str, out double value)
                    {
                        str = footnotes.Keys.Aggregate(str, (current, key) => current.Replace(key, ""));
                        if (str == "N/A")
                        {
                            value = 0;
                            return false;
                        }

                        value = double.Parse(str, CultureInfo.InvariantCulture);
                        return true;
                    }

                    foreach (var (_, sizes) in instanceTypes)
                    foreach (var (size, valueColumns) in sizes)
                        if (filterInstanceType(size))
                            foreach (var (name, prices) in valueColumns)
                                if (OsNameMap.TryGetValue(name, out var os) && filterOperationSystem(os))
                                    if (TryGetCurrencyValue(prices.USD, out var usd))
                                    {
                                        if (!data.TryGetValue(os, out var osValue))
                                            data.Add(os, osValue = new());

                                        if (!osValue.TryGetValue(size, out var sizeValue))
                                            osValue.Add(size, sizeValue = new());

                                        sizeValue.Add(region, usd);
                                    }
                }

            return data;
        }
    }
}
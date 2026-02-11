using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using static AwsPriceParser.Definitions;

namespace AwsPriceParser
{
  public static class SpotJson
  {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    private static class Schema
    {
      public record Root(double vers, Config config);

      public record Config(string[] valueColumns, string[] currencies, Regions[] regions);

      public record Regions(string region, Dictionary<string, string> footnotes, InstanceTypes[] instanceTypes);

      public record InstanceTypes(Sizes[] sizes);

      public record Sizes(string size, ValueColumns[] valueColumns);

      public record ValueColumns(string name, Prices prices);

      public record Prices(string USD);
    }

    private static readonly Dictionary<string, string> ourOsMap = new() { { "mswin", "Windows" }, { "linux", "Linux" }, };

    public static Dictionary<PriceKey, double> Read(
      FileInfo file,
      Predicate<string> filterRegion,
      Predicate<string> filterInstanceType,
      Predicate<string> filterOperationSystem)
    {
      Schema.Root root;
      using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        root = JsonSerializer.Deserialize<Schema.Root>(stream)!;

      if (Math.Abs(root.vers - 0.01) >= Δ)
        throw new FormatException("Invalid version, 0.01 expected");
      var config = root.config;
      if (!config.currencies.Contains(nameof(Schema.Prices.USD)))
        throw new FormatException("USD is not supported");
      if (!ourOsMap.All(x => config.valueColumns.Contains(x.Key)))
        throw new FormatException($"Expected at least following OSes: {string.Join(",", ourOsMap.Keys)}");

      var data = new Dictionary<PriceKey, double>();
      foreach (var (region, footnotes, instanceTypes) in config.regions)
        if (filterRegion(region))
        {
          foreach (var instanceType in instanceTypes)
          foreach (var (size, valueColumns) in instanceType.sizes)
            if (filterInstanceType(size))
              foreach (var (name, prices) in valueColumns)
                if (ourOsMap.TryGetValue(name, out var os) && filterOperationSystem(os) &&
                    TryGetCurrencyStr(footnotes.Keys, prices.USD, out var usdStr))
                {
                  var usd = double.Parse(usdStr, CultureInfo.InvariantCulture);
                  var key = new PriceKey(os, region, size);
                  if (!data.TryGetValue(key, out var prevUsd))
                    data.Add(key, usd);
                  else if (Math.Abs(prevUsd - usd) >= Δ)
                    throw new FormatException($"Duplicate difference prices {usd.ToString(CultureInfo.InvariantCulture)} and {prevUsd.ToString(CultureInfo.InvariantCulture)} for {os},{region},{size}");
                }
        }

      return data;

      static bool TryGetCurrencyStr(IEnumerable<string> footnotes, string str, out string value)
      {
        value = footnotes.Aggregate(str, (current, key) => current.Replace(key, ""));
        return value != "N/A";
      }
    }
  }
}
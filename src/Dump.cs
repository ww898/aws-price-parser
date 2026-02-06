using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static AwsPriceParser.Definitions;

namespace AwsPriceParser
{
  public static class Dump
  {
    public static void WriteMarkdown(TextWriter writer, string title, Dictionary<PriceKey, double> data)
    {
      var operationSystems = new HashSet<string>();
      var regions = new HashSet<string>();
      var instanceTypes = new HashSet<string>();
      foreach (var (priceKey, _) in data)
      {
        operationSystems.Add(priceKey.Os);
        instanceTypes.Add(priceKey.InstanceType);
        regions.Add(priceKey.Region);
      }

      var orderedOperationSystems = operationSystems.OrderBy(x => x).ToList();
      var orderedRegions = regions.OrderBy(x => x).ToList();
      var orderedInstanceTypes = instanceTypes.OrderBy(x => x, AwsEc2InstanceTypeNameComparer).ToList();

      foreach (var operationSystem in orderedOperationSystems)
      {
        writer.WriteLine($"### {title} for {operationSystem}:");
        writer.WriteLine(orderedRegions.Aggregate(new StringBuilder("|Instance type|"), (builder, region) => builder.Append($"{GetRegionName(region) ?? "???"}</br>{region}|")));
        writer.WriteLine(orderedRegions.Aggregate(new StringBuilder("|---|"), (builder, _) => builder.Append(":---:|")));

        foreach (var instanceType in orderedInstanceTypes)
        {
          var minUsd = double.MaxValue;
          var maxUsd = double.MinValue;
          var usds = orderedRegions.Select(region =>
            {
              if (!data.TryGetValue(new(operationSystem, region, instanceType), out var usd))
                return (double?)null;
              minUsd = Math.Min(minUsd, usd);
              maxUsd = Math.Max(maxUsd, usd);
              return usd;
            }).ToList();
          if (usds.Any(x => x != null))
            writer.WriteLine(usds.Aggregate(new StringBuilder($"|{instanceType}|"), (builder, mayBeUsd) =>
              {
                if (mayBeUsd == null)
                  return builder.Append("-|");
                var usd = mayBeUsd.Value;
                var isMin = Math.Abs(minUsd - usd) < Δ;
                var isMax = Math.Abs(maxUsd - usd) < Δ;
                if (isMin)
                  builder.Append("**<span style=\"color:darkgreen;\">");
                else if (isMax)
                  builder.Append("**<span style=\"color:red;\">");
                builder.Append(usd.ToString("F4"));
                if (isMin || isMax)
                  builder.Append("</span>**");
                return builder.Append('|');
              }));
        }
      }
    }
  }
}
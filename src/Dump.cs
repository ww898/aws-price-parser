using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using static AwsPriceParser.Definitions;

namespace AwsPriceParser
{
  public static class Dump
  {
    public static void WriteJson(Utf8JsonWriter writer, Dictionary<string, InstanceTypeInfo> data)
    {
      writer.WriteStartObject();
      foreach (var (instanceType, info) in data.OrderBy(x => x.Key, AwsEc2InstanceTypeNameComparer))
      {
        writer.WriteStartObject(instanceType);
        writer.WriteNumber("vcpu", info.VCpu);
        writer.WriteNumber("memoryInGiB", info.MemoryInGiB);
        if (!string.IsNullOrEmpty(info.StorageType))
          writer.WriteString("storageType", info.StorageType);
        if (info.StorageCount != 0)
          writer.WriteNumber("storageCount", info.StorageCount);
        if (info.StorageSizeInGb != 0)
          writer.WriteNumber("storageSizeInGB", info.StorageSizeInGb);
        if (!string.IsNullOrEmpty(info.NetworkPerformance))
          writer.WriteString("networkPerformance", info.NetworkPerformance);
        writer.WriteString("physicalProcessor", info.PhysicalProcessor);
        if (!string.IsNullOrEmpty(info.ClockSpeed))
          writer.WriteString("clockSpeed", info.ClockSpeed);
        writer.WriteEndObject();
      }

      writer.WriteEndObject();
    }

    public static void WriteJson(Utf8JsonWriter writer, Dictionary<PriceKey, double> data)
    {
      var orderedOperationSystems = data.Select(x => x.Key.Os).Distinct().Order().ToList();
      var orderedRegions = data.Select(x => x.Key.Region).Distinct().Order().ToList();
      var orderedInstanceTypes = data.Select(x => x.Key.InstanceType).Distinct().Order(AwsEc2InstanceTypeNameComparer).ToList();

      writer.WriteStartObject();
      foreach (var operationSystem in orderedOperationSystems)
      {
        writer.WriteStartObject(operationSystem);
        foreach (var region in orderedRegions)
        {
          writer.WriteStartObject(region);
          foreach (var instanceType in orderedInstanceTypes)
          {
            if (data.TryGetValue(new(operationSystem, region, instanceType), out var usd))
              writer.WriteNumber(instanceType, usd);
          }

          writer.WriteEndObject();
        }

        writer.WriteEndObject();
      }

      writer.WriteEndObject();
    }

    public static void WriteMarkdown(TextWriter writer, Dictionary<string, InstanceTypeInfo> data)
    {
      var builder = new StringBuilder(1024);
      builder
        .Append("|Instance type|VCpu|Memory, GiB|Storage type|Storage count|Storage size, GB|NetworkPerformance|PhysicalProcessor|ClockSpeed|").AppendLine()
        .Append("|---|:---:|:---:|:---:|:---:|:---:|:---:|---|:---:|").AppendLine();
      foreach (var (instanceType, info) in data.OrderBy(x => x.Key, AwsEc2InstanceTypeNameComparer))
      {
        builder.Append('|')
          .Append(instanceType).Append('|')
          .Append(info.VCpu).Append('|')
          .Append(info.MemoryInGiB).Append('|');
        if (!string.IsNullOrEmpty(info.StorageType))
          builder.Append(info.StorageType);
        builder.Append('|');
        if (info.StorageCount != 0)
          builder.Append(info.StorageCount);
        builder.Append('|');
        if (info.StorageSizeInGb != 0)
          builder.Append(info.StorageSizeInGb);
        builder.Append('|')
          .Append(info.NetworkPerformance).Append('|')
          .Append(info.PhysicalProcessor).Append('|');
        if (!string.IsNullOrEmpty(info.ClockSpeed))
          builder.Append(info.ClockSpeed);
        builder.Append('|').AppendLine();
      }

      writer.Write(builder);
    }

    public static void WriteMarkdown(TextWriter writer, string title, Dictionary<PriceKey, double> data)
    {
      var orderedOperationSystems = data.Select(x => x.Key.Os).Distinct().Order().ToList();
      var orderedRegions = data.Select(x => x.Key.Region).Distinct().Order().ToList();
      var orderedInstanceTypes = data.Select(x => x.Key.InstanceType).Distinct().Order(AwsEc2InstanceTypeNameComparer).ToList();

      var builder = new StringBuilder(1024);
      foreach (var operationSystem in orderedOperationSystems)
      {
        builder.Length = 0;
        builder
          .Append("### ").Append(title).Append(" for ").Append(operationSystem).Append(':')
          .AppendLine().Append("|Instance type|");
        foreach (var region in orderedRegions)
          builder.Append(GetRegionName(region) ?? "???").Append("</br>").Append(region).Append('|');
        builder.AppendLine().Append("|---|");
        for (var n = orderedRegions.Count; n-- > 0;)
          builder.Append(":---:|");
        builder.AppendLine();

        foreach (var instanceType in orderedInstanceTypes)
        {
          var sparseUsds = orderedRegions.Select(region => data.TryGetValue(new(operationSystem, region, instanceType), out var usd) ? usd : (double?)null).ToList();
          var rangeUsds = sparseUsds.Where(x => x != null).Select(x => x ?? throw new NullReferenceException()).Aggregate(
            new { Min = double.MaxValue, Max = double.MinValue },
            (acc, v) => new { Min = Math.Min(acc.Min, v), Max = Math.Max(acc.Max, v) });
          if (rangeUsds.Min <= rangeUsds.Max)
          {
            builder.Append('|').Append(instanceType).Append('|');
            foreach (var sparseUsd in sparseUsds)
              if (sparseUsd == null)
                builder.Append("-|");
              else
              {
                var usd = sparseUsd.Value;
                var usdStr = usd.ToString("F4");
                if (Math.Abs(rangeUsds.Min - usd) < Δ)
                  builder.Append("**<span style=\"color:darkgreen;\">").Append(usdStr).Append("</span>**|");
                else if (Math.Abs(rangeUsds.Max - usd) < Δ)
                  builder.Append("**<span style=\"color:red;\">").Append(usdStr).Append("</span>**|");
                else
                  builder.Append(usdStr).Append('|');
              }

            builder.AppendLine();
          }
        }

        writer.Write(builder);
      }
    }
  }
}
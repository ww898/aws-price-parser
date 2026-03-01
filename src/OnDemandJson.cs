using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using static AwsPriceParser.Definitions;

namespace AwsPriceParser
{
  public static class OnDemandJson
  {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    private static class Schema
    {
      public record Root(string formatVersion, string offerCode, Dictionary<string, Product> products, Terms terms);

      public record Product(Attributes attributes);

      public record Attributes(
        string tenancy,
        string preInstalledSw,
        string capacitystatus,
        string licenseModel,
        string operatingSystem,
        string regionCode,
        string instanceType,
        string marketoption,
        string vcpu,
        string memory,
        string storage,
        string physicalProcessor,
        string clockSpeed,
        string networkPerformance);

      public record Terms(Dictionary<string, Dictionary<string, Term>> OnDemand);

      public record Term(Dictionary<string, PriceDimension> priceDimensions);

      public record PriceDimension(string unit, PricePerUnit pricePerUnit);

      public record PricePerUnit(string USD);
    }

    private static readonly Regex ourStorageRegex = new(@"^(?>(?'c'\d+)\s[xX]\s)?(?'s'\d+)\s?(?>GB)?\s?(?'t'NVMe\sSSD|SSD|HDD)?$");

    private static readonly Dictionary<string, Os> ourOsMap = new() { { "Windows", Os.Windows }, { "Linux", Os.Linux }, };

    public static (Dictionary<PriceKey, double>, Dictionary<string, InstanceTypeInfo>) Read(
      FileInfo file,
      Predicate<string> filterRegion,
      Predicate<string> filterInstanceType,
      Predicate<Os> filterOs)
    {
      Schema.Root root;
      using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        root = JsonSerializer.Deserialize<Schema.Root>(stream)!;

      if (root.formatVersion != "v1.0")
        throw new FormatException("Invalid version, v1.0 expected");
      if (root.offerCode != "AmazonEC2")
        throw new FormatException("Invalid offer code, AmazonEC2 expected");

      var data = new Dictionary<PriceKey, double>();
      var config = new Dictionary<string, InstanceTypeInfo>();
      var products = root.products;
      foreach (var (productKey, tmp) in root.terms.OnDemand)
        if (products.TryGetValue(productKey, out var product) &&
            product.attributes is
              {
                tenancy: "Shared",
                preInstalledSw: "NA",
                capacitystatus: "Used",
                licenseModel: "No License required",
                marketoption: "OnDemand",
                operatingSystem: var operatingSystem,
                regionCode: var regionCode,
                instanceType: var instanceType,
              } attributes &&
            ourOsMap.TryGetValue(operatingSystem, out var os) && filterOs(os) &&
            filterRegion(regionCode) &&
            filterInstanceType(instanceType))
        {
          if (!config.ContainsKey(instanceType))
          {
            var vCpu = uint.Parse(attributes.vcpu, CultureInfo.InvariantCulture);
            var memoryInGiB = GetMemoryInGiB(attributes.memory);
            var (storageType, storageCount, storageSizeInGb) = GetStorage(attributes.storage, instanceType);
            config.Add(instanceType, new(
              VCpu: vCpu,
              MemoryInGiB: memoryInGiB,
              StorageCount: storageCount,
              StorageSizeInGb: storageSizeInGb,
              StorageType: storageType,
              NetworkPerformance: attributes.networkPerformance,
              PhysicalProcessor: attributes.physicalProcessor,
              ClockSpeed: attributes.clockSpeed));
          }

          foreach (var (_, term) in tmp)
          foreach (var (_, priceDimensions) in term.priceDimensions)
            if (priceDimensions.unit is "Hrs")
            {
              var usd = double.Parse(priceDimensions.pricePerUnit.USD, CultureInfo.InvariantCulture);
              var key = new PriceKey(os, regionCode, instanceType);
              if (!data.TryGetValue(key, out var prevUsd))
                data.Add(key, usd);
              else if (Math.Abs(prevUsd - usd) >= Δ)
                throw new FormatException($"Duplicate difference prices {usd.ToString(CultureInfo.InvariantCulture)} and {prevUsd.ToString(CultureInfo.InvariantCulture)} for {operatingSystem},{regionCode},{instanceType}");
            }
        }

      return new(data, config);

      static (string storageType, uint storageCount, uint storageSizeInGb) GetStorage(string storage, string instanceType)
      {
        if (storage == "EBS only")
          return ("EBS", 0, 0);
        else
        {
          var match = ourStorageRegex.Match(storage);
          if (!match.Success)
            throw new FormatException($"Unexpected storage field format {storage}");
          var countGroup = match.Groups["c"];
          var typeGroup = match.Groups["t"];
          return (
            typeGroup.Success ? typeGroup.Value : RecoveryStorageType(),
            countGroup.Success ? uint.Parse(countGroup.Value, CultureInfo.InvariantCulture) : 1u,
            uint.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture));
        }

        string RecoveryStorageType()
        {
          var v = AwsInstanceType.Parse(instanceType);
          if (v is { Generation: 8, Series: "i", Options: "g" })
            return "AWS Nitro SSD";
          throw new FormatException($"Failed to recovery storage type for {instanceType}");
        }
      }

      static double GetMemoryInGiB(string memory)
      {
        if (!memory.EndsWith(" GiB"))
          throw new FormatException($"Unexpected memory field format {memory}");
        return double.Parse(memory[..^4], CultureInfo.InvariantCulture);
      }

      static (double networkPerformanceInGbit, bool networkPerformanceUpTo, string networkPerformanceType) GetNetworkPerformanceInGbit(string networkPerformance)
      {
        var isUpTo = networkPerformance.StartsWith("Up to ");
        var str = isUpTo ? networkPerformance[6..] : networkPerformance;
        if (str.EndsWith(" Gigabit"))
          return new(double.Parse(str[..^8], CultureInfo.InvariantCulture), isUpTo, "");
        if (str.EndsWith(" Megabit"))
          return new(double.Parse(str[..^8], CultureInfo.InvariantCulture) / 1000, isUpTo, "");
        if (str is "Very High" or "High" or "Moderate" or "Low" or "Very Low" or "Low to Moderate")
          return new(0, false, str);
        throw new FormatException($"Unexpected network performance field format {networkPerformance}");
      }
    }
  }
}
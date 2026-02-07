using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using static AwsPriceParser.Definitions;

namespace AwsPriceParser
{
  public static class OnDemandJson
  {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private static class Schema
    {
      public record Root(
        string formatVersion,
        string offerCode,
        string version,
        Dictionary<string, Product> products,
        Terms terms);

      public record Product(Dictionary<string, string> attributes);

      public record Terms(Dictionary<string, Dictionary<string, Term>> OnDemand);

      public record Term(Dictionary<string, PriceDimension> priceDimensions);

      public record PriceDimension(string unit, PricePerUnit pricePerUnit);

      public record PricePerUnit(string USD);
    }

    public static Dictionary<PriceKey, double> Read(
      FileInfo file,
      Predicate<string> filterRegion,
      Predicate<string> filterInstanceType,
      Predicate<string> filterOperationSystem)
    {
      Schema.Root root;
      using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        root = JsonSerializer.Deserialize<Schema.Root>(stream)!;

      if (root.formatVersion != "v1.0")
        throw new FormatException("Invalid version, v1.0 expected");
      if (root.offerCode != "AmazonEC2")
        throw new FormatException("Invalid offer code, AmazonEC2 expected");

      var data = new Dictionary<PriceKey, double>();
      var products = root.products;
      foreach (var (productKey, payload) in root.terms.OnDemand)
      foreach (var (_, term) in payload)
      foreach (var (_, priceDimensions) in term.priceDimensions)
        if (priceDimensions.unit == "Hrs" && products.TryGetValue(productKey, out var product))
        {
          var attributes = product.attributes;
          if (attributes.TryGetValue("tenancy", out var tenancy) && tenancy == "Shared" &&
              attributes.TryGetValue("preInstalledSw", out var preInstalledSw) && preInstalledSw == "NA" &&
              attributes.TryGetValue("capacitystatus", out var capacitystatus) && capacitystatus == "Used" &&
              attributes.TryGetValue("licenseModel", out var licenseModel) && licenseModel == "No License required" &&
              attributes.TryGetValue("operatingSystem", out var operatingSystem) && filterOperationSystem(operatingSystem) &&
              attributes.TryGetValue("regionCode", out var regionCode) && filterRegion(regionCode) &&
              attributes.TryGetValue("instanceType", out var instanceType) && filterInstanceType(instanceType))
          {
            var usd = double.Parse(priceDimensions.pricePerUnit.USD, CultureInfo.InvariantCulture);
            data.Add(new(operatingSystem, regionCode, instanceType), usd);
          }
        }

      return data;
    }
  }
}
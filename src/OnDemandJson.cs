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
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    private static class Schema
    {
      public record Root(string formatVersion, string offerCode, Dictionary<string, Product> products, Terms terms);

      public record Product(Attributes attributes);

      public record Attributes(string tenancy, string preInstalledSw, string capacitystatus, string licenseModel, string operatingSystem, string regionCode, string instanceType);

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
      foreach (var (productKey, tmp) in root.terms.OnDemand)
        if (products.TryGetValue(productKey, out var product) &&
            product.attributes is
              {
                tenancy: "Shared",
                preInstalledSw: "NA",
                capacitystatus: "Used",
                licenseModel: "No License required",
                operatingSystem: var operatingSystem,
                regionCode: var regionCode,
                instanceType: var instanceType,
              } &&
            filterOperationSystem(operatingSystem) &&
            filterRegion(regionCode) &&
            filterInstanceType(instanceType))
        {
          foreach (var (_, term) in tmp)
          foreach (var (_, priceDimensions) in term.priceDimensions)
            if (priceDimensions.unit is "Hrs")
              data.Add(new(operatingSystem, regionCode, instanceType), double.Parse(priceDimensions.pricePerUnit.USD, CultureInfo.InvariantCulture));
        }

      return data;
    }
  }
}
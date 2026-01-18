using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AwsPriceParser
{
    public static class OnDemandJson
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        private static class Schema
        {
            public record Root(
                string formatVersion,
                string offerCode,
                string version,
                Dictionary<string, Product> products,
                Terms terms);

            public record Product(Attributes attributes);

            public record Attributes(
                string? instanceType,
                string? operatingSystem,
                string? regionCode,
                string? licenseModel,
                string? preInstalledSw,
                string? tenancy);

            public record Terms(Dictionary<string, Dictionary<string, Term>> OnDemand);

            public record Term(Dictionary<string, PriceDimension> priceDimensions);

            public record PriceDimension(
                string unit,
                //string beginRange,
                //string endRange,
                PricePerUnit pricePerUnit);

            public record PricePerUnit(string USD);
        }

        public static Dictionary<string, Dictionary<string, Dictionary<string, double>>> Read(
            FileInfo file,
            Predicate<string> filterRegion,
            Predicate<string> filterInstanceType,
            Predicate<string> filterOperationSystem)
        {
            Schema.Root root;
            using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                root = JsonSerializer.Deserialize<Schema.Root>(stream, new JsonSerializerOptions())!;

            if (root.formatVersion != "v1.0")
                throw new FormatException("Invalid version, v1.0 expected");
            if (root.offerCode != "AmazonEC2")
                throw new FormatException("Invalid offer code, AmazonEC2 expected");

            var data = new Dictionary<string, Dictionary<string, Dictionary<string, Tuple<double, string>>>>();
            foreach (var (productKey, tmp) in root.terms.OnDemand)
            foreach (var (_, term) in tmp)
            foreach (var (_, priceDimensions) in term.priceDimensions)
                if (priceDimensions.unit == "Hrs" && root.products.TryGetValue(productKey, out var product))
                {
                    var attributes = product.attributes;
                    var instanceType = attributes.instanceType;
                    var operatingSystem = attributes.operatingSystem;
                    var regionCode = attributes.regionCode;
                    if (attributes.licenseModel is "No License required" &&
                        attributes.preInstalledSw is "NA" &&
                        attributes.tenancy is "Shared" &&
                        operatingSystem != null && filterOperationSystem(operatingSystem) &&
                        regionCode != null && filterRegion(regionCode) &&
                        instanceType != null && filterInstanceType(instanceType))
                    {
                        if (!data.TryGetValue(operatingSystem, out var operatingSystemValue))
                            data.Add(operatingSystem, operatingSystemValue = new());

                        if (!operatingSystemValue.TryGetValue(instanceType, out var instanceTypeValue))
                            operatingSystemValue.Add(instanceType, instanceTypeValue = new());

                        var usd = double.Parse(priceDimensions.pricePerUnit.USD, CultureInfo.InvariantCulture);
                        instanceTypeValue.TryGetValue(regionCode,  out var prev);
                        instanceTypeValue.Add(regionCode, Tuple.Create(usd, productKey));
                    }
                }

            return data
                .Select(x => KeyValuePair.Create(x.Key, x.Value
                    .Select(y => KeyValuePair.Create(y.Key, y.Value
                        .Select(z => KeyValuePair.Create(z.Key, z.Value.Item1))
                        .ToDictionary()))
                    .ToDictionary()))
                .ToDictionary();
        }
    }
}
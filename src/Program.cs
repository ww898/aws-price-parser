using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using static AwsPriceParser.Definitions;

namespace AwsPriceParser
{
  internal static class Program
  {
    private static bool IsAllowedInstanceType(string size)
    {
      return true;
      var v = AwsInstanceType.Parse(size);
      if (v.Size is not ("large" or "xlarge" or "2xlarge"))
        return false;
      var series = v.Series;
      var generation = v.Generation;
      var options = v.Options;
      return
      // @formatter:off
        (series is "m" or "c" or "r" && generation is 7 or 8 or 9 && AllFlags(options, "gd")) ||
        (series is "i"               && generation is 7 or 8 or 9 && AllFlags(options, "g" )) ||
        (series is "m" or "c" or "r" && generation is 6           && AllFlags(options, "id")) ||
        (series is "m" or "c" or "r" && generation is 5           && AllFlags(options, "d" )) ||
        (series is "i"               && generation is 4           && AllFlags(options, "i" ));
      // @formatter:on

      static bool AllFlags(string str, string flags) => flags.All(str.Contains);
    }

    private static bool IsAllowedRegion(string region) => region is "eu-central-1" or "eu-north-1" or "eu-west-1";

    private static bool IsAllowedOs(Os os) => os is Os.Windows or Os.Linux;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private enum OutputFormat
    {
      markdown,
      json,
    }

    private static int Main(string[] args)
    {
      try
      {
        var outputFormatOption = new Option<OutputFormat>("--output", "-o")
          {
            Description = "Console output format",
            DefaultValueFactory = _ => OutputFormat.json,
            Arity = ArgumentArity.ZeroOrOne,
          };
        var sourceFileArgument = new Argument<FileInfo>("json-file") { Description = "Source JSON-file", Arity = ArgumentArity.ExactlyOne };
        var spotsCommand = new Command("aws-spots") { Description = "Process JSON-file with AWS spot prices", Arguments = { sourceFileArgument } };
        var onDemandsCommand = new Command("aws-on-demands") { Description = "Process JSON-file with AWS on-demand prices", Arguments = { sourceFileArgument } };
        var instanceInfoCommand = new Command("aws-instances") { Description = "Process JSON-file with AWS on-demand prices", Arguments = { sourceFileArgument } };
        var rootCommand = new RootCommand("AWS spots and on-demands price parser") { Subcommands = { spotsCommand, onDemandsCommand, instanceInfoCommand }, Options = { outputFormatOption } };
        spotsCommand.SetAction(result =>
          {
            var filename = result.GetRequiredValue(sourceFileArgument);
            var prices = SpotJson.Read(filename, IsAllowedRegion, IsAllowedInstanceType, IsAllowedOs);
            switch (result.GetValue(outputFormatOption))
            {
              case OutputFormat.markdown:
                Dump.WriteMarkdown(Console.Out, "Spots", DateTime.Now, prices);
                break;
              case OutputFormat.json:
                using (var stream = Console.OpenStandardOutput())
                using (var writer = new Utf8JsonWriter(stream, new() { Indented = true, IndentSize = 2 }))
                  Dump.WriteJson(writer, "spots", DateTime.Now, prices);
                break;
              default:
                throw new ArgumentOutOfRangeException();
            }

            return 0;
          });
        onDemandsCommand.SetAction(result =>
          {
            var filename = result.GetRequiredValue(sourceFileArgument);
            var (prices, _) = OnDemandJson.Read(filename, IsAllowedRegion, IsAllowedInstanceType, IsAllowedOs);
            switch (result.GetValue(outputFormatOption))
            {
              case OutputFormat.markdown:
                Dump.WriteMarkdown(Console.Out, "On-demands", DateTime.Now, prices);
                break;
              case OutputFormat.json:
                using (var stream = Console.OpenStandardOutput())
                using (var writer = new Utf8JsonWriter(stream, new() { Indented = true, IndentSize = 2 }))
                  Dump.WriteJson(writer, "on-demands", DateTime.Now, prices);
                break;
              default:
                throw new ArgumentOutOfRangeException();
            }

            return 0;
          });
        instanceInfoCommand.SetAction(result =>
          {
            var filename = result.GetRequiredValue(sourceFileArgument);
            var (_, instanceTypeInfo) = OnDemandJson.Read(filename, IsAllowedRegion, IsAllowedInstanceType, IsAllowedOs);
            switch (result.GetValue(outputFormatOption))
            {
              case OutputFormat.markdown:
                Dump.WriteMarkdown(Console.Out, instanceTypeInfo);
                break;
              case OutputFormat.json:
                using (var stream = Console.OpenStandardOutput())
                using (var writer = new Utf8JsonWriter(stream, new() { Indented = true, IndentSize = 2 }))
                  Dump.WriteJson(writer, instanceTypeInfo);
                break;
              default:
                throw new ArgumentOutOfRangeException();
            }

            return 0;
          });
        return rootCommand.Parse(args).Invoke();
      }
      catch (Exception e)
      {
        Console.Error.WriteLine(e);
        return 1;
      }
    }
  }
}
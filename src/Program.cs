using System;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace AwsPriceParser
{
  internal static class Program
  {
    private static bool IsAllowedInstanceType(string size)
    {
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

    private static bool IsAllowedOperationSystem(string operationSystem) => operationSystem is "Windows" or "Linux";

    private static int Main(string[] args)
    {
      try
      {
        var argument = new Argument<FileInfo>("json-file") { Arity = ArgumentArity.ExactlyOne };
        var spotsCommand = new Command("aws-spots") { Description = "Process JSON-file with AWS spot prices", Arguments = { argument } };
        var onDemandsCommand = new Command("aws-on-demands") { Description = "Process JSON-file with AWS on-demand prices", Arguments = { argument } };
        var rootCommand = new RootCommand("AWS spots and on-demands price parser") { Subcommands = { spotsCommand, onDemandsCommand } };
        spotsCommand.SetAction(result =>
          {
            var filename = result.GetRequiredValue(argument);
            var spotPrices = SpotJson.Read(filename, IsAllowedRegion, IsAllowedInstanceType, IsAllowedOperationSystem);
            Dump.WriteMarkdown(Console.Out, "Spots", spotPrices);
            return 0;
          });
        onDemandsCommand.SetAction(result =>
          {
            var filename = result.GetRequiredValue(argument);
            var spotPrices = OnDemandJson.Read(filename, IsAllowedRegion, IsAllowedInstanceType, IsAllowedOperationSystem);
            Dump.WriteMarkdown(Console.Out, "On-demands", spotPrices);
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
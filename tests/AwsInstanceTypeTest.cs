using AwsPriceParser;
using NUnit.Framework;

namespace AwsPriceParserTests
{
  [TestFixture]
  public class AwsInstanceTypeTest
  {
    // @formatter:off
    [TestCase("M8g.metal-24xl"    , "m"  , 8u, "g"  , ""       , "metal-24xl")]
    [TestCase("Hpc7g.16xlarge"    , "hpc", 7u, "g"  , ""       , "16xlarge"  )]
    [TestCase("I8g.16xlarge"      , "i"  , 8u, "g"  , ""       , "16xlarge"  )]
    [TestCase("is4Gen.medium"     , "is" , 4u, "gen", ""       , "medium"    )]
    [TestCase("u-12tb1.112xlarge" , "u"  , 0u, ""   , "12tb1"  , "112xlarge" )]
    [TestCase("x1.32xlarge"       , "x"  , 1u, ""   , ""       , "32xlarge"  )]
    [TestCase("u7i-8tb.112xlarge" , "u"  , 7u, "i"  , "8tb"    , "112xlarge" )]
    [TestCase("mac-m4pro.metal"   , "mac", 0u, ""   , "m4pro"  , "metal"     )]
    [TestCase("mac2-m1ultra.metal", "mac", 2u, ""   , "m1ultra", "metal"     )]
    [TestCase("P6-b200.48xlarge"  , "p"  , 6u, ""   , "b200"   , "48xlarge"  )]
    [TestCase("p6e-gb200.36xlarge", "p"  , 6u, "e"  , "gb200"  , "36xlarge"  )]
    [TestCase("c8i-flex.16xlarge" , "c"  , 8u, "i"  , "flex"   , "16xlarge"  )]
    // @formatter:on
    [Test]
    public void Test(string instanceType, string series, uint generation, string options, string parameter, string size)
    {
      var res = AwsInstanceType.Parse(instanceType);
      Assert.Multiple(() =>
        {
          Assert.That(res.Series, Is.EqualTo(series));
          Assert.That(res.Generation, Is.EqualTo(generation));
          Assert.That(res.Options, Is.EqualTo(options));
          Assert.That(res.Parameter, Is.EqualTo(parameter));
          Assert.That(res.Size, Is.EqualTo(size));
          Assert.That(res.ToString(), Is.EqualTo(instanceType.ToLowerInvariant()));
        });
    }
  }
}
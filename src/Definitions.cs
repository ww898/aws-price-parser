using System;
using System.Collections.Generic;

namespace AwsPriceParser
{
  public static class Definitions
  {
    public const double Δ = 1e-12;

    private static readonly Dictionary<string, string> ourRegionNames = new()
      {
        // @formatter:off
        { "af-south-1",           "Africa (Cape Town)"        },
        { "af-south-1-los-1",     "Nigeria (Lagos)"           },
        { "ap-east-1",            "Asia Pacific (Hong Kong)"  },
        { "ap-northeast-1",       "Asia Pacific (Tokyo)"      },
        { "ap-northeast-1-tpe-1", "Taiwan (Taipei)"           },
        { "ap-northeast-2",       "Asia Pacific (Seoul)"      },
        { "ap-northeast-3",       "Asia Pacific (Osaka)"      },
        { "ap-south-1",           "Asia Pacific (Mumbai)"     },
        { "ap-south-1-ccu-1",     "India (Kolkata)"           },
        { "ap-south-1-del-1",     "India (Delhi)"             },
        { "ap-south-2",           "Asia Pacific (Hyderabad)"  },
        { "ap-southeast-1",       "Asia Pacific (Singapore)"  },
        { "ap-southeast-1-bkk-1", "Thailand (Bangkok)"        },
        { "ap-southeast-1-mnl-1", "Philippines (Manila)"      },
        { "ap-southeast-2",       "Asia Pacific (Sydney)"     },
        { "ap-southeast-2-akl-1", "New Zealand (Auckland)"    },
        { "ap-southeast-2-per-1", "Australia (Perth)"         },
        { "ap-southeast-3",       "Asia Pacific (Jakarta)"    },
        { "ap-southeast-4",       "Asia Pacific (Melbourne)"  },
        { "ap-southeast-5",       "Asia Pacific (Malaysia)"   },
        { "ap-southeast-7",       "Asia Pacific (Thailand)"   },
        { "ca-central-1",         "Canada (Central)"          },
        { "ca-west-1",            "Canada West (Calgary)"     },
        { "eu-central-1",         "Europe (Frankfurt)"        },
        { "eu-central-1-ham-1",   "Germany (Hamburg)"         },
        { "eu-central-1-waw-1",   "Poland (Warsaw)"           },
        { "eu-central-2",         "Europe (Zurich)"           },
        { "eu-north-1",           "Europe (Stockholm)"        },
        { "eu-north-1-cph-1",     "Denmark (Copenhagen)"      },
        { "eu-north-1-hel-1",     "Finland (Helsinki)"        },
        { "eu-south-1",           "Europe (Milan)"            },
        { "eu-south-2",           "Europe (Spain)"            },
        { "eu-west-1",            "Europe (Ireland)"          },
        { "eu-west-2",            "Europe (London)"           },
        { "eu-west-3",            "Europe (Paris)"            },
        { "il-central-1",         "Israel (Tel Aviv)"         },
        { "me-central-1",         "Middle East (UAE)"         },
        { "me-south-1",           "Middle East (Bahrain)"     },
        { "me-south-1-mct-1",     "Oman (Muscat)"             },
        { "mx-central-1",         "Mexico (Central)"          },
        { "sa-east-1",            "South America (São Paulo)" },
        { "us-east-1",            "US East (N. Virginia)"     },
        { "us-east-1-atl-1",      "US East (Atlanta)"         },
        { "us-east-1-atl-2",      "US East (Atlanta) 2"       },
        { "us-east-1-bos-1",      "US East (Boston)"          },
        { "us-east-1-bue-1",      "Argentina (Buenos Aires)"  },
        { "us-east-1-chi-1",      "US East (Chicago)"         },
        { "us-east-1-chi-2",      "US East (Chicago) 2"       },
        { "us-east-1-dfw-1",      "US East (Dallas)"          },
        { "us-east-1-dfw-2",      "US East (Dallas) 2"        },
        { "us-east-1-iah-1",      "US East (Houston)"         },
        { "us-east-1-iah-2",      "US East (Houston) 2"       },
        { "us-east-1-lim-1",      "Peru (Lima)"               },
        { "us-east-1-mci-1",      "US East (Kansas City) 2"   },
        { "us-east-1-mia-1",      "US East (Miami)"           },
        { "us-east-1-mia-2",      "US East (Miami) 2"         },
        { "us-east-1-msp-1",      "US East (Minneapolis)"     },
        { "us-east-1-nyc-1",      "US East (New York City)"   },
        { "us-east-1-nyc-2",      "US East (New York City) 2" },
        { "us-east-1-phl-1",      "US East (Philadelphia)"    },
        { "us-east-1-qro-1",      "México (Querétaro)"        },
        { "us-east-1-scl-1",      "Chile (Santiago)"          },
        { "us-east-2",            "US East (Ohio)"            },
        { "us-west-1",            "US West (N. California)"   },
        { "us-west-2",            "US West (Oregon)"          },
        { "us-west-2-den-1",      "US West (Denver)"          },
        { "us-west-2-hnl-1",      "US West (Honolulu)"        },
        { "us-west-2-las-1",      "US West (Las Vegas)"       },
        { "us-west-2-lax-1",      "US West (Los Angeles)"     },
        { "us-west-2-lax-1b",     "US West (Los Angeles)"     },
        { "us-west-2-pdx-1",      "US West (Portland)"        },
        { "us-west-2-phx-1",      "US West (Phoenix)"         },
        { "us-west-2-phx-2",      "US West (Phoenix) 2"       },
        { "us-west-2-sea-1",      "US West (Seattle)"         },
        // @formatter:on
      };

    public static string? GetRegionName(string region) => ourRegionNames.GetValueOrDefault(region);

    public static readonly IComparer<string> AwsEc2InstanceTypeNameComparer = Comparer<string>.Create((x, y) =>
      {
        var xp = AwsInstanceType.Parse(x);
        var yp = AwsInstanceType.Parse(y);
        var res = string.Compare(xp.Size, yp.Size, StringComparison.InvariantCulture);
        if (res != 0)
          return res;
        res = string.Compare(xp.Series, yp.Series, StringComparison.InvariantCulture);
        if (res != 0)
          return res;
        res = xp.Generation.CompareTo(yp.Generation);
        if (res != 0)
          return res;
        res = string.Compare(xp.Options, yp.Options, StringComparison.InvariantCulture);
        if (res != 0)
          return res;
        res = string.Compare(xp.Parameter, yp.Parameter, StringComparison.InvariantCulture);
        if (res != 0)
          return res;
        return 0;
      });

    public record PriceKey(string Os, string Region, string InstanceType);
  }
}
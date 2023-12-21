using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Conventor2;


internal class ConventionExpander {

    private class YamlNode {
        YamlNode? Parent { get; set; } = null;
        List<YamlNode> Children { get; set; } = new List<YamlNode>();

        string? Key { get; set; } = null;
        string? Value { get; set; } = null;

        Dictionary<string, object>? Properties { get; set; } = new Dictionary<string, object>();

    }

    public static void ParseConventions(string path) {
        dynamic parsedYaml;
        using (var stream = new FileStream(path, FileMode.Open)) {
            parsedYaml = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(new StreamReader(stream));
        }

        RootConvention = Convention.FromYamlObject(parsedYaml);
        RootConvention.FullyExpandChildren();
        RootConvention.TrimIllegalSequences();

    }

    public static Convention? RootConvention { get; private set; } = null;

    public static Convention? GetConvention(string biddingSequence) {
        return RootConvention?.GetConvention(biddingSequence.Split("-"));
    }
}
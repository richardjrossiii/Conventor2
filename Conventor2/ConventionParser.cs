using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Helpers;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Conventor2; 

public static class YamlExtensions { 
    public static T? TryGetChild<T>(this YamlMappingNode mappingNode, string key) where T: YamlNode {
        YamlNode? childNode;
        mappingNode.Children.TryGetValue(new YamlScalarNode(key), out childNode);

        return childNode as T;
    }
}

public class ConventionParser {
    private int yamlConventionCount = 0;

    public static Convention ParseConventionsAt(string path, List<Macro> globalMacros) {
        YamlMappingNode? yamlRoot;
        using (var stream = new FileStream(path, FileMode.Open)) {
            DeserializerBuilder builder = new DeserializerBuilder();

            YamlStream yamlStream = new YamlStream();
            yamlStream.Load(new StreamReader(stream));

            yamlRoot = yamlStream.Documents[0].RootNode as YamlMappingNode;
        }

        if (yamlRoot == null) {
            throw new ArgumentNullException("Failed to parse yaml root object");
        }

        var parser = new ConventionParser();

        var root = parser.FromYamlObject(yamlRoot, globalMacros);
        root.FullyExpandChildren();
        root.TrimIllegalSequences();

        return root;
    }

    public static List<Macro> YamlToMacros(YamlMappingNode? yamlNode) {
        if (yamlNode == null) {
            return [];
        }

        List<Macro> macros = [];
        foreach (var (yamlKey, yamlValue) in yamlNode.Children) {
            if (yamlKey is YamlScalarNode yamlKeyScalar && yamlValue is YamlScalarNode yamlValueScalar) {
                if (yamlKeyScalar.Value == null || yamlValueScalar.Value == null) {
                    continue;
                }

                macros.Add(new Macro(yamlKeyScalar.Value, yamlValueScalar.Value));
            }
        }

        return macros;
    }

    public static void ParseSections(string path) {
        YamlMappingNode? yamlRoot;
        using (var stream = new FileStream(path, FileMode.Open)) {
            DeserializerBuilder builder = new DeserializerBuilder();

            YamlStream yamlStream = new YamlStream();
            yamlStream.Load(new StreamReader(stream));

            yamlRoot = yamlStream.Documents[0].RootNode as YamlMappingNode;
        }

        if (yamlRoot is null) {
            return;
        }

        List<Macro> globalMacros = YamlToMacros(yamlRoot.TryGetChild<YamlMappingNode>("define"));

        if (yamlRoot.TryGetChild<YamlSequenceNode>("sections") is { } yamlSections)
            foreach (var yamlSection in yamlSections) {
                if (yamlSection is YamlMappingNode yamlDict) {
                    ConventorConfig.AllSections.Add(new Section {
                        Name = yamlDict.TryGetChild<YamlScalarNode>("name")?.Value ?? "",
                        Description = yamlDict.TryGetChild<YamlScalarNode>("description")?.Value ?? "",
                        RootConvention = ParseConventionsAt(yamlDict.TryGetChild<YamlScalarNode>("path")?.Value ?? "", globalMacros),
                    });
                }
            }
    }

    public Convention FromYamlObject(YamlMappingNode yamlObject, List<Macro>? globalMacros = null, Convention? parent = null, bool impliedPass = false) {
        if (parent == null) {
            parent = new Convention(null, "");
            parent.Macros.AddRange(globalMacros ?? []);
        }

        foreach (var yamlPair in yamlObject.Children) {
            _ = ((yamlPair.Key as YamlScalarNode)?.Value, yamlPair.Value) switch {
                ("define", YamlMappingNode yamlMacros) => processMacros(yamlMacros),
                ("conventions" or "-", YamlMappingNode yamlConventions) => processConventions(yamlConventions, false),
                ("/", YamlMappingNode yamlConventions) => processConventions(yamlConventions, true),
                ("description", _) => 0, // Handled by processConventionSingle
                (string biddingSequence, YamlScalarNode yamlDescription) =>
                    processConventionSingle(
                        [biddingSequence],
                        new YamlMappingNode(new KeyValuePair<YamlNode, YamlNode>(new YamlScalarNode("description"), yamlDescription))
                    ),
                (string biddingSequence, YamlMappingNode yamlDict) => processConventionSingle([biddingSequence], yamlDict),
                _ when yamlPair.Key is YamlSequenceNode biddingSequences && yamlPair.Value is YamlScalarNode yamlDescription => 
                    processConventionSingle(
                        biddingSequences.Cast<YamlScalarNode>().Select(s => s.Value ?? "").ToList(), 
                        new YamlMappingNode(new KeyValuePair<YamlNode, YamlNode>(new YamlScalarNode("description"), yamlDescription))
                    ),
                _ when yamlPair.Key is YamlSequenceNode biddingSequences && yamlPair.Value is YamlMappingNode yamlDict => 
                    processConventionSingle(biddingSequences.Cast<YamlScalarNode>().Select(s => s.Value ?? "").ToList(), yamlDict),
                _ => 0,
            };
        }
        return parent;

        int processMacros(YamlMappingNode? yamlMacros) {
            parent.Macros.AddRange(ConventionParser.YamlToMacros(yamlMacros));

            return 0;
        }

        int processConventions(YamlMappingNode conventions, bool impliedPass) {
            FromYamlObject(conventions, globalMacros, parent, impliedPass);

            return 0;
        }

        int processConventionSingle(List<string> biddingSequences, YamlMappingNode yamlValue) {
            var seqeunces = biddingSequences.Select(seq => seq.Replace("/", "-P-"). Replace(" ", "").TrimStart('-').Split('-').ToList()).ToList();

            if (impliedPass) {
                seqeunces.ForEach(s => s.Insert(0, "P"));
            }

            foreach (var sequence in seqeunces) {
                var child = parent.GetConvention(sequence, true);
                if (child == null) {
                    continue;
                }

                child.Priority = yamlConventionCount++;

                if (yamlValue.TryGetChild<YamlScalarNode>("description") is { } yamlScalarNode) {
                    child.Description = yamlScalarNode.Value;
                }

                FromYamlObject(yamlValue, globalMacros, child, false);
            }

            return 0;
        }
    }
}

using System.Text.RegularExpressions;

namespace Conventor2;
public struct Expander {
    public Regex Regex;
    public string Replacement;
    public Macro[] MacroSubstitutions;

    public string Expand(string rawBid, ref List<Macro> macros) {
       if (Regex.IsMatch(rawBid)) {
            var newBid = Regex.Replace(rawBid, Replacement);
            macros.AddRange(MacroSubstitutions);

            return newBid;
        }

        return rawBid;
    }

    private static readonly Regex majorRegex = new Regex("([1234567])M"); 
    private static readonly Regex minorRegex = new Regex("([1234567])m");
    private static readonly Regex wildcardRegex = new Regex("([1234567])X");
    public static bool IsExpandedBid(string rawBid) {
        if (rawBid.Contains("|")) {
            return false;
        }

        if (majorRegex.IsMatch(rawBid) || minorRegex.IsMatch(rawBid) || wildcardRegex.IsMatch(rawBid)) {
            return false;
        }

        return true;
    }

    private static IEnumerable<Expander[]> AllExpanders_() {
        List<(string, string)> majors = [ ("H", "S"), ("S", "H") ];
        List<(string, string)> minors = [("C", "D"), ("D", "C")];
        string[] wildcards = ["C", "D", "H", "S"];

        foreach (var (major, otherMajor) in majors) {
            foreach (var (minor, otherMinor) in minors) {
                foreach (var wildcard in wildcards) {
                    yield return [
                        new Expander {
                            Regex = majorRegex,
                            Replacement = $"$1{major}",
                            MacroSubstitutions = [
                                new Macro("\\$M", major),
                                new Macro("\\$OM", otherMajor)
                            ],
                        },
                        new Expander {
                            Regex = minorRegex,
                            Replacement = $"$1{minor}",
                            MacroSubstitutions = [
                                new Macro("\\$m", minor),
                                new Macro("\\$Om", otherMinor),
                            ],
                        },
                        new Expander {
                            Regex = wildcardRegex,
                            Replacement = $"$1{wildcard}",
                            MacroSubstitutions = [
                                new Macro("\\$X", wildcard),
                            ],
                        },
                    ];
                }
            }
        }
    }
    public static IReadOnlyList<Expander[]> AllExpanders { get; } = AllExpanders_().ToList();
}

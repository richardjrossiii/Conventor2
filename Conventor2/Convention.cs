using Microsoft.AspNetCore.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Conventor2;

public class Convention {
    private static bool IsExpandedBid(string rawBid) {
        if (rawBid.Contains("|") || rawBid.Contains("?")) {
            return false;
        }

        if (majorRegex.IsMatch(rawBid) || minorRegex.IsMatch(rawBid)) {
            return false;
        }

        return true;
    }

    private static string ParseBid(string rawBid, out string alertTag, out string bidSuit, out int bidLevel, out bool isAlertable, out bool isAnnouncable) {
        if (!IsExpandedBid(rawBid)) {
            alertTag = string.Empty;
            bidSuit = string.Empty;
            bidLevel = -1;
            isAlertable = false;
            isAnnouncable = false;

            return rawBid;
        }

        if (rawBid.Contains("!!")) {
            isAnnouncable = true;
            isAlertable = false;
        } else if (rawBid.Contains('!')) {
            isAnnouncable = false;
            isAlertable = true;
        } else {
            isAnnouncable = false;
            isAlertable = false;
        }
        
        var tagIndex = rawBid.IndexOf('[');
        if (tagIndex == -1) {
            alertTag = "";
        } else {
            alertTag = rawBid.Substring(tagIndex + 1, rawBid.Length - tagIndex - 2);
        }

        bidSuit = rawBid switch {
            _ when rawBid.Contains('C') => "C",
            _ when rawBid.Contains('D') => "D",
            _ when rawBid.Contains('H') => "H",
            _ when rawBid.Contains('S') => "S",
            _ when rawBid.Contains("NT") => "NT",
            _ when rawBid.Contains('P') => "P",
            _ when rawBid.Contains("XX") => "XX",
            _ when rawBid.Contains('X') => "X",
            _ => "",
        };

        if (int.TryParse(new string(rawBid.TakeWhile(char.IsNumber).ToArray()), out bidLevel)) {
            var bid = $"{bidLevel}{bidSuit}";
            if (!string.IsNullOrEmpty(alertTag)) {
                bid += $"[{alertTag}]";
            }

            return bid;
        }
        
        return rawBid;
    }

    private static string ParseBid(string rawBid) {
        return ParseBid(rawBid, out _, out _, out _, out _, out _);
    }

    public Convention? Parent { get; set; }

    public string BiddingSequence {
        get {
            var parentSequence = Parent?.BiddingSequence;
            if (string.IsNullOrEmpty(parentSequence)) {
                return Bid;
            }

            return $"{parentSequence}-{Bid}";
        }
    }

    public Convention(Convention? parent, string bid) {
        Parent = parent;
        RawBid = bid;

        Bid = ParseBid(bid, out var alertTag, out var bidSuit, out var bidLevel, out var isAlertable, out var isAnnouncable);

        AlertTag = alertTag;
        BidSuit = bidSuit;
        BidLevel = bidLevel;
        IsAlertable = isAlertable;
        IsAnnouncable = isAnnouncable;
    }

    public string Bid { get; private init; }

    public string BidSuit { get; private init; }

    public string RawBid { get; private init; }

    public string AlertTag { get; private init; }

    public int BidLevel { get; private init; }

    public HtmlString HumanReadableBid { 
        get {
            var alertSuffix = string.IsNullOrEmpty(AlertTag) ? "" : $" ({AlertTag})";
            var bidString = BidSuit switch {
                "P" => $"P{alertSuffix}",
                "X" => $"X{alertSuffix}",
                "XX" => $"XX{alertSuffix}",
                "C" => $"{BidLevel}<span class=\"club-suit\">C</span>{alertSuffix}",
                "D" => $"{BidLevel}<span class=\"diamond-suit\">D</span>{alertSuffix}",
                "H" => $"{BidLevel}<span class=\"heart-suit\">H</span>{alertSuffix}",
                "S" => $"{BidLevel}<span class=\"spade-suit\">S</span>{alertSuffix}",
                "NT" => $"NT{alertSuffix}",
                _ => Bid,
            };

            if (IsAlertable) {
                bidString = $"<span class=\"alert\">{bidString}</span>";
            }
            if (IsAnnouncable) {
                bidString = $"<span class=\"announce\">{bidString}</span>";
            }

            return new HtmlString(bidString);
        } 
    }

    public bool IsAlertable { get; private init; }   
    public bool IsAnnouncable { get; private init; }   

    public int Priority { get; set; } = 0;
    
    public int SortOrder => BidSuit switch {
        "P" => 0,
        "X" => 1,
        "XX" => 2,
        "C" => (BidLevel * 10) + 1,
        "D" => (BidLevel * 10) + 2,
        "H" => (BidLevel * 10) + 3,
        "S" => (BidLevel * 10) + 4,
        "NT" => (BidLevel * 10) + 5,
        _ => -1,
    };


    public string? Description { get; set; } = null;

    private HtmlString? humanReadableDescription_ = null;

    public HtmlString? HumanReadableDescription {
        get {
            if (humanReadableDescription_ != null) {
                return humanReadableDescription_;
            }

            if (Description == null) {
                return null;
            }

            // apply macros 'inside-out'
            var tempDescription = Description;

            var current = this;
            int depth = 0;
            while (current != null) {
                foreach (var macro in current.Macros) {
                    tempDescription = macro.Apply(tempDescription);
                }

                tempDescription = tempDescription.Replace($"${depth}", current.BidSuit);

                current = current.Parent;
                depth++;
            }

            humanReadableDescription_ = new HtmlString(tempDescription);
            return humanReadableDescription_;
        }
    }

    public Dictionary<string, Convention> Children { get; set; } = new Dictionary<string, Convention>();

    public List<Macro> Macros { get; set; } = new List<Macro>();

    private void MergeTo(Convention other) {
        if (!string.IsNullOrEmpty(Description)) {
            // Later bids in the document always override previously seen ones.
            if (this.Priority > other.Priority || string.IsNullOrEmpty(other.Description)) {
                other.Description = Description;
                other.Priority = Priority;
            } else {
                // debug
                Debug.Print("test");
            }
        }

        foreach (var macro in Macros) {
            // Add instead of insert - expanded macros always take lower precedence than anything already defined.
            other.Macros.Add(macro); 
        }

        foreach (var (key, oldChild) in Children) {
            var newChild = other.GetConvention([oldChild.RawBid], true);
            if (newChild == null) {
                continue;
            }

            Children[key].MergeTo(newChild);
        }
    }

    public Convention? GetConvention(IEnumerable<string> biddingSequence, bool create = false) {
        var rawBid = biddingSequence.FirstOrDefault();
        var parsedBid = ParseBid(rawBid ?? "");
        if (string.IsNullOrEmpty(rawBid)) {
            return this;
        }

        if (!Children.ContainsKey(parsedBid)) {
            if (!create) {
                return null;
            }

            Children[parsedBid] = new Convention(this, rawBid);
        }

        return Children[parsedBid].GetConvention(biddingSequence.Skip(1), create);
    }


    private static Regex majorRegex = new Regex("([1234567])M"); 
    private static Regex minorRegex = new Regex("([1234567])m");

    private struct ExpansionState {
        public string ExpandedBid { get; set; }

        public List<Macro> DefinedMacros { get; set; }
        public int MajorState { get; set; }
        public int MinorState {  get; set; }

        public bool ParentLevel { get; set; }
        
        public static List<Macro> heartsMacros = [ new Macro("\\$M", "H"), new Macro("\\$OM", "S") ];
        public static List<Macro> spadesMacros = [ new Macro("\\$M", "S"), new Macro("\\$OM", "H") ];
        public static List<Macro> clubsMacros = [ new Macro("\\$m", "C"), new Macro("\\$Om", "D") ];
        public static List<Macro> diamondsMacros = [ new Macro("\\$m", "D"), new Macro("\\$Om", "C") ];
    }

    public void FullyExpandChildren() {
        ExpandChildren_(new ExpansionState { MajorState = 0, MinorState = 0 });
    }

    private void ExpandChildren_(ExpansionState expansionState) {
        // At this point, we need to to handle 'or' bids, as well as Major/Minor expansions.
        // The root object (i.e. this) never needs to do any expanding work - so we can do this by
        foreach (var (key, oldChild) in Children.ToList()) {
            List<ExpansionState> expansions = oldChild.RawBid.Split("|").SelectMany<string, ExpansionState>(b => {
                if (majorRegex.IsMatch(b)) {
                    return expansionState.MajorState switch {
                        0 => [
                            new ExpansionState { 
                                ExpandedBid=majorRegex.Replace(b, "$1H"), 
                                DefinedMacros = [ ..ExpansionState.heartsMacros ],
                                MajorState=1,
                                MinorState=expansionState.MinorState,
                            },
                            new ExpansionState {
                                ExpandedBid=majorRegex.Replace(b, "$1S"), 
                                DefinedMacros = [ ..ExpansionState.spadesMacros ],
                                MajorState=2,
                                MinorState=expansionState.MinorState,
                            },
                        ],
                        1 => [
                            new ExpansionState { 
                                ExpandedBid=majorRegex.Replace(b, "$1H"), 
                                DefinedMacros = [ ..ExpansionState.heartsMacros ],
                                MajorState=1,
                                MinorState=expansionState.MinorState,
                            },
                        ],
                        2 => [
                            new ExpansionState {
                                ExpandedBid=majorRegex.Replace(b, "$1S"), 
                                DefinedMacros = [ ..ExpansionState.spadesMacros ],
                                MajorState=2,
                                MinorState=expansionState.MinorState,
                            },
                        ],
                        _ => [],
                    };
                }

                return [new ExpansionState {
                    ExpandedBid=b, 
                    MajorState=expansionState.MajorState, 
                    MinorState=expansionState.MinorState, 
                }];
            }).SelectMany<ExpansionState, ExpansionState>(e => {
                if (minorRegex.IsMatch(e.ExpandedBid)) {
                    return expansionState.MinorState switch {
                        0 => [
                            new ExpansionState {
                                ExpandedBid = minorRegex.Replace(e.ExpandedBid, "$1C"),
                                DefinedMacros = [ ..e.DefinedMacros ?? [], .. ExpansionState.clubsMacros],
                                MajorState=e.MajorState,
                                MinorState=1,
                            },
                            new ExpansionState {
                                ExpandedBid = minorRegex.Replace(e.ExpandedBid, "$1D"),
                                DefinedMacros = [ .. e.DefinedMacros ?? [], .. ExpansionState.diamondsMacros ],
                                MajorState=e.MajorState,
                                MinorState=2,
                            },
                        ],
                        1 => [
                            new ExpansionState {
                                ExpandedBid = minorRegex.Replace(e.ExpandedBid, "$1C"),
                                DefinedMacros = [ .. e.DefinedMacros ?? [], .. ExpansionState.clubsMacros ],
                                MajorState=e.MajorState,
                                MinorState=1,
                            },
                        ],
                        2 => [
                            new ExpansionState {
                                ExpandedBid = minorRegex.Replace(e.ExpandedBid, "$1D"),
                                DefinedMacros = [ .. e.DefinedMacros ?? [], .. ExpansionState.diamondsMacros ],
                                MajorState=e.MajorState,
                                MinorState=2,
                            },
                        ],
                        _ => [],
                    }; ;
                }

                return [e];
            }).ToList();
            
            Children.Remove(key);

            foreach (var expansion in expansions) {
                var newChild = GetConvention([expansion.ExpandedBid], true);
                if (newChild == null) {
                    continue;
                }

                oldChild.MergeTo(newChild);

                if (expansion.DefinedMacros != null) {
                    foreach (var macro in expansion.DefinedMacros) {
                        var parent = newChild;
                        var shouldAdd = true;
                        while (parent != null) {
                            if (parent.Macros.Contains(macro)) {
                                shouldAdd = false;
                                break;
                            }

                            parent = parent.Parent;
                        }

                        if (shouldAdd) {
                            newChild.Macros.Insert(0, macro);
                        }
                    }
                }

                newChild.ExpandChildren_(expansion);
            }
        }
    }

    public void TrimIllegalSequences() {
        // TODO
    }

    private static int yamlConventionCount = 0;

    public static Convention FromYamlObject(IDictionary yamlObject, Convention? parent = null, bool impliedPass = false) {
        if (parent == null) {
            parent = new Convention(null, "");
        }

        List<List<string>> lastSequences = [];
        foreach (var yamlPair in yamlObject) {
            if (yamlPair is not DictionaryEntry) {
                continue;
            }

            var yamlEntry = (DictionaryEntry)yamlPair;
            _ = yamlEntry switch {
                { Key: "define", Value: IDictionary yamlValue } => processMacros(yamlValue),
                { Key: "conventions" or ".", Value: IList yamlConventions } => processConventions(yamlConventions, false),
                { Key: "/", Value: IList yamlConventions } => processConventions(yamlConventions, true),
                { Key: string biddingSequence, Value: string yamlDescription } => processConventionSingle(biddingSequence, new Dictionary<string, object> {
                    ["description"] = yamlDescription, 
                }),
                { Key: string biddingSequence, Value: IDictionary yamlDict } => processConventionSingle(biddingSequence, yamlDict),
                _ => 0,
            };
        }
        return parent;

        int processMacros(IDictionary yamlMacros) {
            foreach (var yamlKey in yamlMacros.Keys) {
                if (yamlKey == null) {
                    continue;
                }

                parent.Macros.Add(new Macro((string)yamlKey, (string)yamlMacros[yamlKey]));
            }
            
            return 0;
        }

        int processConventions(IList conventions, bool impliedPass) {
            foreach (var convention in conventions) {
                if (convention is IDictionary yamlConvention) {
                    if (lastSequences.Count == 0) {
                        FromYamlObject(yamlConvention, parent, impliedPass);    
                    }

                    foreach (var sequence in lastSequences) {
                        FromYamlObject(yamlConvention, parent.GetConvention(sequence, true) ?? parent, impliedPass);    
                    }
                }
            }
            
            return 0;
        }

        int processConventionSingle(string biddingSequence, IDictionary yamlValue) {
            lastSequences = biddingSequence
                .Split(',')
                .Select(
                    seq => seq
                        .Replace("/", "-P-")
                        .Replace(" ", "")
                        .Split('-').ToList()
                ).ToList();

            if (impliedPass) {
                lastSequences.ForEach(s => s.Insert(0, "P"));
            }

            foreach (var sequence in lastSequences) {
                var child = parent.GetConvention(sequence, true);
                if (child == null) {
                    continue;
                }

                child.Priority = yamlConventionCount++;

                if (yamlValue.Contains("description")) {
                    child.Description = yamlValue["description"] as string;
                }
            }

            return 0;
        }
    }
}

using HeyRed.MarkdownSharp;
using Microsoft.AspNetCore.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Conventor2;

public class Convention {
    public Convention? Parent { get; set; }

    public string BiddingSequence {
        get {
            var parentSequence = Parent?.BiddingSequence;
            if (string.IsNullOrEmpty(parentSequence)) {
                return BidString;
            }

            return $"{parentSequence}-{BidString}";
        }
    }

    public Convention(Convention? parent, string bid) {
        Parent = parent;
        RawBid = bid;

        BidString = BidParser.ParseBid(bid, out var alertTag, out var bidSuit, out var bidLevel, out var isAlertable, out var isAnnouncable);

        AlertTag = alertTag;
        BidSuit = bidSuit;
        BidLevel = bidLevel;
        IsAlertable = isAlertable;
        IsAnnouncable = isAnnouncable;
    }

    public string BidString { get; private init; }

    public string BidSuit { get; private init; }

    public string RawBid { get; private init; }

    public string AlertTag { get; private init; }

    public int BidLevel { get; private init; }

    public Bid Bid {
        get {
            return (BidLevel, BidSuit) switch {
                (_, "P") => Bid.Pass,
                (_, "X") => Bid.Double,
                (_, "XX") => Bid.Redouble,
                (var level, "C") => new Bid(level, ContractStrain.Clubs),
                (var level, "D") => new Bid(level, ContractStrain.Diamonds),
                (var level, "H") => new Bid(level, ContractStrain.Hearts),
                (var level, "S") => new Bid(level, ContractStrain.Spades),
                (var level, "NT") => new Bid(level, ContractStrain.NoTrump),
                _ => Bid.Pass,
            };
        }
    }

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
                "NT" => $"{BidLevel}NT{alertSuffix}",
                _ => BidString,
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

    public bool IsAlertable { get; private set; }   
    public bool IsAnnouncable { get; private set; }   

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

            var m = new Markdown(new MarkdownOptions { AutoNewLines = true, });
            tempDescription = m.Transform(tempDescription.Trim());

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
                other.IsAlertable = IsAlertable;
                other.IsAnnouncable = IsAnnouncable;
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
        var parsedBid = BidParser.ParseBid(rawBid ?? "");
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

    public void FullyExpandChildren() {
        ExpandStateless_();

        List<Convention> statefulExpansions = Expander.AllExpanders.Select(expanders => Expanded(expanders)).Where(c => c != null).ToList();

        Children.Clear();

        foreach (var expansion in statefulExpansions) {
            expansion.MergeTo(this);
        }
    }

    private Convention Expanded(Expander[] expanders) {
        var newBid = RawBid;
        var addedMacros = new List<Macro>();
        foreach (var expander in expanders) {
            newBid = expander.Expand(newBid, ref addedMacros);
        }

        Convention copy = new Convention(null, newBid);
        copy.Description = Description;
        copy.Priority = Priority;
        copy.Macros.AddRange(Macros);
        copy.Macros.AddRange(addedMacros);

        foreach (var (key, child) in Children) {
            var expandedChild = child.Expanded(expanders);

            expandedChild.MergeTo(copy.GetConvention([expandedChild.BidString], true)!);
        }

        return copy;
    }

    private void ExpandStateless_() {
        foreach (var (key, oldChild) in Children.ToList()) {
            List<string> expansions = oldChild.RawBid.Split("|").ToList();
            
            Children.Remove(key);

            foreach (var expansion in expansions) {
                var newChild = GetConvention([expansion], true);
                if (newChild == null) {
                    continue;
                }

                oldChild.MergeTo(newChild);
                newChild.ExpandStateless_();
            }
        }
    }

    public void TrimIllegalSequences() {
        foreach (var childKey in Children.Keys.ToList()) {
            var child = Children[childKey];
            child.TrimIllegalSequences();

            var temp = child;
            var biddingSequence = new List<Bid>();
            while (temp.Parent != null) {
                biddingSequence.Insert(0, temp.Bid);
                temp = temp.Parent;
            }

            if (!biddingSequence.IsLegalBiddingSequence()) {
                Children.Remove(childKey);        
            } else if (string.IsNullOrEmpty(child.Description) && child.Children.Count == 0) {
                Children.Remove(childKey);
            }
        }
    }
}

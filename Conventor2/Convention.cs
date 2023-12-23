using HeyRed.MarkdownSharp;
using Microsoft.AspNetCore.Html;

namespace Conventor2;

public class Convention {
    public Convention? Parent { get; set; }

    public List<BidType> BiddingSequence {
        get {
            List<BidType> sequence = [];

            var cur = this;
            while (cur != null) {
                if (cur.Bid != null) {
                    sequence.Insert(0, cur.Bid ?? BidType.Pass);
                }
                cur = cur.Parent;
            }

            return sequence;
        }
    }

    public Convention(Convention? parent, BidType bid) {
        Parent = parent;
        RawBid = bid.ToString();
        Bid = bid;

        AlertTag = null;
        IsAlertable = false;
        IsAnnouncable = false;
    }

    public Convention(Convention? parent, string bid) {
        Parent = parent;
        RawBid = bid;

        Bid = BidParser.ParseBid(bid, out var alertTag, out var isAlertable, out var isAnnouncable);

        AlertTag = alertTag;
        IsAlertable = isAlertable;
        IsAnnouncable = isAnnouncable;
    }

    public string RawBid { get; private init; }

    public string? AlertTag { get; private init; }

    public BidType? Bid { get; private init;  }

    // TODO: Move into bid.cs?
    public HtmlString HumanReadableBid { 
        get {
            if (this.Bid == null) {
                return new HtmlString("NULLBID");
            }

            var Bid = this.Bid ?? BidType.Pass;
            var alertSuffix = string.IsNullOrEmpty(AlertTag) ? "" : $" ({AlertTag})";
            var bidString = Bid.Strain switch {
                ContractStrain.None when Bid.IsPass => $"P{alertSuffix}",
                ContractStrain.None when Bid.IsDouble => $"X{alertSuffix}",
                ContractStrain.None when Bid.IsRedouble => $"XX{alertSuffix}",
                ContractStrain.Clubs => $"{Bid.Level}<span class=\"club-suit\">C</span>{alertSuffix}",
                ContractStrain.Diamonds => $"{Bid.Level}<span class=\"diamond-suit\">D</span>{alertSuffix}",
                ContractStrain.Hearts => $"{Bid.Level}<span class=\"heart-suit\">H</span>{alertSuffix}",
                ContractStrain.Spades => $"{Bid.Level}<span class=\"spade-suit\">S</span>{alertSuffix}",
                ContractStrain.NoTrump => $"{Bid.Level}NT{alertSuffix}",
                _ => Bid.UnexpandedBid,
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

                tempDescription = tempDescription.Replace($"${depth}", current.Bid?.Strain.ToShortString());

                current = current.Parent;
                depth++;
            }

            var m = new Markdown(new MarkdownOptions { AutoNewLines = true, });
            tempDescription = m.Transform(tempDescription.Trim());

            humanReadableDescription_ = new HtmlString(tempDescription);
            return humanReadableDescription_;
        }
    }

    public Dictionary<BidType, Convention> Children { get; set; } = [];

    public List<Convention> Steps { get; set; } = [];

    public List<Macro> Macros { get; set; } = [];

    private void MergeTo(Convention other) {
        if (!string.IsNullOrEmpty(Description)) {
            // Later bids in the document always override previously seen ones.
            if (this.Priority > other.Priority || string.IsNullOrEmpty(other.Description)) {
                other.Description = Description;
                other.Priority = Priority;
                other.IsAlertable = IsAlertable;
                other.IsAnnouncable = IsAnnouncable;
            }
        }

        if (Steps.Count != 0) {
            if (this.Priority > other.Priority || other.Steps.Count == 0) {
                other.Steps = new(Steps);
            }
        }

        foreach (var macro in Macros) {
            // Add instead of insert - expanded macros always take lower precedence than anything already defined.
            if (other.Macros.Contains(macro)) {
                continue;
            }

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
        if (!biddingSequence.Any()) {
            return this;
        }

        var rawBid = biddingSequence.First();
        var parsedBid = BidParser.ParseBid(rawBid);
        if (parsedBid == null) {
            return this;
        }

        if (!Children.ContainsKey(parsedBid.GetValueOrDefault())) {
            if (!create) {
                return null;
            }

            Children[parsedBid.GetValueOrDefault()] = new Convention(this, rawBid);
        }

        return Children[parsedBid.GetValueOrDefault()].GetConvention(biddingSequence.Skip(1), create);
    }

    public Convention? GetConvention(IEnumerable<BidType> biddingSequence, bool create = false) {
        if (!biddingSequence.Any()) {
            return this;
        }

        var bid = biddingSequence.First();
        if (!Children.ContainsKey(bid)) {
            if (!create) {
                return null;
            }

            Children[bid] = new Convention(this, bid);
        }

        return Children[bid].GetConvention(biddingSequence.Skip(1), create);
    }

    public void FullyExpandChildren() {
        ExpandStateless_();

        List<Convention> statefulExpansions = Expander.AllExpanders.Select(expanders => Expanded(expanders)).Where(c => c != null).ToList();

        Children.Clear();

        foreach (var expansion in statefulExpansions) {
            expansion.MergeTo(this);
        }

        ExpandSteps_();
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

        foreach (var addedMacro in addedMacros) {
            if (copy.Macros.Contains(addedMacro)) {
                continue;
            }

            copy.Macros.Add(addedMacro);
        }

        foreach (var (key, child) in Children) {
            var expandedChild = child.Expanded(expanders);

            expandedChild.MergeTo(copy.GetConvention([expandedChild.RawBid], true)!);
        }

        foreach (var step in Steps) {
            copy.Steps.Add(step.Expanded(expanders));
        }

        return copy;
    }

    private void ExpandStateless_() {
        foreach (var (key, oldChild) in Children.ToList()) {
            List<string> expansions = oldChild.RawBid.Split("|").ToList();
            
            Children.Remove(key);

            foreach (var expansion in expansions) {
                if (string.IsNullOrEmpty(expansion)) {
                    continue;
                }

                var newChild = GetConvention([expansion], true);
                if (newChild == null) {
                    continue;
                }

                oldChild.MergeTo(newChild);
                newChild.ExpandStateless_();
            }
        }

        foreach (var step in Steps) {
            step.ExpandStateless_();
        }
    }

    private void ExpandSteps_() {
        var currentBid = BiddingSequence.LastOrDefault(b => !b.IsPass || b.IsDouble || b.IsRedouble);

        foreach (var step in Steps) {
            currentBid = currentBid.NextStep();

            var child = GetConvention([currentBid.ToString()], true);
            if (child == null) { 
                continue; 
            }


            step.MergeTo(child);
            
            // Assume all relay sequences are alertable.
            child.IsAlertable = true;
        }

        Steps.Clear();

        foreach (var (_, child) in Children) {
            child.ExpandSteps_();
        }
    }

    public void TrimIllegalSequences() {
        foreach (var childKey in Children.Keys.ToList()) {
            var child = Children[childKey];
            child.TrimIllegalSequences();

            if (!child.BiddingSequence.IsLegalBiddingSequence()) {
                Children.Remove(childKey);        
            } else if (string.IsNullOrEmpty(child.Description) && child.Children.Count == 0) {
                Children.Remove(childKey);
            }
        }
    }
}

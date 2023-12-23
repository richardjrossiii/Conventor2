using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2; 

public class BidParser {
    public static BidType? ParseBid(string rawBid, out string? alertTag, out bool isAlertable, out bool isAnnouncable) {
        if (!Expander.IsExpandedBid(rawBid)) {
            alertTag = null;
            isAlertable = false;
            isAnnouncable = false;

            return BidType.Unexpanded(rawBid);
        }

        if (string.IsNullOrEmpty(rawBid)) {
            alertTag = null;
            isAlertable = false;
            isAnnouncable = false;

            return null;
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
            alertTag = null;
        } else {
            alertTag = rawBid.Substring(tagIndex + 1, rawBid.Length - tagIndex - 2);
        }

        var bidStrain = rawBid switch {
            _ when rawBid.Contains('C') => ContractStrain.Clubs,
            _ when rawBid.Contains('D') => ContractStrain.Diamonds,
            _ when rawBid.Contains('H') => ContractStrain.Hearts,
            _ when rawBid.Contains('S') => ContractStrain.Spades,
            _ when rawBid.Contains("NT") => ContractStrain.NoTrump,
            _ => ContractStrain.None,
        };

        var alertSuffix = string.IsNullOrEmpty(alertTag) ? "" : $"[{alertTag}]";
        if (int.TryParse(new string(rawBid.TakeWhile(char.IsNumber).ToArray()), out var bidLevel)) {
            return new BidType(bidLevel, bidStrain) { AlertTag = alertTag };
        }

        if (rawBid.StartsWith("XX")) {
            return BidType.Redouble with { AlertTag = alertTag };
        }

        if (rawBid.StartsWith("X")) {
            return BidType.Double with { AlertTag = alertTag };
        }

        if (rawBid.StartsWith("P")) {
            return BidType.Pass with { AlertTag = alertTag };
        }
        
        return BidType.Pass;
    }

    public static BidType? ParseBid(string rawBid) {
        return ParseBid(rawBid, out _, out _, out _);
    }
}

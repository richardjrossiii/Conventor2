using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2; 

public class BidParser {
    public static string ParseBid(string rawBid, out string alertTag, out string bidSuit, out int bidLevel, out bool isAlertable, out bool isAnnouncable) {
        if (!Expander.IsExpandedBid(rawBid)) {
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

        var alertSuffix = string.IsNullOrEmpty(alertTag) ? "" : $"[{alertTag}]";
        if (int.TryParse(new string(rawBid.TakeWhile(char.IsNumber).ToArray()), out bidLevel)) {
            return $"{bidLevel}{bidSuit}{alertSuffix}";
        }

        if (bidSuit == "X") {
            return $"X{alertTag}";
        }

        if (bidSuit == "XX") {
            return $"XX{alertTag}";
        }
        
        return rawBid;
    }

    public static string ParseBid(string rawBid) {
        return ParseBid(rawBid, out _, out _, out _, out _, out _);
    }
}

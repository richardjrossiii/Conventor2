using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2;

internal class ConventorConfig {
    public static List<Section> AllSections { get; set; } = [];

    public static Convention? GetConvention(string section, List<BidType> biddingSequence) {
        return AllSections.Where(s => s.Name == section).FirstOrDefault()?.RootConvention?.GetConvention(biddingSequence);
    }
}

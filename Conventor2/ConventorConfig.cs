using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2;

internal class ConventorConfig {
    public static ConventorConfig GlobalConfig { get; set; } = default!;

    public List<ConventionExpander> HTMLMacros { get; set; } = [];

    public Dictionary<string, string> Conventions { get; set; } = []; 
}

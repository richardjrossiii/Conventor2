using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2;

public class Section {
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Convention? RootConvention { get; set; } = null;
}

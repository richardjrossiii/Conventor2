﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Conventor2 {
    public class Macro : IEquatable<Macro> {
        public Regex Regex { get; set; }
        public string Replace { get; set; }

        public Macro(string find, string replace) { 
            Regex = new Regex(find, RegexOptions.Compiled);
            Replace = replace;
        }

        public string Apply(string input) {
            return Regex.Replace(input, Replace);
        }

        public bool Equals(Macro? other) {
            if (other == null) return false;

            return Regex.Equals(other.Regex) && Replace.Equals(other.Replace); 
        }
    }
}

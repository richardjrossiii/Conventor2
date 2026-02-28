using HeyRed.MarkdownSharp;
using Microsoft.AspNetCore.Html;
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


    private HtmlString? humanReadableName_ = null;
    public HtmlString? HumanReadableName {
        get {
            if (humanReadableName_ != null) {
                return humanReadableName_;
            }

            if (Name == null) {
                return null;
            }

            // apply macros
            var tempName = Name;

            foreach (var macro in RootConvention?.Macros ?? []) {
                tempName = macro.Apply(tempName);
            }

            var m = new Markdown(new MarkdownOptions { AutoNewLines = true, });
            tempName = m.Transform(tempName.Trim());

            humanReadableName_ = new HtmlString(tempName);
            return humanReadableName_;
        }
    }

    private HtmlString? humanReadableDescription_ = null;
    public HtmlString? HumanReadableDescription {
        get {
            if (humanReadableDescription_ != null) {
                return humanReadableDescription_;
            }

            if (Description == null) {
                return null;
            }

            // apply macros
            var tempDescription = Description;

            foreach (var macro in RootConvention?.Macros ?? []) {
                tempDescription = macro.Apply(tempDescription);
            }

            var m = new Markdown(new MarkdownOptions { AutoNewLines = true, });
            tempDescription = m.Transform(tempDescription.Trim());

            humanReadableDescription_ = new HtmlString(tempDescription);
            return humanReadableDescription_;
        }
    }
}

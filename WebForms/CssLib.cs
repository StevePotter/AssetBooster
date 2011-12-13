using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using AssetBooster;

namespace AssetBooster.WebForms
{
    [DefaultProperty("Text")]
    [ToolboxData("<{0}:CssLib runat=server Name=\"\"></{0}:CssLib>")]
    public class CssLib : Control
    {
        [Description("The name of the css library from web.config.  Example 'skin.css'")]
        [Category("General")]
        [DefaultValue("")]
        public string Name
        {
            get
            {
                return ViewState["Name"].CastTo<string>().CharsOrEmpty();
            }

            set
            {
                ViewState["Name"] = value;
            }
        }


        protected override void Render(HtmlTextWriter writer)
        {
            var html = AssetHelper.AssetLib(this, Name, "css", (src) => "<link href=\"" + src + "\" rel=\"stylesheet\" type=\"text/css\" />");
            writer.Write(html);
        }
    }
}

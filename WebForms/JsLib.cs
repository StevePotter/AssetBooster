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
    [ToolboxData("<{0}:JsLib runat=server Name=\"\"></{0}:JsLib>")]
    public class JsLib : Control
    {

        [Description("The name of the js library from web.config.  Example 'main.js'")]
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
            var html = AssetHelper.AssetLib(this, Name, "js", (src) => "<script src=\"" + src + "\" language=\"javascript\" type=\"text/javascript\"></script>");
            writer.Write(html);
        }
    }
}

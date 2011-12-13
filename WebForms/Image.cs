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
    [ToolboxData("<{0}:Image runat=server ImageUrl=\"\\images\\\"></{0}:Image>")]
    public class Image : System.Web.UI.WebControls.Image
    {

        public override string ImageUrl
        {
            get
            {
                if (!DesignMode && !AssetHelper.UseLocalFiles)
                {
                    return AssetHelper.UrlOnCdn(base.ImageUrl, this.Context.Request);
                }
                return base.ImageUrl;
            }
            set
            {
                base.ImageUrl = value;
            }
        }

    }
}

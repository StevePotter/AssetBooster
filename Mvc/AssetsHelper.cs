using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using AssetBooster.Configuration;

namespace System.Web.Mvc
{
    /// <summary>
    /// Helps to include assets that are deployed locally or on a CDN (where they are minified, compressed, and even forever cached).  Part of AssetBooster library.
    /// </summary>
    public static class AssetHelper
    {
        static AssetHelper()
        {
            var config = AssetBoosterConfigurationSection.Current;
            var cdnFolder = config.Version;
            if (config.CdnSubDirectory.HasChars())
            {
                cdnFolder = config.CdnSubDirectory.SeperatedBy("/", cdnFolder);
            }
            CdnHttpsUrlPrefix = config.CdnHttpsUrlPrefix.CharsOr("https:" + config.CdnUrlPrefix.After(":")).SeperatedBy("/", cdnFolder).EndWith("/");
            CdnHttpUrlPrefix = config.CdnUrlPrefix.SeperatedBy("/", cdnFolder).EndWith("/");
            DebugKey = config.DebugKey;
            UseLocalFiles = config.Local;
        }

        public static bool UseLocalFiles { get; private set; }
        static string CdnHttpUrlPrefix;
        static string CdnHttpsUrlPrefix;
        static string DebugKey;

        public static string Asset(this UrlHelper url, string relativeUrl)
        {
            return Asset(url, relativeUrl, false, false);
        }

        public static string Asset(this UrlHelper url, string relativeUrl, bool forceCdnUrl, bool forceSecureCdnUrl)
        {
            if (UseLocalFiles && !forceCdnUrl)
            {
                var toResolve = relativeUrl.StartsWith("~/") ? relativeUrl : "~" + relativeUrl.StartWith("/");
                return url.Content(toResolve);
            }
            else
            {
                var prefix = forceSecureCdnUrl ? CdnHttpsUrlPrefix : CdnUrlPrefix(url.RequestContext.HttpContext.Request);
                return prefix.SeperatedBy("/", relativeUrl);
            }
        }

        public static MvcHtmlString JsLib(this HtmlHelper html, string libraryName)
        {
            return AssetLib(html, libraryName, "js", (src) => "<script src=\"" + src + "\" language=\"javascript\" type=\"text/javascript\"></script>");
        }

        public static MvcHtmlString CssLib(this HtmlHelper html, string libraryName)
        {
            return AssetLib(html, libraryName, "css", (src) =>
                                                          {
                                                              if (src.EndsWith(".less"))
                                                                  return "<link href=\"" + src +
                                                                         "\" rel=\"stylesheet/less\" type=\"text/css\" />";
                                                              else
                                                              {
                                                                  return "<link href=\"" + src +
                                                                         "\" rel=\"stylesheet\" type=\"text/css\" />";
                                                              }
                                                          });
        }

        /// <summary>
        /// Returns the markup for a particular asset library, which is limited to js and css now.
        /// </summary>
        /// <param name="html"></param>
        /// <param name="libraryName"></param>
        /// <param name="libraryExtension"></param>
        /// <param name="tagBuilder"></param>
        /// <returns></returns>
        private static MvcHtmlString AssetLib(this HtmlHelper html, string libraryName, string libraryExtension, Func<string, string> tagBuilder)
        {
            if (UseLocalFiles)
            {
                //slow but this is for dev only
                var library = AssetBoosterConfigurationSection.Current.Libraries.Cast<AssetLibraryConfigElement>().Where(l => l.Name == libraryName).FirstOrDefault();
                if (library == null)
                    throw new InvalidOperationException("Library called '" + libraryName + "' could not be found.");
                StringBuilder sb = new StringBuilder();
                foreach (FileConfigElement asset in library.Files)
                {
                    if ( asset.IncludeIn == AssetEnvironment.Production)
                        continue;

                    var path = asset.CustomLocalPath.CharsOr(asset.Path);
                    path = path.StartsWith("~/") ? path : "~" + path.StartWith("/");
                    path = UrlHelper.GenerateContentUrl(path, html.ViewContext.HttpContext);
                    sb.AppendLine(tagBuilder(path));
                }
                return MvcHtmlString.Create(sb.ToString());
            }
            else
            {
                var context = html.ViewContext.HttpContext;
                var pathPrefix = CdnUrlPrefix(context.Request);
                //gets the bit that is inserted right before the extension, so the result is something like main.debug.js, main.min.js, and main.gzip.js, depending on the request
                var bundlePrefix = context.Items.Ensure("__assetSuffix", () =>
                {
                    //if the debug key was included in the query string, return the debug version of the asset bundle
                    var debugParam = context.Request["AssetDebugKey"];
                    if ( debugParam.HasChars() && debugParam.EqualsExact(DebugKey) )
                    {
                        return debugParam;
                    }
                    string acceptEncoding = context.Request.Headers["Accept-Encoding"];
                    if (acceptEncoding == null || !acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                        return "min";
                    else
                        return "gzip";
                }).CastTo<string>();
                var cdnFileName = libraryName.EndWithout(libraryExtension) + bundlePrefix + "." + libraryExtension;
                return MvcHtmlString.Create(tagBuilder(pathPrefix.SeperatedBy("/", cdnFileName)));
            }
        }


        /// <summary>
        /// Gets the proper http url prefix on the CDN for this request.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static string CdnUrlPrefix(this HttpRequestBase request)
        {
            return request.IsSecureConnection ? CdnHttpsUrlPrefix : CdnHttpUrlPrefix;
        }

    }

}

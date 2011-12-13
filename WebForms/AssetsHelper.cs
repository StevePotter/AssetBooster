using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using AssetBooster.Configuration;
using System.Web;
using System.Web.UI;
using System.IO;

namespace AssetBooster
{
    /// <summary>
    /// Helps to include assets that are deployed locally or on a CDN (where they are minified, compressed, and even forever cached).  Part of AssetBooster library.
    /// </summary>
    internal static class AssetHelper
    {
        static AssetHelper()
        {
            var config = AssetBoosterConfigurationSection.Current;
            if (config == null)
                throw new ConfigurationErrorsException("<assetBooster> section missing from configuration.  Please add to web.config.");

            if (!config.CdnUrlPrefix.HasChars())
            {
                throw new ConfigurationErrorsException("AssetBooster CdnUrlPrefix is required.  See your <asset> section in web.config.");
            }
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

        public static string UrlOnCdn(string relativeUrl, HttpRequest request)
        {
            return CdnUrlPrefix(request).SeperatedBy("/", relativeUrl.StartWithout("~"));
        }


        /// <summary>
        /// Returns the markup for a particular asset library, which is limited to js and css now.
        /// </summary>
        /// <param name="html"></param>
        /// <param name="libraryName"></param>
        /// <param name="libraryExtension"></param>
        /// <param name="tagBuilder"></param>
        /// <returns></returns>
        public static string AssetLib(this Control control, string libraryName, string libraryExtension, Func<string, string> tagBuilder)
        {
            if (UseLocalFiles)
            {
                //slow but this is for dev only
                var config = AssetBoosterConfigurationSection.Current;
                var library = AssetBoosterConfigurationSection.Current.Libraries.Cast<AssetLibraryConfigElement>().Where(l => l.Name == libraryName).FirstOrDefault();
                if (library == null)
                    throw new InvalidOperationException("Library called '" + libraryName + "' could not be found.");

                StringBuilder sb = new StringBuilder();

                foreach (FileConfigElement asset in library.Files)
                {
                    var path = asset.Path;
                    path = path.StartsWith("~/") ? path : "~" + path.StartWith("/");
                    path = control.ResolveUrl(path);
                    sb.AppendLine(tagBuilder(path));
                }
                return sb.ToString();
            }
            else
            {
                var context = HttpContext.Current;
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
                return tagBuilder(pathPrefix.SeperatedBy("/", cdnFileName));
            }
        }

        /// <summary>
        /// Gets the proper http url prefix on the CDN for this request.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static string CdnUrlPrefix(this HttpRequest request)
        {
            return request.IsSecureConnection ? CdnHttpsUrlPrefix : CdnHttpUrlPrefix;
        }

    }

}




        //static string[] GetAssetsInDirectory(string subDirectory, string appRootDirectory, string libraryExtension)
        //{
        //    if (library.Directory.HasChars())
        //    {
        //        var subDirectory = library.Directory.Trim('/', Path.DirectorySeparatorChar);
        //        var appRootDirectory = HttpContext.Current.Server.MapPath(string.Empty);//get the directory to the root of the application
        //        var assetFolder = Path.Combine(appRootDirectory, subDirectory);

        //        var searchPattern = "*." + libraryExtension;

        //        string folder, searchPattern;
        //        if (library.Directory.Contains("*"))
        //        {
        //            folder = library.Directory.Before("*");
        //            searchPattern = library.Directory.After("*").StartWith("*");
        //        }
        //        else
        //        {
        //            folder = library.Directory;
        //            searchPattern = "*." + libraryExtension;
        //        }

        //        var absoluteDirectory = Path.Combine(appDirectory, folder.StartWithout(@"/").StartWithout(@"\")).EndWithout(@"/").EndWithout(@"\");
        //        var files = Directory.GetFiles(absoluteDirectory, searchPattern, SearchOption.AllDirectories);
        //        foreach (var file in files)
        //        {
        //            var path = file.StartWithout(appDirectory);
        //            path = path.StartsWith("~/") ? path : "~" + path.StartWith("/");
        //            path = control.ResolveUrl(path);
        //            sb.AppendLine(tagBuilder(path));

        //        }
        //    }
        //    subDirectory = subDirectory.Trim('/', Path.DirectorySeparatorChar);//get rid of surrounding slashes or else Path.Combine might not work right
        //    var assetFolder = Path.Combine(appRootDirectory, subDirectory);

        //}

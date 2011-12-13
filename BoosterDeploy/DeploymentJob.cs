using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Configuration;
using AssetBooster.Configuration;
using System.Security;
using System.Xml.Linq;
using BoosterDeploy.Properties;
using System.IO.Compression;
using Amazon.S3.Model;
using System.Collections.Specialized;
using Amazon.S3;
using CommandLine;
using CommandLine.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;

namespace BoosterDeploy
{

    internal class LibraryFile
    {
        public AssetEnvironment IncludeIn { get; set; }
        public string AbsolutePath { get; set; }
        //        public string AppPath { get; set; }
        //        public bool PreMinified { get; set; }
    }


    public class DeploymentJob
    {

        const string JsExtension = ".js";
        const string JsMinifiedExtension = ".min.js";
        const string JsGZippedExtension = ".gzip.js";
        const string CssExtension = ".css";
        const string CssMinifiedExtension = ".min.css";
        const string CssGZippedExtension = ".gzip.css";

        /// <summary>
        /// The directory to the root of the web app.
        /// </summary>
        public DirectoryInfo WebAppRoot { get; set; }

        /// <summary>
        /// The public access key for Amazon Web Services.
        /// </summary>
        public string AwsKey { get; set; }

        /// <summary>
        /// The secret key for Amazon Web Services.
        /// </summary>
        public string AwsSecretKey { get; set; }

        /// <summary>
        /// The name of the S3 bucket to upload to.
        /// </summary>
        public string S3BucketName { get; set; }

        /// <summary>
        /// Search patterns for each type of binary asset file.  For example: "*.png","*.bmp"
        /// </summary>
        public string[] BinaryAssetsSearchPatterns { get; set; }

        /// <summary>
        /// The full path to the java exe file used to run Google Closure.
        /// </summary>
        public string JavaPath { get; set; }

        /// <summary>
        /// The config for AssetBooster.  This is extracted from the web app's web.config, then replaces the web.config's section after the asset version changes.
        /// </summary>
        private AssetBoosterConfigurationSection ConfigSection { get; set; }

        /// <summary>
        /// The web app's web.config file that will be loaded and possibly modified.
        /// </summary>
        private Configuration WebConfig { get; set; }

        /// <summary>
        /// A list of directories whose assets will be ignored.  These are relative to the app path, like "code\asdf","bin","obj\more"
        /// </summary>
        public string[] DirectoriesToIgnore { get; set; }

        /// <summary>
        /// DirectoriesToIgnore is translated into a list of directories to include, which is stored here.
        /// </summary>
        private string[] DirectoriesToInclude { get; set; }

        /// <summary>
        /// When true, every css file will be deployed in addition to bundles.
        /// </summary>
        public bool AddEveryCss { get; set; }

        /// <summary>
        /// When true, every js file will be deployed in addition to bundles.
        /// </summary>
        public bool AddEveryJs { get; set; }

        public JsMinifier JsMinifier { get; set; }

        /// <summary>
        /// The current asset version of this asset batch.
        /// </summary>
        public int AssetVersion { get; set; }

        /// <summary>
        /// When true, the asset version in web.config will be auto incremented and web.config will be modified.
        /// </summary>
        public bool AutoUpdateAssetVersion { get; set; }

        /// <summary>
        /// When set, this indicates a suffix for text-based assets where the non-minified version of each asset bundle will be uploaded. 
        /// </summary>
        private string DebugKey { get; set; }

        private AmazonS3Client S3Client { get; set; }

        private bool UploadToS3
        {
            get { return AwsKey.HasChars() && AwsSecretKey.HasChars() && S3BucketName.HasChars(); }
        }

        /// <summary>
        /// The folder within S3 bucket to dump asset files.
        /// </summary>
        private string CdnFolder
        {
            get
            {
                if (ConfigSection.CdnSubDirectory.HasChars())
                {
                    return ConfigSection.CdnSubDirectory.SeperatedBy("/", AssetVersion.ToInvariant());
                }
                else
                {
                    return AssetVersion.ToInvariant();
                }
            }
        }



        ///// <summary>
        ///// When specified, this is a folder where copies of the assets will get stored.
        ///// </summary>
        //private string LocalDirectory { get; set; }

        //private bool LocalDirectory
        //{
        //    get { return TempFolder.HasChars(); }
        //}

        /// <summary>
        /// Runs the job.
        /// </summary>
        public void Execute()
        {

            //first establish all the directories to include
            var directories = new List<string>(Directory.GetDirectories(WebAppRoot.FullName, "*", SearchOption.AllDirectories));
            if ( DirectoriesToIgnore.HasItems() )
                directories.RemoveAll(directory => DirectoriesToIgnore.Select(d => Path.Combine(WebAppRoot.FullName,d)).Where(d => directory.StartsWith(d)).FirstOrDefault() != null);
            DirectoriesToInclude = directories.ToArray();

            ProcessWebConfig();
            S3Client = UploadToS3 ? new Amazon.S3.AmazonS3Client(AwsKey, AwsSecretKey, new AmazonS3Config
                                                                                    {
                                                                                        CommunicationProtocol =
                                                                                            Protocol.HTTP,
                                                                                    })
                           : null;

            using (S3Client)
            {
                ProcessBinaryAssets();
                ProcessCss();
                ProcessJs();
            }
        }


        /// <summary>
        /// Extracts information from the web.config and potentially updates it with the new asset version.
        /// </summary>
        private void ProcessWebConfig()
        {
            var webConfigPath = Path.Combine(WebAppRoot.FullName, "web.config");
            if (!File.Exists(webConfigPath))
            {
                Console.WriteLine(string.Format("ERROR: web.config could not be found at {0}", webConfigPath));
                ExitCode.InputError.Exit();
            }
            try
            {
                WebConfig = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = webConfigPath }, ConfigurationUserLevel.None);
                for (var i = 0; i < WebConfig.Sections.Count; i++)//foreach would bomb out if one section was bad, so a for loop fixed it
                {
                    try
                    {
                        var section = WebConfig.Sections[i];
                        if (section is AssetBoosterConfigurationSection)
                        {
                            ConfigSection = (AssetBoosterConfigurationSection) section;
                            if (AutoUpdateAssetVersion)
                                AssetVersion = ConfigSection.Version.ToIntTry().GetValueOrDefault(0) + 1;
                            break;
                        }

                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch( Exception ex )
            {
                Console.WriteLine(string.Format("ERROR: web.config could not be read.  Message: {0}", ex.Message));
                ExitCode.InputError.Exit();
            }

            if (ConfigSection == null)
            {
                Console.WriteLine(string.Format("ERROR: AssetBoosterConfigurationSection could not be found in the web.config."));
                ExitCode.InputError.Exit();
            }

            if (AutoUpdateAssetVersion)
            {
                ConfigSection.Version = AssetVersion.ToInvariant();
                WebConfig.Save();
            }

            if (ConfigSection.DebugKey.HasChars())
            {
                this.DebugKey = ConfigSection.DebugKey;
            }
        }


        private string ToRelativeWebPath(string filePath)
        {
            return filePath.Substring(WebAppRoot.FullName.Length).Replace(System.IO.Path.DirectorySeparatorChar, '/');
        }


        private string GetCombinedFilesText(IEnumerable<string> paths)
        {
            return string.Join(Environment.NewLine, paths.Select(path => File.ReadAllText(path)).ToArray());
        }

        /// <summary>
        /// Returns the name and full info to assets for all bundles whose name has the given extension.
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        private Dictionary<string, LibraryFile[]> GetAssetBundlesWithExtension(string extension)
        {
            var result = new Dictionary<string, LibraryFile[]>();
            foreach (AssetLibraryConfigElement assetLib in ConfigSection.Libraries)
            {
                if ( assetLib.IncludeIn == AssetEnvironment.Local )//ignore local ones
                    continue;

                if (assetLib.Name.HasChars() && assetLib.Name.EndsWith(extension))
                {
                    var paths = new List<LibraryFile>();
                    foreach (FileConfigElement asset in assetLib.Files)
                    {
                        if ( asset.IncludeIn == AssetEnvironment.Local)
                        {
                            continue;
                        }
                        var path = Path.Combine(WebAppRoot.FullName, asset.Path.StartWithout("/").Replace('/', System.IO.Path.DirectorySeparatorChar));
                        if (!File.Exists(path))
                        {
                            Console.WriteLine(string.Format("WARNING: asset '{0}' in bundle '{1}' could not be found.  Job will continue but you need to fix the <add path=\"{0}\" /> element your web.config!", asset.Path, assetLib.Name));
                        }
                        else
                        {
                            paths.Add(new LibraryFile
                                          {
                                              AbsolutePath = path,
                                              //AppPath = asset.Path,
                                              IncludeIn = asset.IncludeIn,
                                          });
                        }
                    }
                    if (paths.Count == 0)
                    {
                        Console.WriteLine(string.Format("WARNING: asset bundle '{0}' had no asset files and will be ignored.", assetLib.Name));
                    }
                    else
                    {
                        result[assetLib.Name] = paths.ToArray();
                    }
                }
            }
            return result;
        }

        private void ProcessBinaryAssets()
        {
            foreach (var file in AssetsWithPattern(BinaryAssetsSearchPatterns))
            {
                var relativePath = ToRelativeWebPath(file);

                if (UploadToS3)
                {
                    UploadPublicCachedFileToS3(File.ReadAllBytes(file), relativePath, false);
                }
            }
        }

        /// <summary>
        /// Gets all files in the app (excluding ignored directories) that match the patterns.
        /// </summary>
        /// <param name="patterns"></param>
        /// <returns></returns>
        private IEnumerable<string> AssetsWithPattern(params string[] patterns)
        {
            return patterns.SelectMany(pattern => DirectoriesToInclude.SelectMany(dir => Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly)));
        }


        /// <summary>
        /// Creates and deploys all the css bundles and possibly each individual file.
        /// </summary>
        private void ProcessCss()
        {
            if (AddEveryCss)
            {
                foreach (var file in AssetsWithPattern("*" + CssExtension))
                {
                    var fileSource = File.ReadAllText(file);
                    var relativePath = ToRelativeWebPath(file).EndWithout(CssExtension, StringComparison.OrdinalIgnoreCase);
                    ProcessCssBundle(relativePath, fileSource);
                }
            }

            foreach (var assetLib in GetAssetBundlesWithExtension(CssExtension))
            {
                var source = GetCombinedFilesText(assetLib.Value.Select(f => f.AbsolutePath));
                ProcessCssBundle(assetLib.Key, source);
            }
        }


        /// <summary>
        /// Takes the css text provided and uploads the necessary bundles to CDN.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="cssSource"></param>
        private void ProcessCssBundle(string relativePath, string cssSource)
        {
            var pathWithoutExt = relativePath.NotSurroundedBy("/", CssExtension);
            //upload full dev version
            if (UploadToS3 && DebugKey.HasChars())
                UploadPublicCachedFileToS3(cssSource, pathWithoutExt + "." + DebugKey + CssExtension, false);

            if (UploadToS3)
            {
                var minifiedCss = cssSource.HasChars()
                                      ? Yahoo.Yui.Compressor.CssCompressor.Compress(cssSource)
                                      : string.Empty;
                //minified no gzip
                UploadPublicCachedFileToS3(minifiedCss, pathWithoutExt + CssMinifiedExtension, false);
                //minified and gzipped script
                UploadPublicCachedFileToS3(minifiedCss.GZip(), pathWithoutExt + CssGZippedExtension, true);
            }
        }


        /// <summary>
        /// Takes all the javascript files in the app, compresses them as necessary, and deploys them.
        /// </summary>
        private void ProcessJs()
        {
            if (AddEveryJs)
            {
                foreach (var file in AssetsWithPattern("*" + JsExtension))
                {
                    var relativePath = ToRelativeWebPath(file).EndWithout(JsExtension, StringComparison.OrdinalIgnoreCase);
                    ProcessJsBundle(relativePath, new LibraryFile[]{new LibraryFile{ AbsolutePath = file }});
                }
            }

            foreach (var assetLib in GetAssetBundlesWithExtension(JsExtension))
            {
                ProcessJsBundle(assetLib.Key, assetLib.Value);
            }
        }

        /// <summary>
        /// Processes a set of javascript files into a single output file.
        /// </summary>
        /// <param name="outputRelativePath"></param>
        /// <param name="inputFiles"></param>
        private void ProcessJsBundle(string outputRelativePath, IEnumerable<LibraryFile> inputFiles)
        {
            var outputKeyPrefix = outputRelativePath.NotSurroundedBy("/", JsExtension);//this is everything but the extension, so we can add ".min.js" and ".gzip.js" to it.
            //upload full dev version
            if (UploadToS3 && DebugKey.HasChars())
            {
                UploadPublicCachedFileToS3(GetCombinedFilesText(inputFiles.Select(f => f.AbsolutePath)), outputKeyPrefix + "-" + DebugKey + JsExtension, false);
            }

            //minification can get slightly tricky.  we honor the ".min.js" convention for minified files (like "jquery-1.4.1.min.js").  in the case of an existing minified file, we do not minify it, and instead use their provided minified version.  this makes a single asset bundle potentially complicated, breaking it into multiple files that each need to be minified individually
            StringBuilder minifiedJsSource = new StringBuilder();
            List<string> currentBatch = null;
            bool currentBatchIsPreMinified = false;
            foreach (var inputFile in inputFiles)
            {
                var minifiedPath = inputFile.AbsolutePath.EndWithout(JsExtension).EndWith(JsMinifiedExtension);
                var minifiedExists = File.Exists(minifiedPath);
                if (currentBatch == null)
                {
                    currentBatch = new List<string>();
                }
                else
                {
                    if (minifiedExists)//there is a pre-minified version
                    {
                        Console.WriteLine("Pre-minified file '{0}' was found and will be used instead of minifying ourselves.", minifiedPath);
                        if (!currentBatchIsPreMinified)//the current batch of files aren't pre-minified, so minify them and start a fresh batch
                        {
                            minifiedJsSource.AppendLine(MinifyJsFiles(currentBatch));
                            currentBatch.Clear();
                        }
                    }
                    else
                    {
                        if (currentBatchIsPreMinified)//the current batch of files aren pre-minified, so dump their source and start a fresh batch
                        {
                            minifiedJsSource.AppendLine(GetCombinedFilesText(currentBatch));
                            currentBatch.Clear();
                        }
                    }
                }
                currentBatchIsPreMinified = minifiedExists;
                currentBatch.Add(minifiedExists ? minifiedPath : inputFile.AbsolutePath);
            }

            if ( currentBatchIsPreMinified )
                minifiedJsSource.AppendLine(GetCombinedFilesText(currentBatch));
            else
                minifiedJsSource.AppendLine(MinifyJsFiles(currentBatch));

            if (UploadToS3)
            {
                //minified no gzip
                UploadPublicCachedFileToS3(minifiedJsSource.ToString(), outputKeyPrefix + JsMinifiedExtension, false);
                //minified and gzipped script
                UploadPublicCachedFileToS3(minifiedJsSource.ToString().GZip(), outputKeyPrefix + JsGZippedExtension,
                                           true);
            }
        }


        /// <summary>
        /// Minifies the given javascript using either google closure or yahoo's compiler.
        /// </summary>
        /// <param name="inputPaths"></param>
        /// <returns></returns>
        private string MinifyJsFiles(IEnumerable<string> inputPaths)
        {
            var source = GetCombinedFilesText(inputPaths);
            if (JavaPath.HasChars())
            {
                var java = new FileInfo(JavaPath);
                var inputFile = Path.GetTempFileName();
                File.WriteAllText(inputFile, source);
                var outputFile = Path.GetTempFileName();
                var closure = Path.Combine(Environment.CurrentDirectory, "googleclosure.jar");
                if (File.Exists(closure))
                {
                    var arguments = string.Format("-jar \"{0}\" --js \"{1}\" --js_output_file \"{2}\"", closure, inputFile, outputFile);
                    using (Process process = System.Diagnostics.Process.Start(new ProcessStartInfo(java.Name, arguments)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WorkingDirectory = java.DirectoryName

                    }))
                    {
                        process.Start();
                        process.WaitForExit();
                        using (StreamReader stdErr = process.StandardError)
                        {
                            var d = stdErr.ReadToEnd();
                        }
                    }
                    try
                    {
                        return File.ReadAllText(outputFile);
                    }
                    catch (IOException)
                    {
                        //this was happening occasionally
                        System.Threading.Thread.Sleep(1000);
                        return File.ReadAllText(outputFile);
                    }
                }
            }
            
            return Yahoo.Yui.Compressor.JavaScriptCompressor.Compress(source, false);
        }


        private void UploadPublicCachedFileToS3(string text, string relativeFileName, bool isGZipped)
        {
            UploadFileToS3AsPublicCached(new MemoryStream(Encoding.UTF8.GetBytes(text)), S3BucketName, CdnFolder.SeperatedBy("/", relativeFileName), isGZipped);
        }

        private void UploadPublicCachedFileToS3(byte[] bytes, string relativeFileName, bool isGZipped)
        {
            UploadFileToS3AsPublicCached(new MemoryStream(bytes), S3BucketName, CdnFolder.SeperatedBy("/", relativeFileName), isGZipped);
        }

        public void UploadFileToS3AsPublicCached(MemoryStream inputStream, string bucketName, string filePathInBucket, bool isGZipped)
        {
            UploadFileToS3(inputStream, bucketName, filePathInBucket, "public, max-age=31536000", S3CannedACL.PublicRead, isGZipped);//use max-age instead of expires because its a sliding expiration.  this will cause the browser to cache it for a year, no questions asked.   
        }


        public void UploadFileToS3(MemoryStream inputStream, string bucketName, string fileKey, string cacheControl, Amazon.S3.Model.S3CannedACL acl, bool isGZipped)
        {
            Debug.Assert(UploadToS3);
            var request = new Amazon.S3.Model.PutObjectRequest
            {
                InputStream = inputStream,
                BucketName = bucketName,
                CannedACL = Amazon.S3.Model.S3CannedACL.PublicRead,
                Key = fileKey,
                ContentType = GetMimeType(System.IO.Path.GetExtension(fileKey)),
            };
            Console.WriteLine(string.Format("Uploading file '{0}' to S3.", fileKey));
            var headers = new NameValueCollection();
            if (!string.IsNullOrEmpty(cacheControl))
                headers.Add("Cache-Control", cacheControl);
            if (isGZipped)
                headers.Add("Content-Encoding", "gzip");
            request.ContentType = GetMimeType(System.IO.Path.GetExtension(fileKey));
            inputStream.Position = 0;

            request.AddHeaders(headers);
            S3Client.PutObject(request);
        }


        #region Mime Types

        /// <summary>
        /// An array of file extension/mime type pairs generated from lists found online.
        /// </summary>
        readonly static string[] m_mimeTypesPerExtensionSource = new string[] { 
        "acx", "application/internet-property-stream", "ai", "application/postscript", "aif", "audio/x-aiff", "aifc", "audio/x-aiff", "aiff", "audio/x-aiff", "asf", "video/x-ms-asf", "asr", "video/x-ms-asf", "asx", "video/x-ms-asf", "au", "audio/basic", "avi", "video/x-msvideo", "axs", "application/olescript", "bas", "text/plain", "bcpio", "application/x-bcpio", "bin", "application/octet-stream", "bmp", "image/bmp", "c", "text/plain", "cat", "application/vnd.ms-pkiseccat", "cdf", "application/x-cdf", "cer", "application/x-x509-ca-cert", "class", "application/octet-stream", "clp", "application/x-msclip", "cmx", "image/x-cmx", "cod", "image/cis-cod", "cpio", "application/x-cpio", "crd", "application/x-mscardfile", "crl", "application/pkix-crl", "crt", "application/x-x509-ca-cert", "csh", "application/x-csh", "css", "text/css", "dcr", "application/x-director", "der", "application/x-x509-ca-cert", "dir", "application/x-director", "dll", "application/x-msdownload", "dms", "application/octet-stream", "doc", "application/msword", "dot", "application/msword", "dvi", "application/x-dvi", "dxr", "application/x-director", "eps", "application/postscript", "etx", "text/x-setext", "evy", "application/envoy", "exe", "application/octet-stream", "fif", "application/fractals", "flr", "x-world/x-vrml", "gif", "image/gif", "gtar", "application/x-gtar", "gz", "application/x-gzip", "h", "text/plain", "hdf", "application/x-hdf", "hlp", "application/winhlp", "hqx", "application/mac-binhex40", "hta", "application/hta", "htc", "text/x-component", "htm", "text/html", "html", "text/html", "htt", "text/webviewhtml", "ico", "image/x-icon", "ief", "image/ief", "iii", "application/x-iphone", "ins", "application/x-internet-signup", "isp", "application/x-internet-signup", "jfif", "image/pipeg", "jpe", "image/jpeg", "jpeg", "image/jpeg", "jpg", "image/jpeg", "js", "application/x-javascript", "latex", "application/x-latex", "lha", "application/octet-stream", "lsf", "video/x-la-asf", "lsx", "video/x-la-asf", "lzh", "application/octet-stream", "m13", "application/x-msmediaview", "m14", "application/x-msmediaview", "m3u", "audio/x-mpegurl", "man", "application/x-troff-man", "mdb", "application/x-msaccess", "me", "application/x-troff-me", "mht", "message/rfc822", "mhtml", "message/rfc822", "mid", "audio/mid", "mny", "application/x-msmoney", "mov", "video/quicktime", "movie", "video/x-sgi-movie", "mp2", "video/mpeg", "mp3", "audio/mpeg", "mpa", "video/mpeg", "mpe", "video/mpeg", "mpeg", "video/mpeg", "mpg", "video/mpeg", "mpp", "application/vnd.ms-project", "mpv2", "video/mpeg", "ms", "application/x-troff-ms", "mvb", "application/x-msmediaview", "nws", "message/rfc822", "oda", "application/oda", "p10", "application/pkcs10", "p12", "application/x-pkcs12", "p7b", "application/x-pkcs7-certificates", "p7c", "application/x-pkcs7-mime", "p7m", "application/x-pkcs7-mime", "p7r", "application/x-pkcs7-certreqresp", "p7s", "application/x-pkcs7-signature", "pbm", "image/x-portable-bitmap", "pdf", "application/pdf", "pfx", "application/x-pkcs12", "pgm", "image/x-portable-graymap", "pko", "application/ynd.ms-pkipko", "pma", "application/x-perfmon", "pmc", "application/x-perfmon", "pml", "application/x-perfmon", "pmr", "application/x-perfmon", "pmw", "application/x-perfmon", "pnm", "image/x-portable-anymap", "pot,", "application/vnd.ms-powerpoint", "ppm", "image/x-portable-pixmap", "pps", "application/vnd.ms-powerpoint", "ppt", "application/vnd.ms-powerpoint", "prf", "application/pics-rules", "ps", "application/postscript", "pub", "application/x-mspublisher", "qt", "video/quicktime", "ra", "audio/x-pn-realaudio", "ram", "audio/x-pn-realaudio", "ras", "image/x-cmu-raster", "rgb", "image/x-rgb", "rmi", "audio/mid", "roff", "application/x-troff", "rtf", "application/rtf", "rtx", "text/richtext", "scd", "application/x-msschedule", "sct", "text/scriptlet", "setpay", "application/set-payment-initiation", "setreg", "application/set-registration-initiation", "sh", "application/x-sh", "shar", "application/x-shar", "sit", "application/x-stuffit", "snd", "audio/basic", "spc", "application/x-pkcs7-certificates", "spl", "application/futuresplash", "src", "application/x-wais-source", "sst", "application/vnd.ms-pkicertstore", "stl", "application/vnd.ms-pkistl", "stm", "text/html", "svg", "image/svg+xml", "sv4cpio", "application/x-sv4cpio", "sv4crc", "application/x-sv4crc", "swf", "application/x-shockwave-flash", "t", "application/x-troff", "tar", "application/x-tar", "tcl", "application/x-tcl", "tex", "application/x-tex", "texi", "application/x-texinfo", "texinfo", "application/x-texinfo", "tgz", "application/x-compressed", "tif", "image/tiff", "tiff", "image/tiff", "tr", "application/x-troff", "trm", "application/x-msterminal", "tsv", "text/tab-separated-values", "txt", "text/plain", "uls", "text/iuls", "ustar", "application/x-ustar", "vcf", "text/x-vcard", "vrml", "x-world/x-vrml", "wav", "audio/x-wav", "wcm", "application/vnd.ms-works", "wdb", "application/vnd.ms-works", "wks", "application/vnd.ms-works", "wmf", "application/x-msmetafile", "wps", "application/vnd.ms-works", "wri", "application/x-mswrite", "wrl", "x-world/x-vrml", "wrz", "x-world/x-vrml", "xaf", "x-world/x-vrml", "xbm", "image/x-xbitmap", "xla", "application/vnd.ms-excel", "xlc", "application/vnd.ms-excel", "xlm", "application/vnd.ms-excel", "xls", "application/vnd.ms-excel", "xlt", "application/vnd.ms-excel", "xlw", "application/vnd.ms-excel", "xof", "x-world/x-vrml", "xpm", "image/x-xpixmap", "xwd", "image/x-xwindowdump", "z", "application/x-compress", "zip", "application/zip", "asc", "text/plain", "atom", "application/atom+xml", "cgm", "image/cgm", "cpt", "application/mac-compactpro", "dif", "video/x-dv", "djv", "image/vnd.djvu", "djvu", "image/vnd.djvu", "dmg", "application/octet-stream", "dtd", "application/xml-dtd", "dv", "video/x-dv", "ez", "application/andrew-inset", "gram", "application/srgs", "grxml", "application/srgs+xml", "ice", "x-conference/x-cooltalk", "ics", "text/calendar", "ifb", "text/calendar", "iges", "model/iges", "igs", "model/iges", "jnlp", "application/x-java-jnlp-file", "jp2", "image/jp2", "kar", "audio/midi", "m4a", "audio/mp4a-latm", "m4b", "audio/mp4a-latm", "m4p", "audio/mp4a-latm", "m4u", "video/vnd.mpegurl", "m4v", "video/x-m4v", "mac", "image/x-macpaint", "mathml", "application/mathml+xml", "mesh", "model/mesh", "midi", "audio/midi", "mif", "application/vnd.mif", "mp4", "video/mp4", "mpga", "audio/mpeg", "msh", "model/mesh", "mxu", "video/vnd.mpegurl", "nc", "application/x-netcdf", "ogg", "application/ogg", "pct", "image/pict", "pdb", "chemical/x-pdb", "pgn", "application/x-chess-pgn", "pic", "image/pict", "pict", "image/pict", "png", "image/png", "pnt", "image/x-macpaint", "pntg", "image/x-macpaint", "qti", "image/x-quicktime", "qtif", "image/x-quicktime", "rdf", "application/rdf+xml", "rm", "application/vnd.rn-realmedia", "sgm", "text/sgml", "sgml", "text/sgml", "silo", "model/mesh", "skd", "application/x-koan", "skm", "application/x-koan", "skp", "application/x-koan", "skt", "application/x-koan", "smi", "application/smil", "smil", "application/smil", "so", "application/octet-stream", "vcd", "application/x-cdlink", "vxml", "application/voicexml+xml", "wbmp", "image/vnd.wap.wbmp", "wbmxl", "application/vnd.wap.wbxml", "wml", "text/vnd.wap.wml", "wmlc", "application/vnd.wap.wmlc", "wmls", "text/vnd.wap.wmlscript", "wmlsc", "application/vnd.wap.wmlscriptc", "xht", "application/xhtml+xml", "xhtml", "application/xhtml+xml", "xml", "application/xml", "xsl", "application/xml", "xslt", "application/xslt+xml", "xul", "application/vnd.mozilla.xul+xml", "xyz", "chemical/x-xyz"
       };

        const string DefaultMime = "application/octet-stream";

        /// <summary>
        /// Maps extensions to mime types.  Used for faster lookups.
        /// </summary>
        static Dictionary<string, string> m_mimeTypesPerExtension = CreateMimeTypesPerExtension();

        /// <summary>
        /// Takes the array of mime types 
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, string> CreateMimeTypesPerExtension()
        {
            Dictionary<string, string> types = new Dictionary<string, string>();
            string[] sourceArray = m_mimeTypesPerExtensionSource;
            for (int i = 0; i < sourceArray.Length; i += 2)
            {
                types.Add(sourceArray[i], sourceArray[i + 1]);
            }
            return types;
        }

        /// <summary>
        /// Gets the MIME type for the given extension.  If there is no specific MIME type available, it returns "application/octet-stream".
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static string GetMimeType(string fileOrExt)
        {
            var ext = Path.GetExtension(fileOrExt).CharsOr(fileOrExt);
            if (ext.Contains("."))
                ext = ext.After(".");
            if (!ext.HasChars())
                return DefaultMime;

            string mimeType;
            if (m_mimeTypesPerExtension.TryGetValue(ext.ToLowerInvariant(), out mimeType))
                return mimeType;
            else
                return DefaultMime;
        }

        #endregion
    }


}


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
using System.ComponentModel;

namespace BoosterDeploy
{

    /// <summary>
    /// Options parsed from command line arguments.
    /// </summary>
    public sealed class CommandLineOptions
    {

        [Option("b", "s3Bucket", Required = true, HelpText = "The name of the Amazon S3 bucket that asset files will be uploaded to.")]
        public string S3Bucket = String.Empty;

        /// <summary>
        /// The directory to the root of the web app.
        /// </summary>
        [Option("a", "appPath", Required = true, HelpText = "The full path to the web application's root folder.  Do not end in a backslash.")]
        public string AppPath;

        /// <summary>
        /// The public access key for Amazon Web Services.
        /// </summary>
        [Option("k", "awsKey", Required = true, HelpText = "The public access key for your S3 account.")]
        public string AwsKey;

        /// <summary>
        /// The secret key for Amazon Web Services.
        /// </summary>
        [Option("s", "awsSecretKey", Required = true, HelpText = "The secret access key for your S3 account.")]
        public string AwsSecretKey;

        [OptionList("p", "binaryFilePatterns", Separator = ',', HelpText = "Search patterns for each type of binary asset file.  For example: \"*.png,*.bmp\".  Default is \"*.png,*.bmp,*.jpg,*.jpeg,*.gif,*.bmp,*.ico,*.tiff,*.swf\"")]
        [DefaultValue("*.png,*.bmp,*.jpg,*.jpeg,*.gif,*.bmp,*.ico,*.tiff,*.swf")]
        public IList<string> BinaryAssetsSearchPatterns;

        [OptionList("i", "ignoreDirectories", Separator = '|', HelpText = "A pipe (|) separated list of directories relative to the web root that should be ignored.  Example: bin|app_code.  Default is \"bin|obj\".")]
        public IList<string> IgnoreDirectories;

        [Option(null, "javaPath", HelpText = "The full path to Java, needed to run Google Closure if it can't be found automatically")]
        public string JavaPath;

        [Option(null, "addEachCss", Required = false, HelpText = "When true, individual css files will be deployed to the CDN in addition to any bundles specified.")]
        public bool AddEachCss;

        [Option(null, "addEachJs", Required = false, HelpText = "When true, individual js files will be deployed to the CDN in addition to any bundles specified.")]
        public bool AddEachJs;

        [Option("v", "assetVersion", Required = false, HelpText = "When set, this indicates an explicit asset version to use.  If not provided, the version will be taken from web.config, and web.config will be updated to reflect the new version.")]
        public int? AssetVersion;

        [HelpOption(HelpText = "Dispaly this help screen.")]
        public string GetUsage()
        {
            var help = new HelpText("Usage Instructions:");
            help.AdditionalNewLineAfterOption = true;
            help.AddPreOptionsLine("This is free, open source software under the the MIT License <http://www.opensource.org/licenses/mit-license.php>.");
            help.AddOptions(this);

            return help;
        }

    }

}


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
    /// 
    /// </summary>
    class Program
    {


        static int Main(string[] args)
        {
            Console.WriteLine("stevepotter.me's Asset Booster");
            var options = new CommandLineOptions();
            ICommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
            if (!parser.ParseArguments(args, options))
                ExitCode.InputError.Exit();

            //setup the job
            DeploymentJob job = CreateJob(options);
            try
            {
                Console.WriteLine("Uploading to S3 bucket '" + options.S3Bucket + "'.");
                job.Execute();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Report("Error: ", true, false, false));
                ExitCode.ProcessingError.Exit();
            }
            return (int)ExitCode.Success;
        }

        /// <summary>
        /// Creates a deploymentjob from the command line options specified.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        static DeploymentJob CreateJob(CommandLineOptions options)
        {
            var job = new DeploymentJob
            {
                S3BucketName = options.S3Bucket,
                AwsKey = options.AwsKey,
                AwsSecretKey = options.AwsSecretKey,
            };

            job.WebAppRoot = new DirectoryInfo(options.AppPath);
            if (!job.WebAppRoot.Exists)
            {
                Console.WriteLine(string.Format("No directory found for --{1} argument value of {0}.", options.AppPath, options.Reflect(o=> o.AppPath).Attribute<OptionAttribute>().LongName));
                ExitCode.InputError.Exit();
            }

            if (options.BinaryAssetsSearchPatterns.HasItems())
            {
                job.BinaryAssetsSearchPatterns = options.BinaryAssetsSearchPatterns.Select(p => p.Trim()).ToArray();
            }
            else
            {
                job.BinaryAssetsSearchPatterns = ((string)options.Reflect(o => o.BinaryAssetsSearchPatterns).Attribute<DefaultValueAttribute>().Value).Split(',');
            }

            if (options.IgnoreDirectories.HasItems())
            {
                job.DirectoriesToIgnore = options.IgnoreDirectories.Select(d => d.Trim().Replace('/',Path.DirectorySeparatorChar).StartWithout(Path.DirectorySeparatorChar.ToString())).ToArray();
            }
            else
            {
                job.DirectoriesToIgnore = new string[] { "bin", "obj" };
            }

            if (options.JavaPath.HasChars() && !File.Exists(options.JavaPath))
            {
                Console.WriteLine("javapath argument '" + options.JavaPath + "' was invalid because the file could not be found.");
                ExitCode.InputError.Exit();
            }
            else if ( !options.JavaPath.HasChars() )
            {
                Console.WriteLine("Searching for java.exe...");
                options.JavaPath = LocateJava();
                if (!options.JavaPath.HasChars())
                    Console.WriteLine(string.Format("Warning: java.exe could not be found.  Please specify command arg {0}.  Without it, Google Closure can't be used to minify javascript.  Please install Java JRE or specify the path to the java.exe file.  Otherwise you are missing out on better compression!", "javapath"));     
            }

            job.JavaPath = options.JavaPath;

            job.AddEveryCss = options.AddEachCss;
            job.AddEveryJs = options.AddEachJs;
            if (options.AssetVersion.HasValue)
            {
                job.AssetVersion = options.AssetVersion.Value;
            }
            else
            {
                job.AutoUpdateAssetVersion = true;
            }

            return job;
        }

        private static string LocateJava()
        {
            var path = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!path.HasChars())
                return null;

            return Directory.GetFiles(path, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
        }

    }


    enum ExitCode
    {
        Success = 0,
        /// <summary>
        /// Indicates a command line argument was missing or invalid.
        /// </summary>
        InputError = 1,
        /// <summary>
        /// An error occured at some point.
        /// </summary>
        ProcessingError = 2,
    }


    public enum JsMinifier
    {
        None,
        Auto,
        Closure,
        Yahoo
    }

}

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

namespace BoosterDeploy
{

    internal static class ExtensionMethods
    {

        /// <summary>
        /// Given the member passed, this provides reflection information about it.  This is highly useful when getting metadata like attributes.
        /// </summary>
        /// <typeparam name="TDeclarer"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="value"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static MemberInfo Reflect<TDeclarer, TValue>(this TDeclarer value, Expression<Func<TDeclarer, TValue>> action)
        {
            var asMem = action.Body as MemberExpression;
            if (asMem != null)
                return asMem.Member;

            var asMeth = action.Body as MethodCallExpression;
            if (asMeth != null)
                return asMeth.Method;
            return null;
        }


        /// <summary>
        /// Gets the first attribute for the given member.  Searches through base classes as well.  If the attribute isn't found, null is returned.
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="info"></param>
        /// <returns></returns>
        public static TAttribute Attribute<TAttribute>(this MemberInfo info) where TAttribute : System.Attribute
        {
            return info.GetCustomAttributes(typeof(TAttribute), true).FirstOrDefault().MapIfNotNull(a => (TAttribute)a);
        }

        public static void Exit(this ExitCode code)
        {
            Console.WriteLine("Exiting with code " + ((int)code).ToInvariant() + ": " + code.ToString());
            Environment.Exit((int)code);
        }

        public static TValue ValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> values, TKey key, TValue defaultValue)
        {
            TValue output;
            if (values.TryGetValue(key, out output))
                return output;

            return defaultValue;
        }

        
        public static byte[] GZip(this string value)
        {
            //create a temp file with the file text
            var temp = System.IO.Path.GetTempFileName();
            File.WriteAllText(temp, value);
            var b = File.ReadAllBytes(temp);
            // Use GZipStream to write compressed bytes to target file.
            var output = System.IO.Path.GetTempFileName();
            using (FileStream f2 = new FileStream(output, FileMode.Create))
            using (GZipStream gz = new GZipStream(f2, CompressionMode.Compress, false))
            {
                gz.Write(b, 0, b.Length);
            }

            var bytes = File.ReadAllBytes(output);
            File.Delete(temp);
            File.Delete(output);
            return bytes;
        }


    }

}


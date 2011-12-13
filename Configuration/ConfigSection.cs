using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace AssetBooster.Configuration
{
    public class AssetBoosterConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// Gets the current AssetBooster configuration in the web.config or whatever.  Returns null if it's not available.
        /// </summary>
        public static AssetBoosterConfigurationSection Current
        {
            get
            {
                return GetCurrent(true);
            }
        }


        /// <summary>
        /// Gets the current AssetBooster configuration in the web.config or whatever.  Returns null if it's not available.
        /// </summary>
        public static AssetBoosterConfigurationSection GetCurrent(bool throwIfMissing)
        {
            var config = (AssetBoosterConfigurationSection)ConfigurationManager.GetSection("assetBooster");
            if (throwIfMissing && config == null)
                throw new ConfigurationErrorsException("<assetBooster> section missing from configuration.  Please add to web.config.");
            return config;
        }

        /// <summary>
        /// A list of combined css and js "libraries".  
        /// </summary>
        [ConfigurationProperty("libraries", IsDefaultCollection = false),
        ConfigurationCollection(typeof(AssetLibraryConfigElement), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public AssetLibraryConfigElementCollection Libraries
        {
            get
            {
                return this["libraries"] as AssetLibraryConfigElementCollection;
            }
        }

        /// <summary>
        /// The asset version used by your app.  This is normally set by the deployment program.
        /// </summary>
        [ConfigurationProperty("version", IsRequired = false)]
        public string Version
        {
            get
            {
                return this["version"] as string;
            }
            set
            {
                this["version"] = value;
            }
        }

        /// <summary>
        /// This is a sub folder within the S3 bucket to place different asset versions.  This is useful when using a single S3 bucket (and hence, CloudFront) for multiple apps.
        /// </summary>
        [ConfigurationProperty("cdnSubDirectory", IsRequired = false)]
        public string CdnSubDirectory
        {
            get
            {
                return this["cdnSubDirectory"] as string;
            }
            set
            {
                this["cdnSubDirectory"] = value;
            }
        }


        /// <summary>
        /// When defined, this indicates a special suffix applied to development bundles & assets.  These are the exact same js and css files that are in the dev version of the site.
        /// This is great for debugging in production.  However, since it's defined by you, and takes a special URL parameter to activate, you can be protected against someone who might want to rip you off.
        /// </summary>
        [ConfigurationProperty("debugKey", IsRequired = false)]
        public string DebugKey
        {
            get
            {
                return this["debugKey"] as string;
            }
        }

        /// <summary>
        /// When true, local files will be used for assets.  Otherwise, the urls for the assets deployed to the CDN will be used.  This is generally true during development and false in production.
        /// </summary>
        [ConfigurationProperty("local", IsRequired = false, DefaultValue = true)]
        public bool Local
        {
            get
            {
                return (bool)this["local"];
            }
        }


        /// <summary>
        /// The http prefix to your CloudFront (or s3) bucket.  You get this from AWS.  Ex: http://d195o39hmhpr24.cloudfront.net/.  This is required and an exception will be thrown if this isn't provided.
        /// </summary>
        [ConfigurationProperty("cdnUrlPrefix", IsRequired = true)]
        public string CdnUrlPrefix
        {
            get
            {
                return this["cdnUrlPrefix"] as string;
            }
        }


        /// <summary>
        /// The http prefix to your CloudFront (or s3) bucket.  You get this from AWS.  Ex: https://d195o39hmhpr24.cloudfront.net/.  By default, this will just substitute https for http from your normal prefix.  This is only used when you use SSL to avoid errors about downloading non-https resources on an https page.  If you don't use https you can ignore this.
        /// </summary>
        [ConfigurationProperty("cdnHttpsUrlPrefix", IsRequired = false)]
        public string CdnHttpsUrlPrefix
        {
            get
            {
                return this["cdnHttpsUrlPrefix"] as string;
            }
        }


    }

    /// <summary>
    /// Contains information about an asset "bundle", which is either a series of javascript or css files.  Each bundle gets deployed as a single url on the CDN but will be treated as a series of local files during development.
    /// </summary>
    public class AssetLibraryConfigElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get
            {
                return this["name"] as string;
            }
        }

        /// <summary>
        /// Indicates which environments this library is included in.  This is useful for something used in dev but not production, such as less.js (when a compiler is used to generate output).
        /// </summary>
        [ConfigurationProperty("includeIn", IsRequired = false, DefaultValue = AssetEnvironment.All)]
        public AssetEnvironment IncludeIn
        {
            get
            {
                var includeIn = this["includeIn"];
                return (AssetEnvironment)includeIn;
            }
        }


        /// <summary>
        /// An explicit list of files to include in the library.
        /// </summary>
        [ConfigurationProperty("files", IsDefaultCollection = false),
        ConfigurationCollection(typeof(FileConfigElement), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public FileConfigElementCollection Files
        {
            get
            {
                return this["files"] as FileConfigElementCollection;
            }
        }

    }

    /// <summary>
    /// Defines a single text-based asset file that is a part of a bundle. 
    /// </summary>
    public class FileConfigElement : ConfigurationElement
    {
        /// <summary>
        /// The relative path of the asset.  
        /// </summary>
        [ConfigurationProperty("path", IsRequired = true)]
        public string Path
        {
            get
            {
                return this["path"] as string;
            }
        }

        /// <summary>
        /// Added for .less files when there is a .css equivalent.  This would point to the .less file.
        /// </summary>
        [ConfigurationProperty("customLocalPath", IsRequired = false)]
        public string CustomLocalPath
        {
            get
            {
                return this["customLocalPath"] as string;
            }
        }

        /// <summary>
        /// Indicates which environments this file is included in.  This is useful for something used in dev but not production, such as less.js (when a compiler is used to generate output).
        /// </summary>
        [ConfigurationProperty("includeIn", IsRequired = false, DefaultValue = AssetEnvironment.All)]
        public AssetEnvironment IncludeIn
        {
            get
            {
                return (AssetEnvironment)this["includeIn"];
            }
        }

        ///// <summary>
        ///// When true, this indicates the file was already minified and no minification or minification detection will be done.  Useful for when, say, there is only a .min.js of a library.
        ///// </summary>
        //[ConfigurationProperty("preMinified", IsRequired = false, DefaultValue = false)]
        //public bool PreMinified
        //{
        //    get
        //    {
        //        return (bool)this["preMinified"];
        //    }
        //}

    }

    /// <summary>
    /// Used to conditionally include files and libraries.
    /// </summary>
    public enum AssetEnvironment
    {
        All,
        Local,
        Production
    }

    public class AssetLibraryConfigElementCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.AddRemoveClearMap;
            }
        }

        public AssetLibraryConfigElement this[int index]
        {
            get { return (AssetLibraryConfigElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);
                BaseAdd(index, value);
            }
        }

        public void Add(AssetLibraryConfigElement element)
        {
            BaseAdd(element);
        }

        public void Clear()
        {
            BaseClear();
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new AssetLibraryConfigElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((AssetLibraryConfigElement)element).Name;
        }

        public void Remove(AssetLibraryConfigElement element)
        {
            BaseRemove(element.Name);
        }

        public void Remove(string name)
        {
            BaseRemove(name);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }
    }

    public class FileConfigElementCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.AddRemoveClearMap;
            }
        }

        public FileConfigElement this[int index]
        {
            get { return (FileConfigElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);
                BaseAdd(index, value);
            }
        }

        public void Add(FileConfigElement element)
        {
            BaseAdd(element);
        }

        public void Clear()
        {
            BaseClear();
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new FileConfigElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((FileConfigElement)element).Path;
        }

        public void Remove(FileConfigElement element)
        {
            BaseRemove(element.Path);
        }

        public void Remove(string name)
        {
            BaseRemove(name);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }
    }


}

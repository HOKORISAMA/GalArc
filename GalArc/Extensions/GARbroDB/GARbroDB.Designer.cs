﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace GalArc.Extensions.GARbroDB {
    using System;
    
    
    /// <summary>
    ///   一个强类型的资源类，用于查找本地化的字符串等。
    /// </summary>
    // 此类是由 StronglyTypedResourceBuilder
    // 类通过类似于 ResGen 或 Visual Studio 的工具自动生成的。
    // 若要添加或移除成员，请编辑 .ResX 文件，然后重新运行 ResGen
    // (以 /str 作为命令选项)，或重新生成 VS 项目。
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class GARbroDB {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal GARbroDB() {
        }
        
        /// <summary>
        ///   返回此类使用的缓存的 ResourceManager 实例。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GalArc.Extensions.GARbroDB.GARbroDB", typeof(GARbroDB).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   重写当前线程的 CurrentUICulture 属性，对
        ///   使用此强类型资源类的所有资源查找执行重写。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   查找类似 Contents: 的本地化字符串。
        /// </summary>
        internal static string DBContents {
            get {
                return ResourceManager.GetString("DBContents", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Failed to read information. 的本地化字符串。
        /// </summary>
        internal static string DBFailedToReadInfos {
            get {
                return ResourceManager.GetString("DBFailedToReadInfos", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Specified file not found. 的本地化字符串。
        /// </summary>
        internal static string DBFileNotFound {
            get {
                return ResourceManager.GetString("DBFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Hash: {0} 的本地化字符串。
        /// </summary>
        internal static string DBHash {
            get {
                return ResourceManager.GetString("DBHash", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 {0}: {1} items 的本地化字符串。
        /// </summary>
        internal static string DBItems {
            get {
                return ResourceManager.GetString("DBItems", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Last modified time: {0} 的本地化字符串。
        /// </summary>
        internal static string DBLastModified {
            get {
                return ResourceManager.GetString("DBLastModified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Size: {0} bytes 的本地化字符串。
        /// </summary>
        internal static string DBSize {
            get {
                return ResourceManager.GetString("DBSize", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Version: {0} 的本地化字符串。
        /// </summary>
        internal static string DBVersion {
            get {
                return ResourceManager.GetString("DBVersion", resourceCulture);
            }
        }
    }
}

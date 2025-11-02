//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace Cliver
{
    public partial class OneDrive
    {
        //void buildPaths(List<Path> paths, Path currentPath, Item currentObject)
        //{
        //    if (currentObject == null || currentObject.ListItem.ParentReference == null)//it is root 'My Drive'
        //    {
        //        paths.Add(currentPath);
        //        return;
        //    }
        //    currentPath = new Path(null, currentObject.ListItem.Name + (currentPath == null ? "" : Path.DirectorySeparatorChar + currentPath));
        //    buildPaths(paths, currentPath, GetItemByLink(currentObject.ListItem.ParentReference.ShareId));
        //}

        //public enum GettingMode
        //{
        //    AlwaysCreateNew,
        //    GetLatestExistingOrCreate,
        //    GetLatestExistingOnly,
        //}
        //Folder getFolder(string parentFolderId, string folderName, GettingMode gettingMode)
        //{
        //    if (parentFolderId == null && string.IsNullOrEmpty(folderName))//root folder
        //        return GetItemByLink(RootFolderId);
        //    if (gettingMode != GettingMode.AlwaysCreateNew)
        //    {
        //        SearchFilter sf = new SearchFilter { IsFolder = true, ParentId = parentFolderId, Name = folderName };
        //        IEnumerable<Item> fs = FindObjects(sf, fields);
        //        Item ff = fs.FirstOrDefault();
        //        if (ff != null)
        //            return ff;
        //        if (gettingMode == GettingMode.GetLatestExistingOnly)
        //            return null;
        //    }
        //    Folder f = new Folder(folderName);
        //    {
        //        Name = folderName,
        //        MimeType = FolderMimeType,
        //        Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
        //    }
        //    ;
        //    var request = Service.Files.Create(f);
        //    request.Fields = getProperFields(fields);
        //    return request.Execute();
        //}

        //public Folder GetFolder(Path folder, GettingMode gettingMode)
        //{
        //    if (string.IsNullOrEmpty(folder.RelativePath))//root folder
        //        return GetObject(folder.BaseObject_ShareId, fields);

        //    if (gettingMode == GettingMode.AlwaysCreateNew
        //        || !cache.Get(folder, out Item @object)
        //        || @object == null && gettingMode == GettingMode.GetLatestExistingOrCreate
        //        )
        //    {
        //        Path folder2;
        //        if (folder.SplitRelativePath(out string rf, out string folderName))
        //        {
        //            Item parentFolder = GetFolder(new Path(folder.BaseObject_ShareId, rf), gettingMode == GettingMode.AlwaysCreateNew ? GettingMode.GetLatestExistingOrCreate : gettingMode, fields);
        //            if (parentFolder == null)
        //                return null;
        //            folder2 = new Path(parentFolder.Id, folderName);
        //        }
        //        else
        //            folder2 = folder;
        //        @object = getFolder(folder2.BaseObject_ShareId, folder2.RelativePath, gettingMode, fields);
        //        cache.Set(folder2, @object);
        //        if (folder2.Key != folder.Key)
        //            cache.Set(folder, @object);
        //    }
        //    return @object;
        //}

        class Cache
        {
            public bool Get(Path path, out Item @object)
            {
                return paths2object.TryGetValue(path.Key, out @object);
            }

            public void Set(Path path, Item @object)
            {
                if (@object != null)
                    paths2object[path.Key] = @object;
            }

            Dictionary<string, Item> paths2object = new Dictionary<string, Item>();
        }
        readonly Cache cache = new Cache();

        public class Path
        {
            /// <summary>
            /// It works for either shared or not shared items.
            /// Expected to work for links of any form:
            /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
            /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
            /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
            /// </summary>
            public string BaseObject_LinkOrEncodedLinkOrShareId { get; private set; }
            public string RelativePath { get; private set; }
            public string Key { get; private set; }

            public const string DirectorySeparatorChar = @"\";

            public override string ToString()
            {
                return Key;
            }

            static public Path Restore(string pathKey)
            {
                try
                {
                    return new Path(pathKey);
                }
                catch
                {
                    return null;
                }
            }

            static public Path Create(string baseObject_LinkOrEncodedLinkOrShareId, string relativePath)
            {
                try
                {
                    return new Path(baseObject_LinkOrEncodedLinkOrShareId, relativePath);
                }
                catch
                {
                    return null;
                }
            }

            public Path(string pathKey)
            {
                if (string.IsNullOrEmpty(pathKey))
                {
                    initialize(null, null);
                    return;
                }
                string[] ps = Regex.Split(pathKey, @"\\\\");
                if (ps.Length < 2)
                {
                    throw new Exception2(nameof(pathKey) + " does not comprise of 2 parts: " + "'" + pathKey + "'");
                    //if (!IsObjectLink(ps[0]))
                    //    throw new Exception2(nameof(pathKey) + " is not a google link: " + "'" + pathKey + "'");
                    //initialize(ps[0], null);
                    //return;
                }
                if (ps.Length > 2)
                    throw new Exception2(nameof(pathKey) + " has more than 2 parts: " + "'" + pathKey + "'");
                initialize(ps[0], ps[1]);
            }

            public Path(string baseObject_LinkOrEncodedLinkOrShareId, string relativePath)
            {
                initialize(baseObject_LinkOrEncodedLinkOrShareId, relativePath);
            }

            public const string RootFolderId = "/";

            void initialize(string baseObject_LinkOrEncodedLinkOrShareId, string relativePath)
            {
                if (!IsLinkOneDrive(baseObject_LinkOrEncodedLinkOrShareId))
                    throw new Exception2("Parameter " + nameof(baseObject_LinkOrEncodedLinkOrShareId) + " is not a OneDrive link: " + "'" + baseObject_LinkOrEncodedLinkOrShareId + "'");
                if (string.IsNullOrEmpty(baseObject_LinkOrEncodedLinkOrShareId))
                {
                    //BaseObject_ShareId = RootFolderId;
                    //BaseObject_LinkOrEncodedLinkOrShareId = RootFolderId;
                }
                else
                {
                    //BaseObject_ShareId = GetObjectId(baseObject_LinkOrEncodedLinkOrShareId);
                    BaseObject_LinkOrEncodedLinkOrShareId = baseObject_LinkOrEncodedLinkOrShareId;
                }
                if (relativePath != null)
                {
                    RelativePath = Regex.Replace(relativePath, @"\\{2,}", @"\").Trim().Trim('\\');
                    //RelativePath_escaped = escapeRelativePath ? GetEscapedPath(RelativePath) : RelativePath;
                }
                //Key = BaseObject_ShareId + @"\\" + RelativePath;
                Key = baseObject_LinkOrEncodedLinkOrShareId + @"\\" + RelativePath;
            }

            public Path GetDescendant(string relativeDescendantPath)
            {
                return new Path(BaseObject_LinkOrEncodedLinkOrShareId, RelativePath + DirectorySeparatorChar + relativeDescendantPath);
            }

            //public bool SplitRelativePath(out string relativeParentFolder, out string folderOrFileName)
            //{
            //    return OneDrive.SplitRelativePath(RelativePath_escaped, out relativeParentFolder, out folderOrFileName);
            //}
        }

        /// <summary>
        /// (!)OneDrive API always tries to url-unescape path arguments.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetEscapedPath(string path)
        {
            //return Regex.Replace(path, @"\%", @"%25");

            if (!path.Contains('%'))//(!)The server always tries to url-decode
                return path;
            string[] ps = path.Split('\\', '/');
            for (int i = 0; i < ps.Length; i++)
                ps[i] = Uri.EscapeDataString(ps[i]);
            return string.Join("\\", ps);
        }

        static public bool SplitRelativePath(string relativePath, out string relativeParentFolder, out string folderOrFileName)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                relativeParentFolder = null;
                folderOrFileName = null;
                return false;
            }
            Match m = Regex.Match(relativePath, @"(.*)[\\\/]+([^\\]+)$");
            if (m.Success)
            {
                relativeParentFolder = m.Groups[1].Value.TrimEnd('\\', '/');
                folderOrFileName = m.Groups[2].Value.TrimEnd('\\', '/');
                return true;
            }
            relativeParentFolder = null;
            folderOrFileName = relativePath.TrimEnd('\\', '/');
            return true;
        }

        /// <summary>
        /// Provides argument for Client.Shares[shareIdOrEncodedSharingUrl].
        /// Expected to work for links of any form:
        /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
        /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
        /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
        /// Encoded link or shareId is retruned unchanged.
        /// </summary>
        /// <param name="linkOrEncodedLinkOrShareId"></param>
        /// <returns></returns>
        static public string GetEncodedLinkOrShareId(string linkOrEncodedLinkOrShareId)
        {
            if (Regex.IsMatch(linkOrEncodedLinkOrShareId, @"^\s*(u|s)\!"))
                return linkOrEncodedLinkOrShareId;
            string base64Value = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(linkOrEncodedLinkOrShareId));
            return "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
        }

        public static string GetParentPath(string relativePath, bool removeTrailingSeparator = true)
        {
            string fd = Regex.Replace(relativePath, @"[^\\\/]*$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (removeTrailingSeparator)
                fd = fd.TrimEnd('\\', '/');
            return fd;
        }

        /// <summary>
        /// Provides argument for Client.Shares[shareIdOrEncodedSharingUrl].
        /// Expected to work for links of any form:
        /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
        /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
        /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
        /// Encoded link or shareId is retruned unchanged.
        /// </summary>
        /// <param name="linkOrEncodedLinkOrShareId"></param>
        /// <returns></returns>
        public static bool IsLinkOneDrive(string linkOrEncodedLinkOrShareId)
        {
            return Regex.IsMatch(linkOrEncodedLinkOrShareId, @"^\s*(https\://(onedrive\.live\.com|1drv\.ms)[\/\?]|u!)", RegexOptions.IgnoreCase);
        }
    }
}
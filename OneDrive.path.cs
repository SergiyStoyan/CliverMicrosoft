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
        void buildPaths(List<Path> paths, Path currentPath, Item currentObject)
        {
            if (currentObject == null || currentObject.ListItem.ParentReference == null)//it is root 'My Drive'
            {
                paths.Add(currentPath);
                return;
            }
            currentPath = new Path(null, currentObject.ListItem.Name + (currentPath == null ? "" : Path.DirectorySeparatorChar + currentPath));
            buildPaths(paths, currentPath, GetItemByLink(currentObject.ListItem.ParentReference.ShareId));
        }

        public enum GettingMode
        {
            AlwaysCreateNew,
            GetLatestExistingOrCreate,
            GetLatestExistingOnly,
        }
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
            public string BaseObject_LinkOrEncodedLinkOrShareId { get; private set; }
            //public string BaseObject_ShareId { get; private set; }
            public string RelativePath { get; private set; }
            public string UnescapedRelativePath { get; private set; }
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
                //if (relativeFolderPath.Contains(DirectorySeparatorChar))
                //    throw new Exception2(nameof(GoogleDrive.Path) + " cannot contain " + DirectorySeparatorChar);
                if (!string.IsNullOrEmpty(baseObject_LinkOrEncodedLinkOrShareId) && Regex.IsMatch(baseObject_LinkOrEncodedLinkOrShareId, @"\s|\\"))
                    throw new Exception2("Parameter " + nameof(baseObject_LinkOrEncodedLinkOrShareId) + " is not a google link: " + "'" + baseObject_LinkOrEncodedLinkOrShareId + "'");
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
                    UnescapedRelativePath = Regex.Replace(relativePath, @"\\{2,}", @"\").Trim().Trim('\\');
                    RelativePath = GetEscapedPath(UnescapedRelativePath);
                }
                //Key = BaseObject_ShareId + @"\\" + RelativePath;
                Key = baseObject_LinkOrEncodedLinkOrShareId + @"\\" + RelativePath;
            }

            public Path GetDescendant(string relativeDescendantPath)
            {
                //return new Path(BaseObject_ShareId, RelativePath + DirectorySeparatorChar + relativeDescendantPath);
                return new Path(BaseObject_LinkOrEncodedLinkOrShareId, RelativePath + DirectorySeparatorChar + relativeDescendantPath);
            }

            public bool SplitRelativePath(out string relativeParentFolder, out string folderOrFileName)
            {
                return OneDrive.SplitRelativePath(RelativePath, out relativeParentFolder, out folderOrFileName);
            }
        }

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

        //public string GetLink(Path folderOrFile)
        //{
        //    if (IsObjectLink(folderOrFile.BaseObject_LinkOrEncodedLinkOrShareId))
        //        return folderOrFile.BaseObject_LinkOrEncodedLinkOrShareId;

        //    return getObject(folderOrFile)?.Id;
        //}

        //Item getObject(Path folderOrFile, string fields = "id, webViewLink")
        //{
        //    if (!cache.Get(folderOrFile, out Item @object))
        //    {
        //        if (folderOrFile.SplitRelativePath(out string rf, out string folderOrFileName))
        //        {
        //            Item parentFolder = GetFolder(new Path(folderOrFile.BaseObject_ShareId, rf), GettingMode.GetLatestExistingOnly, fields);
        //            if (parentFolder == null)
        //                return null;
        //            @object = FindObjects(new SearchFilter { Name = folderOrFileName, ParentId = parentFolder.Id }, fields).FirstOrDefault();
        //        }
        //        else
        //            @object = GetObject(folderOrFile.BaseObject_ShareId, fields);
        //        cache.Set(folderOrFile, @object);
        //    }
        //    return @object;
        //}

        //public Item GetItem(Path item)
        //{
        //    if (!cache.Get(item, out Item @object))
        //    {
        //        if (item.SplitRelativePath(out string parentRelativeFolderPath, out string fileName))
        //        {
        //            Item parentFolder = GetFolder(new Path(item.BaseObject_ShareId, parentRelativeFolderPath), GettingMode.GetLatestExistingOnly);
        //            if (parentFolder == null)
        //                return null;
        //            SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = parentFolder.Id, Name = fileName };
        //            IEnumerable<Item> fs = FindObjects(sf, fields);
        //            @object = fs.FirstOrDefault();
        //        }
        //        else
        //            @object = GetObject(file.BaseObject_ShareId, fields);
        //        cache.Set(file, @object);
        //    }
        //    return @object;
        //}

        //public File GetFile(Path remoteFile, bool createIfNotExists)
        //{
        //    return File.New(this, remoteFile, createIfNotExists);
        //}

        //public File UploadFile(string localFile, Path remotefile, string contentType = null, bool updateExisting = true)
        //{
        //    if (!remotefile.SplitRelativePath(out string remoteRelativeFolderPath, out string fileName)
        //        && IsObjectLink(remotefile.BaseObject_LinkOrEncodedLinkOrShareId)
        //        )
        //        return UpdateFile(localFile, remotefile.BaseObject_ShareId, PathRoutines.GetFileName(localFile), contentType, fields);

        //    string folderId = GetFolder(new Path(remotefile.BaseObject_ShareId, remoteRelativeFolderPath), GettingMode.GetLatestExistingOrCreate).Id;

        //    if (string.IsNullOrWhiteSpace(fileName))
        //        fileName = PathRoutines.GetFileName(localFile);
        //    File file = new File
        //    {
        //        Name = fileName,
        //        //MimeType = getMimeType(localFile), 
        //        //Description=,
        //    };
        //    using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
        //    {
        //        if (updateExisting)
        //        {
        //            SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = folderId, Name = file.Name };
        //            IEnumerable<Item> fs = FindObjects(sf, fields);
        //            Item f = fs.FirstOrDefault();
        //            if (f != null)
        //            {
        //                FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, f.Id, fileStream, contentType != null ? contentType : getMimeType(localFile));
        //                updateMediaUpload.Fields = getProperFields(fields);
        //                Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
        //                if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
        //                    throw new Exception("Uploading file failed.", uploadProgress.Exception);
        //                if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
        //                    throw new Exception("Uploading file has not been completed.");
        //                return updateMediaUpload.ResponseBody;
        //            }
        //        }
        //        {
        //            file.Parents = new List<string>
        //            {
        //                folderId
        //            };
        //            FilesResource.CreateMediaUpload createMediaUpload = Service.Files.Create(file, fileStream, contentType != null ? contentType : getMimeType(localFile));
        //            createMediaUpload.Fields = getProperFields(fields);
        //            Google.Apis.Upload.IUploadProgress uploadProgress = createMediaUpload.Upload();
        //            if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
        //                throw new Exception("Uploading file failed.", uploadProgress.Exception);
        //            if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
        //                throw new Exception("Uploading file has not been completed.");
        //            return createMediaUpload.ResponseBody;
        //        }
        //    }
        //}
        //static string getMimeType(string fileName)
        //{
        //    string mimeType = "application/unknown";
        //    string ext = System.IO.Path.GetExtension(fileName).ToLower();
        //    Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
        //    if (regKey != null && regKey.GetValue("Content Type") != null)
        //        mimeType = regKey.GetValue("Content Type").ToString();
        //    return mimeType;
        //}

        //public File DownloadFile(Path remoteFile, string localFile)
        //{
        //    File file = GetFile(remoteFile);
        //    if (file == null)
        //        return null;
        //    DownloadFile(file.Id, localFile);
        //    return file;
        //}
    }
}
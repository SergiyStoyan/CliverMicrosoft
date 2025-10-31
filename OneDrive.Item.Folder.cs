//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Cliver
{
    public partial class OneDrive
    {
        public class Folder : Item
        {
            public static Folder Get(OneDrive oneDrive, Path folder, bool createIfNotExists)
            {
                if (string.IsNullOrWhiteSpace(folder.Key))
                    throw new Exception("Path is empty.");

                Item bi;
                if (folder.BaseObject_LinkOrEncodedLinkOrShareId != null)
                {
                    bi = oneDrive.GetItemByLink(folder.BaseObject_LinkOrEncodedLinkOrShareId);
                    if (bi == null)
                        return null;
                    if (folder.RelativePath == null)
                    {
                        if (bi is Folder)
                            return (Folder)bi;
                        throw new Exception("Path points to not a folder: " + folder);
                    }
                    if (!(bi is Folder))
                        throw new Exception("Base object link points to not a folder: " + folder.BaseObject_LinkOrEncodedLinkOrShareId);

                    return ((Folder)bi).GetFolder(folder.RelativePath, createIfNotExists);
                }

                bi = oneDrive.GetItemByPath(Path.RootFolderId);
                if (bi == null)
                    throw new Exception("Could not get the root folder.");
                return ((Folder)bi).GetFolder(folder.RelativePath, createIfNotExists);
            }

            public Folder GetFolder(string relativePath, bool createIfNotExists)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new Exception("Path is empty.");

                DriveItem di = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.ItemWithPath(relativePath).Request().GetAsync();
                }).Result;

                if (di != null)
                {
                    Item item = New(OneDrive, di);
                    if (item is Folder)
                        return (Folder)item;
                    throw new Exception("Path points to not a folder: " + relativePath);
                }
                if (!createIfNotExists)
                    return null;

                Match m = Regex.Match(relativePath, @"(.*)[\\\/]+([^\\]+)$");
                if (!m.Success)
                {
                    di = new DriveItem
                    {
                        Name = relativePath,
                        Folder = new Microsoft.Graph.Folder
                        {
                        },
                        AdditionalData = new Dictionary<string, object>()
                            {
                                {"@microsoft.graph.conflictBehavior", "rename"}
                            }
                    };
                    DriveItem cdi = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.Children.Request().AddAsync(di);
                    }).Result;
                    return new Folder(OneDrive, cdi);
                }
                return GetFolder(m.Groups[1].Value, createIfNotExists).GetFolder(m.Groups[2].Value, createIfNotExists);
            }

            //public File Get(string relativePath)
            //{
            //    if (string.IsNullOrWhiteSpace(relativePath))
            //        throw new Exception("Path is empty.");

            //    DriveItem di = Task.Run(() =>
            //    {
            //        return DriveItemRequestBuilder.ItemWithPath(relativePath).Request().GetAsync();
            //    }).Result;

            //    if (di != null)
            //    {
            //        Item item = New(OneDrive, di);
            //        if (item is File)
            //            return (File)item;
            //        throw new Exception("Path points to not a file: " + relativePath);
            //    }
            //    return null;
            //}

            internal Folder(OneDrive oneDrive, DriveItem driveItem) : base(oneDrive, driveItem)
            {
                //if (driveItem.Folder == null)
                //    throw new Exception("");
            }

            public File UploadFile(string localFile, string relativePath = null /*, bool replace = true*/)
            {
                if (relativePath == null)
                    relativePath = PathRoutines.GetFileName(localFile);
                string escapedPath = GetEscapedPath(relativePath);
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem driveItem = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.ItemWithPath(escapedPath).Content.Request().PutAsync<DriveItem>(s);
                    }).Result;
                    return new File(OneDrive, driveItem);
                }
            }

            IEnumerable<DriveItem> getChildren()
            {
                for (IDriveItemChildrenCollectionRequest r = OneDrive.Client.Me.Drives[DriveId].Items[ItemId].Children.Request(); r != null; r = DriveItem.Children.NextPageRequest)
                {
                    DriveItem.Children = Task.Run(() =>
                    {
                        return r.GetAsync();
                    }).Result;
                    foreach (DriveItem child in DriveItem.Children)
                        yield return child;
                }
            }

            public IEnumerable<Item> GetChildren()
            {
                return getChildren()?.Select(a => New(OneDrive, a));
            }

            public IEnumerable<File> GetFiles()
            {
                return getChildren().Where(a => a.File != null).Select(a => new File(OneDrive, a));
            }

            public IEnumerable<Folder> GetFolders()
            {
                return getChildren().Where(a => a.Folder != null).Select(a => new Folder(OneDrive, a));
            }

            public File GetFile(string relativePath)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new Exception("Path is empty.");

                string escapedPath = GetEscapedPath(relativePath);
                DriveItem di = null;
                var task = Task.Run(() =>
                {
                    //return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].ItemWithPath(escapedPath).Request().GetAsync();
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].ItemWithPath(escapedPath).Request().GetAsync();
                });
                try
                {
                    di = task.GetAwaiter().GetResult();
                }
                catch (Microsoft.Graph.ServiceException e)
                {
                    if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return null;
                }
                if (di.File == null)
                    throw new Exception("Item [path='" + relativePath + "'] exists but it is not a file.");
                return new File(OneDrive, di);
            }

            //public Folder GetFolder(string relativePath, bool createIfNotExists)
            //{
            //    string escapedPath = GetEscapedPath(relativePath);
            //    DriveItem di = null;
            //    var task = Task.Run(() =>
            //    {
            //        return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].ItemWithPath(escapedPath).Request().GetAsync();
            //    });
            //    try
            //    {
            //        di = task.GetAwaiter().GetResult();
            //    }
            //    catch (Microsoft.Graph.ServiceException e)
            //    {
            //        if (e.StatusCode != System.Net.HttpStatusCode.NotFound)
            //            throw;
            //    }
            //    if (di != null)
            //    {
            //        if (di.Folder == null)
            //            throw new Exception("Item [name='" + relativePath + "'] exists but it is not a folder.");
            //        return new Folder(OneDrive, di);
            //    }
            //    if (!createIfNotExists)
            //        return null;
            //    di = new DriveItem
            //    {
            //        Name = escapedPath,
            //        Folder = new Microsoft.Graph.Folder
            //        {
            //        },
            //        AdditionalData = new Dictionary<string, object>()
            //        {
            //            {"@microsoft.graph.conflictBehavior", "rename"}
            //        }
            //    };
            //    DriveItem driveItem = Task.Run(() =>
            //    {
            //        return DriveItemRequestBuilder.Children.Request().AddAsync(di);
            //    }).Result;
            //    return new Folder(OneDrive, driveItem);
            //}
        }
    }
}
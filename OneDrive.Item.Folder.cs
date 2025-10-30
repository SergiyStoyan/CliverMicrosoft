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
            public static Folder New(OneDrive oneDrive, string remoteFolder, bool createIfNotExists)
            {
                Item item = oneDrive.GetItemByPath(remoteFolder);
                if (item != null)
                {
                    if (item is Folder)
                        return (Folder)item;
                    throw new Exception("Remote path points to not a folder: " + remoteFolder);
                }
                if (!createIfNotExists)
                    return null;

                Match m = Regex.Match(remoteFolder, @"(?'ParentFolder'.*)[\\\/]+(?'Name'.*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success)
                    throw new Exception("Remote folder path could not be separated: " + remoteFolder);

                Folder parentFolder = New(oneDrive, m.Groups["ParentFolder"].Value, true);
                DriveItem di = new DriveItem
                {
                    Name = m.Groups["Name"].Value,
                    Folder = new Microsoft.Graph.Folder
                    {
                    },
                    AdditionalData = new Dictionary<string, object>()
                    {
                        {"@microsoft.graph.conflictBehavior", "rename"}
                    }
                };
                DriveItem driveItem = Task.Run(() =>
                {
                    return parentFolder.DriveItemRequestBuilder.Children.Request().AddAsync(di);
                }).Result;
                return new Folder(oneDrive, driveItem);
            }

            public static string GetParentPath(string remotePath, bool removeTrailingSeparator = true)
            {
                string fd = Regex.Replace(remotePath, @"[^\\\/]*$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (removeTrailingSeparator)
                    fd = fd.TrimEnd('\\', '/');
                return fd;
            }

            internal Folder(OneDrive oneDrive, DriveItem driveItem) : base(oneDrive, driveItem)
            {
                //if (driveItem.Folder == null)
                //    throw new Exception("");
            }

            public File UploadFile(string localFile, string remoteFileRelativePath = null /*, bool replace = true*/)
            {
                if (remoteFileRelativePath == null)
                    remoteFileRelativePath = PathRoutines.GetFileName(localFile);
                string escapedPath = GetPathEscaped(remoteFileRelativePath);
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

            public File GetFile(string fileName)
            {
                string escapedPath = GetPathEscaped(fileName);
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
                    throw new Exception("Item [name='" + fileName + "'] exists and it is not a file.");
                return new File(OneDrive, di);
            }

            public Folder GetFolder(string folderName, bool createIfNotExists)
            {
                string escapedPath = GetPathEscaped(folderName);
                DriveItem di = null;
                var task = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].ItemWithPath(escapedPath).Request().GetAsync();
                });
                try
                {
                    di = task.GetAwaiter().GetResult();
                }
                catch (Microsoft.Graph.ServiceException e)
                {
                    if (e.StatusCode != System.Net.HttpStatusCode.NotFound)
                        throw;
                }
                if (di != null)
                {
                    if (di.Folder == null)
                        throw new Exception("Item [name='" + folderName + "'] exists and it is not a folder.");
                    return new Folder(OneDrive, di);
                }
                if (!createIfNotExists)
                    return null;
                di = new DriveItem
                {
                    Name = escapedPath,
                    Folder = new Microsoft.Graph.Folder
                    {
                    },
                    AdditionalData = new Dictionary<string, object>()
                    {
                        {"@microsoft.graph.conflictBehavior", "rename"}
                    }
                };
                DriveItem driveItem = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.Children.Request().AddAsync(di);
                }).Result;
                return new Folder(OneDrive, driveItem);
            }
        }
    }
}
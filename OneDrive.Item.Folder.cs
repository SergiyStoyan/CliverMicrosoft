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
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem driveItem = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.ItemWithPath(remoteFileRelativePath).Content.Request().PutAsync<DriveItem>(s);
                    }).Result;
                    return new File(OneDrive, driveItem);
                }
            }

            public List<Item> GetChildren()
            {
                DriveItem.Children = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].Children.Request().GetAsync();
                }).Result;

                return DriveItem.Children?.Select(a => New(OneDrive, a)).ToList();
            }

            public List<File> GetFiles()
            {
                DriveItem.Children = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].Children.Request().GetAsync();
                }).Result;

                return DriveItem.Children.Where(a => a.File != null).Select(a => new File(OneDrive, a)).ToList();
            }

            public List<Folder> GetFolders()
            {
                DriveItem.Children = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].Children.Request().GetAsync();
                }).Result;

                return DriveItem.Children.Where(a => a.Folder != null).Select(a => new Folder(OneDrive, a)).ToList();
            }

            public File GetFile(string fileName)
            {
                DriveItem di = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].ItemWithPath(fileName).Request().GetAsync();
                }).Result;

                if (di == null)
                    return null;
                if (di.File == null)
                    throw new Exception("Item [name='" + fileName + "'] is not a file.");
                return new File(OneDrive, di);
            }

            public Folder GetFolder(string folderName, bool createIfNotExists)
            {
                DriveItem di = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].ItemWithPath(folderName).Request().GetAsync();
                }).Result;

                if (di != null)
                {
                    if (di.Folder == null)
                        throw new Exception("Item [name='" + folderName + "'] is not a folder.");
                    return new Folder(OneDrive, di);
                }
                if (!createIfNotExists)
                    return null;

                di = new DriveItem
                {
                    Name = folderName,
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
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.Graph.Groups.Item.MembersWithLicenseErrors;

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
                    if (folder.RelativePath_escaped == null)
                    {
                        if (bi is Folder)
                            return (Folder)bi;
                        throw new Exception("Path points not to a folder: " + folder);
                    }
                    if (!(bi is Folder))
                        throw new Exception("Base object link points not to a folder: " + folder.BaseObject_LinkOrEncodedLinkOrShareId);

                    return ((Folder)bi).GetFolder(folder.RelativePath_escaped, createIfNotExists);
                }

                bi = oneDrive.GetItemByPath(Path.RootFolderId);
                if (bi == null)
                    throw new Exception("Could not get the root folder.");
                return ((Folder)bi).GetFolder(folder.RelativePath_escaped, createIfNotExists);
            }

            public Folder GetFolder(string relativePath, bool createIfNotExists)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new Exception("Path is empty.");

                DriveItem di = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.ItemWithPath(relativePath).GetAsync();
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
                        Folder = new Microsoft.Graph.Models.Folder
                        {
                        },
                        AdditionalData = new Dictionary<string, object>()
                            {
                                {"@microsoft.graph.conflictBehavior", "rename"}
                            }
                    };
                    DriveItem cdi = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.Children.PostAsync(di);
                    }).Result;
                    return new Folder(OneDrive, cdi);
                }
                return GetFolder(m.Groups[1].Value, createIfNotExists).GetFolder(m.Groups[2].Value, createIfNotExists);
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
                        return DriveItemRequestBuilder.ItemWithPath(remoteFileRelativePath).Content.PutAsync(s);
                    }).Result;
                    return new File(OneDrive, driveItem);
                }
            }

            public List<Item> GetChildren(string filter = null)
            {
                var i = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.Children.GetAsync(
                        rc =>
                        {
                            rc.QueryParameters.Filter = filter;//https://learn.microsoft.com/en-us/graph/filter-query-parameter?tabs=csharp
                        }
                        );
                }).Result.Value;

                return i?.Select(a => New(OneDrive, a)).ToList();
            }

            public List<File> GetFiles(string filter = null)
            {
                var i = Task.Run(() =>
                {
                    string f = "file ne null";
                    if (filter != null)
                        f = "(" + f + ") and (" + filter + ")";
                    return DriveItemRequestBuilder.Children.GetAsync(
                        rc =>
                        {
                            rc.QueryParameters.Filter = f;//https://learn.microsoft.com/en-us/graph/filter-query-parameter?tabs=csharp
                        }
                    );
                }).Result.Value;

                return i?.Select(a => (File)New(OneDrive, a)).ToList();
                //return DriveItem.Children.Where(a => a.File != null).Select(a => new File(OneDrive, a)).ToList();
            }

            public List<Folder> GetFolders(string filter = null)
            {
                var i = Task.Run(() =>
                {
                    string f = "folder ne null";
                    if (filter != null)
                        f = "(" + f + ") and (" + filter + ")";
                    return DriveItemRequestBuilder.Children.GetAsync(
                        rc =>
                        {
                            rc.QueryParameters.Filter = f;//https://learn.microsoft.com/en-us/graph/filter-query-parameter?tabs=csharp
                        }
                        );
                }).Result.Value;

                return i?.Select(a => (Folder)New(OneDrive, a)).ToList();
                //return DriveItem.Children.Where(a => a.Folder != null).Select(a => new Folder(OneDrive, a)).ToList();
            }

            public File GetFile(string remoteFileRelativePath)
            {
                DriveItem di = null;
                var task = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.ItemWithPath(remoteFileRelativePath).GetAsync();
                });
                try
                {
                    di = task.GetAwaiter().GetResult();
                }
                catch (Microsoft.Graph.ServiceException e)
                {
                    if (e.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound)
                        return null;
                }
                if (di.File == null)
                    throw new Exception("Item [remoteFileRelativePath='" + remoteFileRelativePath + "'] is not a file.");
                return new File(OneDrive, di);
            }

            public Folder GetFolder2(string remoteFolderRelativePath, bool createIfNotExists)
            {
                DriveItem di = null;
                var task = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.ItemWithPath(remoteFolderRelativePath).GetAsync();
                });
                try
                {
                    di = task.GetAwaiter().GetResult();
                }
                catch (Microsoft.Graph.ServiceException e)
                {
                    if (e.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound)
                        return null;
                }
                if (di != null)
                {
                    if (di.Folder == null)
                        throw new Exception("Item [remoteFolderRelativePath='" + remoteFolderRelativePath + "'] is not a folder.");
                    return new Folder(OneDrive, di);
                }
                if (!createIfNotExists)
                    return null;

                throw new Exception("TBD");
            }
        }
    }
}
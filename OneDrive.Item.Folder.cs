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
            async public static Task<Folder> GetAsync(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return await oneDrive.GetFolderAsync(linkOrEncodedLinkOrShareId);
            }
            public static Folder Get(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return RunSync(() => GetAsync(oneDrive, linkOrEncodedLinkOrShareId));
            }

            async public Task<Item> GetItemAsync(string relativePath)
            {
                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                DriveItem di = null;
                try
                {
                    di = await DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync();
                }
                catch (Exception e)
                {
                    for (; e != null; e = e.InnerException)
                        if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException es && es?.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound)
                            return null;
                    throw;
                }
                return New(OneDrive, di);
            }
            public Item GetItem(string relativePath)
            {
                return RunSync(() => GetItemAsync(relativePath));
            }

            async public Task<File> GetFileAsync(string relativePath)
            {
                Item i = await GetItemAsync(relativePath);
                if (i == null)
                    return null;
                if (i is File)
                    return (File)i;
                throw new Exception("Item[relativePath='" + relativePath + "'] is not a file.");
            }
            public File GetFile(string relativePath)
            {
                return RunSync(() => GetFileAsync(relativePath));
            }

            async public Task<Folder> GetFolderAsync(string relativePath, bool createIfNotExists)
            {
                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                DriveItem di = await getAsync();
                async Task<DriveItem> getAsync()
                {
                    try
                    {
                        return await DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync();
                    }
                    catch (Exception e)
                    {
                        for (; e != null; e = e.InnerException)
                            if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException es && es?.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound)
                                return null;
                        throw;
                    }
                }
                if (di != null)
                {
                    Item item = New(OneDrive, di);
                    if (item is Folder)
                        return (Folder)item;
                    throw new Exception("Path points to not a folder: " + relativePath);
                }
                if (!createIfNotExists)
                    return null;

                if (!SplitPath(relativePath, out string parentFolder, out string itemName))
                {
                    di = new DriveItem
                    {
                        Name = escapedRelativePath,
                        Folder = new Microsoft.Graph.Models.Folder
                            {
                            },
                        AdditionalData = new Dictionary<string, object>()
                            {
                                {"@microsoft.graph.conflictBehavior", "rename"}
                            }
                    };
                    DriveItem cdi = await DriveItemRequestBuilder.Children.PostAsync(di);
                    return new Folder(OneDrive, cdi);
                }
                return await (await GetFolderAsync(parentFolder, createIfNotExists))?.GetFolderAsync(itemName, createIfNotExists);
            }
            public Folder GetFolder(string relativePath, bool createIfNotExists)
            {
                return RunSync(() => GetFolderAsync(relativePath, createIfNotExists));
            }

            internal Folder(OneDrive oneDrive, DriveItem driveItem) : base(oneDrive, driveItem)
            {
                //if (driveItem.Folder == null)
                //    throw new Exception("");
            }

            async public Task<File> UploadFileAsync(string localFile, string remoteFileRelativePath = null /*, bool replace = true*/)
            {
                if (remoteFileRelativePath == null)
                    remoteFileRelativePath = PathRoutines.GetFileName(localFile);

                string escapedRelativePath = GetEscapedPath(remoteFileRelativePath);//(!)the API always tries to unescape

                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem driveItem = await DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).Content.PutAsync(s);
                    return new File(OneDrive, driveItem);
                }
            }
            public File UploadFile(string localFile, string remoteFileRelativePath = null /*, bool replace = true*/)
            {
                return RunSync(() => UploadFileAsync(localFile, remoteFileRelativePath));
            }

            async public Task DownloadFileAsync(string remoteFileRelativePath, string localFile)
            {
                string escapedRelativePath = GetEscapedPath(remoteFileRelativePath);//(!)the API always tries to unescape

                using (Stream s = await DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).Content.GetAsync())
                {
                    using (var fileStream = System.IO.File.Create(localFile))
                    {
                        //s.Seek(0, SeekOrigin.Begin);!!!not supported
                        s.CopyTo(fileStream);
                    }
                }
            }
            public void DownloadFile(string remoteFileRelativePath, string localFile)
            {
                RunSync(() => UploadFileAsync(remoteFileRelativePath, localFile));
            }

            async Task<List<DriveItem>> GetChildDriveItemsAsync(string filter)
            {
                return (await DriveItemRequestBuilder.Children.GetAsync(
                    rc =>
                    {
                        rc.Headers["Prefer"] = new string[] { "apiversion = 2.1", //supports Filter
                                "TryFilterLastModifiedDateTimeTimeWarningMayFailRandomly", //supports filtering by lastModifiedDateTime
                        };
                        rc.QueryParameters.Filter = filter;//https://learn.microsoft.com/en-us/graph/filter-query-parameter?tabs=csharp
                    }
                )).Value;
            }
            public IEnumerable<DriveItem> GetChildDriveItems(string filter)
            {
                return RunSync(() => GetChildDriveItemsAsync(filter));
            }

            async public Task<List<Item>> GetChildrenAsync(string filter = null)
            {
                return (await GetChildDriveItemsAsync(filter))?.Select(a => New(OneDrive, a)).ToList();
            }
            public List<Item> GetChildren(string filter = null)
            {
                return RunSync(() => GetChildrenAsync(filter));
            }

            async public Task<List<File>> GetFilesAsync(string filter = null)
            {
                string f = "file ne null";
                if (filter != null)
                    f = "(" + f + ") and (" + filter + ")";
                return (await GetChildDriveItemsAsync(f))?.Select(a => (File)New(OneDrive, a)).ToList();
            }
            public List<File> GetFiles(string filter = null)
            {
                return RunSync(() => GetFilesAsync(filter));
            }

            async public Task<List<Folder>> GetFoldersAsync(string filter = null)
            {
                string f = "folder ne null";
                if (filter != null)
                    f = "(" + f + ") and (" + filter + ")";
                return (await GetChildDriveItemsAsync(f))?.Select(a => (Folder)New(OneDrive, a)).ToList();
            }
            public List<Folder> GetFolders(string filter = null)
            {
                return RunSync(() => GetFoldersAsync(filter));
            }
        }
    }
}
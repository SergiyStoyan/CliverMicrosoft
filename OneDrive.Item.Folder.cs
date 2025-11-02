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
            public static Folder Get(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return oneDrive.GetFolder(linkOrEncodedLinkOrShareId);
            }

            public Item GetItem(string relativePath)
            {
                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                DriveItem di = null;
                try
                {
                    di = DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync().Result;
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

            public File GetFile(string relativePath)
            {
                Item i = GetItem(relativePath);
                if(i == null)
                    return null;
                if (i is File)
                    return (File)i;
                throw new Exception("Item[relativePath='" + relativePath + "'] is not a file.");
            }

            public Folder GetFolder(string relativePath, bool createIfNotExists)
            {
                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                DriveItem di = get();
                DriveItem get()
                {
                    try
                    {
                        return DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync().Result;
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
                    DriveItem cdi = DriveItemRequestBuilder.Children.PostAsync(di).Result;
                    return new Folder(OneDrive, cdi);
                }
                return GetFolder(parentFolder, createIfNotExists).GetFolder(itemName, createIfNotExists);
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

                string escapedRelativePath = GetEscapedPath(remoteFileRelativePath);//(!)the API always tries to unescape

                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem driveItem = DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).Content.PutAsync(s).Result;
                    return new File(OneDrive, driveItem);
                }
            }

            public void DownloadFile(string remoteFileRelativePath, string localFile)
            {
                string escapedRelativePath = GetEscapedPath(remoteFileRelativePath);//(!)the API always tries to unescape

                using (Stream s = DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).Content.GetAsync().Result)
                {
                    using (var fileStream = System.IO.File.Create(localFile))
                    {
                        //s.Seek(0, SeekOrigin.Begin);!!!not supported
                        s.CopyTo(fileStream);
                    }
                }
            }

            public List<Item> GetChildren(string filter = null)
            {
                return getChildren(filter)?.Select(a => New(OneDrive, a)).ToList();
            }

            IEnumerable<DriveItem> getChildren(string filter)
            {
                return DriveItemRequestBuilder.Children.GetAsync(
                    rc =>
                    {
                        rc.Headers["Prefer"] = new string[] { "apiversion = 2.1" };//supports Filter
                        rc.QueryParameters.Filter = filter;//https://learn.microsoft.com/en-us/graph/filter-query-parameter?tabs=csharp
                    }
                ).Result.Value;
            }

            public List<File> GetFiles(string filter = null)
            {
                string f = "file ne null";
                if (filter != null)
                    f = "(" + f + ") and (" + filter + ")";
                return getChildren(f)?.Select(a => (File)New(OneDrive, a)).ToList();
            }

            public List<Folder> GetFolders(string filter = null)
            {
                string f = "folder ne null";
                if (filter != null)
                    f = "(" + f + ") and (" + filter + ")";
                return getChildren(f)?.Select(a => (Folder)New(OneDrive, a)).ToList();
            }
        }
    }
}
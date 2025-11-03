//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph.Models;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.Graph.Drives.Item.Items;
using Microsoft.Graph.Drives.Item.Items.Item.CreateLink;
using Microsoft.Graph;

namespace Cliver
{
    public partial class OneDrive
    {
        abstract public class Item
        {
            public static Item New(OneDrive oneDrive, DriveItem driveItem)
            {
                if (driveItem == null)
                    return null;
                if (driveItem.File != null)
                    return new File(oneDrive, driveItem);
                if (driveItem.Folder != null)
                    return new Folder(oneDrive, driveItem);
                throw new Exception("Unknown DriveItem object type: " + driveItem.ToStringByJson());
            }

            static public Item Get(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return oneDrive.GetItem(linkOrEncodedLinkOrShareId);
            }

            protected Item(OneDrive oneDrive, DriveItem driveItem)
            {
                OneDrive = oneDrive;
                DriveItem = driveItem;
                ItemId = DriveItem.Id;
                set();
            }

            void set()
            {
                Match m = Regex.Match(ItemId, @"(.*)\!");//on personal OneDrive DriveItem.Id contains driveId
                if (m.Success)
                    DriveId = m.Groups[1].Value;
                else
                    DriveId = DriveItem.ParentReference?.DriveId;//!!!does not work for Root and such
                if (DriveId == null)
                    throw new Exception("Could not get DriveId from DriveItem:\r\n" + DriveItem.ToStringByJson());
            }

            public OneDrive OneDrive { get; private set; }

            public string DriveId { get; private set; }

            public string ItemId { get; private set; }

            public DriveItem DriveItem
            {
                get
                {
                    if (driveItem == null)
                        driveItem = GetDriveItem();
                    return driveItem;
                }
                set
                {
                    driveItem = value;
                }
            }
            DriveItem driveItem = null;

            public Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder DriveItemRequestBuilder
            {
                get
                {
                    if (itemRequestBuilder == null)
                        itemRequestBuilder = OneDrive.Client.Drives[DriveId].Items[ItemId];
                    return itemRequestBuilder;
                }
            }
            Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder itemRequestBuilder;

            public enum LinkRoles
            {
                view, edit, embed
            }

            public enum LinkScopes
            {
                anonymous, organization
            }

            public SharingLink GetLink(LinkRoles linkRole, string password = null, DateTimeOffset? expirationDateTime = null, LinkScopes? linkScopes = null, string message = null, bool? retainInheritedPermissions = null)
            {
                lock (this)
                {
                    var requestBody = new CreateLinkPostRequestBody
                    {
                        Type = linkRole.ToString(),
                        Password = password,
                        Scope = linkScopes.ToString(),
                        RetainInheritedPermissions = retainInheritedPermissions,
                    };
                    Permission p = Task.Run(() => { return DriveItemRequestBuilder.CreateLink.PostAsync(requestBody); }).Result;
                    return p.Link;
                }
            }

            public string WebViewLink
            {
                get
                {
                    if (viewLink == null)
                        viewLink = GetLink(LinkRoles.view).WebUrl;
                    return viewLink;
                }
            }
            string viewLink;

            public DriveItem GetDriveItem(string[] select = null, string[] expand = null/*, string selectWithoutPrefix = null, string expandWithoutPrefix = null*/)
            {
                return Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.GetAsync(
                        rc =>
                        {
                            rc.QueryParameters.Select = select;//new string[] { "id", "createdDateTime" }
                            rc.QueryParameters.Expand = expand;
                        }
                    );
                    }).Result;
            }

            public DriveItem GetDriveItem(string select, string expand = null)
            {
                return GetDriveItem(select?.Split(','), expand?.Split(','));
            }

            public DriveItem GetRootDriveItem()
            {
                return OneDrive.GetRootDriveItem(DriveId);
            }

            public Folder GetParentFolder(bool refresh = true)
            {
                if (refresh || DriveItem.ParentReference == null)
                    DriveItem.ParentReference = GetDriveItem("ParentReference").ParentReference;

                DriveItem parentDriveItem = Task.Run(() => { return OneDrive.Client.Drives[DriveId].Items[DriveItem.ParentReference.Id].GetAsync(); }).Result;
                return (Folder)New(OneDrive, parentDriveItem);
            }

            public void Delete()
            {
                Task.Run(() => { DriveItemRequestBuilder.DeleteAsync(); }).Wait();
            }

            //public void Rename()
            //{
            //    Task.Run(() =>
            //    {
            //        DriveItemRequestBuilder.Request()();
            //    }).Wait();
            //}

            /// <summary>
            /// Identifiers useful for SharePoint REST compatibility. Read-only.
            /// </summary>
            public SharepointIds SharepointIds
            {
                get
                {
                    if (DriveItem.SharepointIds == null)
                        DriveItem.SharepointIds = GetDriveItem("SharepointIds").SharepointIds;
                    return DriveItem.SharepointIds;
                }
            }

            /// <summary>
            /// For drives in SharePoint, the associated document library list item. Read-only. Nullable.
            /// </summary>
            public ListItem ListItem
            {
                get
                {
                    if (DriveItem.ListItem == null)
                        DriveItem.ListItem = GetDriveItem("ListItem").ListItem;
                    return DriveItem.ListItem;
                }
            }

            public IEnumerable<Item> Search(string query)
            {
                var driveItems = Task.Run(() => { return DriveItemRequestBuilder.SearchWithQ(query).GetAsSearchWithQGetResponseAsync(); }).Result;
                foreach (DriveItem item in driveItems.Value)
                    yield return New(OneDrive, item);
            }

            public string GetPath(bool refresh = true)
            {
                if (refresh)
                {
                    DriveItem di = GetDriveItem("ParentReference, Name");
                    DriveItem.ParentReference = di.ParentReference;
                    DriveItem.Name = di.Name;
                }
                return DriveItem.ParentReference.Path + "/" + DriveItem.Name;
            }

            public Item Get(string relativePath)
            {
                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                DriveItem di = null;
                try
                {
                    di = Task.Run(() => { return DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync(); }).Result;
                }
                catch (Exception e)
                {
                    for (; e != null; e = e.InnerException)
                        if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException ex && (int)System.Net.HttpStatusCode.NotFound == ex.ResponseStatusCode)
                            return null;
                    throw;
                }
                return New(OneDrive, di);
            }
        }
    }
}
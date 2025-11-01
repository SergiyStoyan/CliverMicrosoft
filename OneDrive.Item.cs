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
                if (driveItem.File != null)
                    return new File(oneDrive, driveItem);
                if (driveItem.Folder != null)
                    return new Folder(oneDrive, driveItem);
                throw new Exception("Unknown DriveItem object type: " + driveItem.ToStringByJson());
            }

            static public Item Get(OneDrive oneDrive, Path item)
            {
                Item i = null;
                if (item.BaseObject_LinkOrEncodedLinkOrShareId != null)
                {
                    Item bi = oneDrive.GetItemByLink(item.BaseObject_LinkOrEncodedLinkOrShareId);
                    if (bi == null)
                        return null;
                    if (item.RelativePath == null)
                        return bi;
                    if (!(bi is Folder))
                        throw new Exception("Base object link points not to a folder: " + item.BaseObject_LinkOrEncodedLinkOrShareId);
                    i = bi.Get(item.RelativePath);
                }
                else
                    i = oneDrive.GetItemByPath(item.RelativePath);
                return i;
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
                    //Permission p = Task.Run(() =>
                    //{
                    //    var requestBody = new CreateLinkPostRequestBody
                    //    {
                    //        Type = linkRole.ToString(),
                    //        Password = password,
                    //        Scope = linkScopes.ToString(),
                    //        RetainInheritedPermissions = retainInheritedPermissions,
                    //    };
                    //    return OneDrive.Client.Drives[DriveId].Items[ItemId].CreateLink.PostAsync(requestBody);
                    //}).Result;
                    var requestBody = new CreateLinkPostRequestBody
                    {
                        Type = linkRole.ToString(),
                        Password = password,
                        Scope = linkScopes.ToString(),
                        RetainInheritedPermissions = retainInheritedPermissions,
                    };
                    Permission p = DriveItemRequestBuilder.CreateLink.PostAsync(requestBody).Result;
                    return p.Link;
                }
            }

            public string WebViewLink
            {
                get
                {
                    if (viewLink == null)
                        viewLink = GetLink(LinkRoles.view).ToString();
                    return viewLink;
                }
            }
            string viewLink;

            public DriveItem GetDriveItem(string[] select = null, string[] expand = null/*, string selectWithoutPrefix = null, string expandWithoutPrefix = null*/)
            {
                //return Task.Run(() =>
                //{
                //    return DriveItemRequestBuilder.GetAsync(
                //        rc =>
                //        {
                //            rc.QueryParameters.Select = select;//new string[] { "id", "createdDateTime" }
                //            rc.QueryParameters.Expand = expand;
                //        }
                //        );
                //}).Result;
                return DriveItemRequestBuilder.GetAsync(
                    rc =>
                    {
                        rc.QueryParameters.Select = select;//new string[] { "id", "createdDateTime" }
                        rc.QueryParameters.Expand = expand;
                    }
                ).Result;
            }

            public DriveItem GetDriveItem(string select, string expand = null)
            {
                return GetDriveItem(select.Split(','), expand.Split(','));
            }

            public DriveItem GetRootDriveItem()
            {
                //return Task.Run(() =>
                //{
                //    return OneDrive.Client.Drives[DriveId].Root.GetAsync();
                //}).Result;
                return OneDrive.Client.Drives[DriveId].Root.GetAsync().Result;
            }

            public Folder GetParentFolder(bool refresh = true)
            {
                if (refresh || DriveItem.ParentReference == null)
                    DriveItem.ParentReference = GetDriveItem("ParentReference").ParentReference;

                //DriveItem parentDriveItem = Task.Run(() =>
                //{
                //    return OneDrive.Client.Drives[DriveId].Items[DriveItem.ParentReference.Id].GetAsync();
                //}).Result;
                DriveItem parentDriveItem = OneDrive.Client.Drives[DriveId].Items[DriveItem.ParentReference.Id].GetAsync().Result;

                if (parentDriveItem == null)
                    return null;
                return (Folder)New(OneDrive, parentDriveItem);
            }

            public void Delete()
            {
                //Task.Run(() =>
                //{
                //    DriveItemRequestBuilder.DeleteAsync();
                //}).Wait();
                DriveItemRequestBuilder.DeleteAsync().Wait();
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
                //var driveItems = Task.Run(() =>
                //{
                //    return DriveItemRequestBuilder.SearchWithQ(query).GetAsSearchWithQGetResponseAsync();
                //}).Result;
                var driveItems = DriveItemRequestBuilder.SearchWithQ(query).GetAsSearchWithQGetResponseAsync().Result;

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
                //var di = Task.Run(() =>
                //{
                //    return DriveItemRequestBuilder.ItemWithPath(relativePath).GetAsync();
                //}).Result;

                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                var di = DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync().Result;
                if (di == null)
                    return null;
                return New(OneDrive, di);
            }
        }
    }
}
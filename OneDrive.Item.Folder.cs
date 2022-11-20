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
            internal Folder(OneDrive oneDrive, DriveItem driveItem) : base(oneDrive, driveItem)
            {
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
        }
    }
}
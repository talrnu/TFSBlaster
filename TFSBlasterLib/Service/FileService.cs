using System;
using System.IO;

namespace TFSBlasterLib.Service
{
    internal class FileService
    {
        public virtual string ReadRelativeFileText(Uri relativePath)
        {
            return File.ReadAllText(relativePath.ToString());
        }
    }
}

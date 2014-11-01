using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace RoboWorker3
{
    class Protection
    {
        private static string path = "key.key";
        public static bool checkAccess()
        {
            if (!File.Exists(path))
            {
                return false;
            }
            using (FileStream stream = File.OpenRead(path))
            {
                using (GZipStream gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                {
                    using (BinaryReader reader = new BinaryReader(gzipStream))
                    {
                        DateTime toDate = new DateTime(reader.ReadInt64());
                        if (toDate.Year > DateTime.Now.Year + 2 || toDate.Year < DateTime.Now.Year - 2)
                        {
                            return false;
                        }
                        if (toDate > DateTime.Now)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}

using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Util
{
    public static class TimeLapse
    {
        public static async Task<string> GetAsync(string url, string output_file, int recording_seconds, int interval, int fps, string username = null, string password = "")
        {
            string result_path = null;

            int record_times = recording_seconds / interval; // 撮影回数
            var jpegFileList = new List<string>();

            for(int count = 0; count < record_times; count++)
            {
                string jpegFileNamePath = Path.Combine(Path.GetTempPath(), $"Image_{count:D8}.jpg");
                await DownloadRemoteImageFileAsync(url, jpegFileNamePath, username, password).ConfigureAwait(false);
                jpegFileList.Add(jpegFileNamePath);
                Console.WriteLine($"{count} - {jpegFileNamePath}");
                Thread.Sleep(interval * 1000); // as milleseconds
                
            }

            if (ConvertJpegToAviWithOpenCV(jpegFileList, output_file))
            {
                result_path = output_file;
            } 

            foreach(var f in jpegFileList)
            {
                // Delete Jpeg files
                if (File.Exists(f))
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch { }
                }
            }

            return result_path;
        }

        public static async Task<bool> DownloadRemoteImageFileAsync(string uri, string fileName, string username = null, string password = "")
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            if (!string.IsNullOrEmpty(username))
            {
                //CredentialCacheの作成
                System.Net.CredentialCache cache = new System.Net.CredentialCache();
                //基本認証の情報を追加
                cache.Add(new Uri(uri),
                    "Basic",
                    new System.Net.NetworkCredential(username, password));
                //認証の設定
                request.Credentials = cache;
            }

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }

            // Check that the remote file was found. The ContentType
            // check is performed since a request for a non-existent
            // image file might be redirected to a 404-page, which would
            // yield the StatusCode "OK", even though the image was not
            // found.
            if ((response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Moved ||
                response.StatusCode == HttpStatusCode.Redirect) &&
                response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {

                // if the remote file was found, download it
                using (Stream inputStream = response.GetResponseStream())
                using (Stream outputStream = File.OpenWrite(fileName))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        await outputStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                    } while (bytesRead != 0);
                }
                return true;
            }
            else
                return false;
        }

        public static bool ConvertJpegToAviWithOpenCV(List<string> fileLists, string filename)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                if (fileLists.Count == 0) throw new ArgumentOutOfRangeException();

                (var imageWidth, var imageHeight) = GetImageSize(fileLists.First());
                using (VideoWriter cvw = new VideoWriter(filename, FourCC.MJPG, 30, new OpenCvSharp.Size(imageWidth, imageHeight)))
                {
                    foreach (var f in fileLists)
                    {
                        if (File.Exists(f))
                        {
                            using (Mat m = new Mat(f))
                            {
                                cvw.Write(m);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return false;
            }
            System.Diagnostics.Debug.WriteLine($"time: {sw.Elapsed}");
            return true;
        }
        
        static (int width, int height) GetImageSize(string f)
        {
            using (Mat m = new Mat(f))
            {
                return (m.Width, m.Height);
            }
        }
        
    }
}

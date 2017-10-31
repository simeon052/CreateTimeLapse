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
        public static async Task<string> GetAsync(string url, string outputPath, int recording_seconds, int interval, int fps, string username = null, string password = "")
        {
            string result_path = null;
            try
            {
                if (!Directory.Exists(outputPath))
                {
                    return string.Empty;
                }

                int record_times = recording_seconds / interval; // 撮影回数
                var jpegFileList = new List<string>();
                var commandStr = $"/usr/bin/mogrify";

                var directryName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                outputPath = Path.Combine(outputPath, directryName);
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                var mre = new ManualResetEvent(false);
                mre.Reset();
                int count = 0;
                var timer = new System.Timers.Timer();
                timer.Interval = 1000;
                SemaphoreSlim sem = new SemaphoreSlim(1, 1);
                timer.Elapsed += (async (s, e) =>
                {
                    try
                    {
                        await sem.WaitAsync();
                        string jpegFileNamePath = Path.Combine(outputPath, $"Image_{(++count)-1:D8}.jpg");
                        await DownloadRemoteImageFileAsync(url, jpegFileNamePath, username, password).ConfigureAwait(false);
                        var optionStr = $"-fill yellow -gravity SouthWest -font helvetica -pointsize 45 -annotate +35+10 \"{DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss")}\" {jpegFileNamePath}";
                        jpegFileList.Add(jpegFileNamePath);
                        Console.WriteLine($"{count} - {jpegFileNamePath} -- {DateTime.Now.ToLongTimeString()}");

                        if (File.Exists(jpegFileNamePath))
                        {
                            // System.Console.WriteLine($"Execute : {commandStr} {optionStr}");
                            var psi = new ProcessStartInfo(commandStr, optionStr) { UseShellExecute = false, CreateNoWindow = true };
                            Process p = System.Diagnostics.Process.Start(psi);
                        } else {
                            Console.WriteLine($"{jpegFileNamePath} creattion failed.");
                            File.Copy(jpegFileList.Last(), jpegFileNamePath);
                        }      
                        
                        if (count > record_times)
                        {
                            mre.Set();
                        }
                        sem.Release();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Some exception {ex.ToString()}");
                    }
                });
                timer.Start();

                mre.WaitOne();
                timer.Stop();


                result_path = ConvertJpegToAviWithffmpeg(outputPath, fps);


                if (File.Exists(result_path))
                {
                    foreach (var f in jpegFileList)
                    {

                        // Delete Jpeg files

                        {
                            try
                            {
                                File.Delete(f);
                            }
                            catch
                            {
                                ; // Ignore
                            }
                        }
                    }
                }
            }catch(Exception e)
            {
                throw e;
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

            response = (HttpWebResponse)request.GetResponse();

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
            {
                throw new HttpListenerException(0, $"failed by : {response.StatusCode}");
            }

        }

        public static string ConvertJpegToAviWithffmpeg(string srcPath, int fps=30)
        {
            string outputFileName = Path.Combine(srcPath,$"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.mp4");

            string commandStr = @"/usr/bin/ffmpeg";
            string optionStr = $"-f image2 -r 30 -i {srcPath}/Image_%08d.jpg -r {fps} -an -crf 18 -preset veryfast -vcodec libx264 -pix_fmt yuv420p {outputFileName}";

			var psi = new ProcessStartInfo(commandStr, optionStr) { UseShellExecute = false, CreateNoWindow = true };
            Process p = System.Diagnostics.Process.Start(psi);
            p.WaitForExit();

            return outputFileName;
        }
    }
}

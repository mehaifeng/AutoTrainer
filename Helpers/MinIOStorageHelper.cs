using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Result;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    public class MinIOStorageHelper
    {
        private readonly IMinioClient minioClient;
        public string bucketName = string.Empty;
        public MinIOStorageHelper(string endpoint, string accessKey, string secretKey)
        {
            minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(false)
                .Build();
            Task[] tasks = [];
            Task.WaitAll(tasks);
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task UploadFileAsync(string filePath, string username)
        {
            string fileName = Path.GetFileName(filePath);
            string objectName = $"{username}/{fileName}";

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithFileName(filePath)
                .WithContentType("application/octet-stream");

            await minioClient.PutObjectAsync(putObjectArgs);
        }
        public async Task DownloadFileWithProgressAsync(
        string bucketName,
        string objectName,
        string destinationPath,
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default)
        {
            try
            {
                // 获取对象的状态信息以获取文件大小
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName);
                var objectStat = await minioClient.StatObjectAsync(statObjectArgs, cancellationToken);
                var totalSize = objectStat.Size;

                // 准备下载参数
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream((stream) =>
                    {
                        using (var fileStream = File.Create(destinationPath))
                        {
                            var buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;

                            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    throw new OperationCanceledException();
                                }

                                fileStream.Write(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                // 计算并报告进度
                                if (progress != null)
                                {
                                    double progressPercentage = (double)totalBytesRead / totalSize * 100;
                                    progress.Report(progressPercentage);
                                }
                            }
                        }
                    });

                // 执行下载
                await minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
            }
            catch (Exception ex)
            {
                // 如果下载失败，删除可能部分下载的文件
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                throw new Exception($"下载文件时发生错误: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 检查存储桶是否存在
        /// </summary>
        /// <example>
        /// <code>
        /// var data = MinioHelper.ListBuckets(minio);
        /// </code>
        /// </example>
        /// <param name="bucketName">存储桶名称</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns></returns>
        public bool BucketExists(string bucketName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                BucketExistsArgs args = new BucketExistsArgs()
                    .WithBucket(bucketName);

                Task<bool> bucketExistTask = minioClient.BucketExistsAsync(args, cancellationToken);
                Task.WaitAll(bucketExistTask);
                return bucketExistTask.Result;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        /// <summary>
        /// 列出所有桶
        /// </summary>
        /// <returns></returns>
        public async Task<List<Bucket>> ListBucketsAsync()
        {
            var buckets = new List<Bucket>();
            var bucket = await minioClient.ListBucketsAsync();
            var bucketResults = bucket.Buckets;
            if (bucketResults != null)
            {
                foreach (var bucketResult in bucketResults)
                {
                    buckets.Add(bucketResult);
                }
            }
            return buckets;
        }
        /// <summary>
        /// 创建存储桶
        /// <example >
        ///     <code >
        ///         MinioHelper.MakeBucket(minio, buckName).Wait();    
        ///     </code>
        /// </example>
        /// </summary>
        /// <param name="bucketName">存储桶名称</param>
        /// <param name = "loc" > 可选参数 </param >
        /// <returns ></returns >
        public Task CreateBucket(string bucketName, string loc = "us-east-1")
        {
            try
            {
                bool found = BucketExists(bucketName);
                if (found)
                {
                    throw new Exception($"存储桶[{bucketName}]已存在");
                }

                MakeBucketArgs args = new MakeBucketArgs()
                    .WithBucket(bucketName)
                    .WithLocation(loc);

                return minioClient.MakeBucketAsync(args);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        /// <summary>
        /// 列出文件和目录，包含详细信息
        /// </summary>
        /// <param name="bucketName">桶名称</param>
        /// <param name="prefix">前缀路径</param>
        /// <param name="recursive">是否递归获取</param>
        /// <returns>包含文件和目录详细信息的元组</returns>
        public async Task<(List<FileInfo> Files, List<string> Directories)> ListFilesAndDirectoriesAsync(
            string bucketName,
            string? prefix = null,
            bool recursive = false)
        {
            var files = new List<FileInfo>();
            var directories = new HashSet<string>();
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(recursive);
            var items = minioClient.ListObjectsEnumAsync(listArgs);
            await foreach (var item in items)
            {
                if (item.Key.EndsWith('/'))
                {
                    var dirName = item.Key.TrimEnd('/');
                    if (prefix != null)
                    {
                        dirName = dirName.Substring(prefix.TrimEnd('/').Length).TrimStart('/');
                    }

                    if (!string.IsNullOrEmpty(dirName) && !dirName.Contains('/'))
                    {
                        directories.Add(dirName);
                    }
                }
                else
                {
                    var fileName = item.Key;
                    if (prefix != null)
                    {
                        fileName = fileName.Substring(prefix.Length);
                    }
                    if (!fileName.Contains('/'))
                    {
                        // 创建包含详细信息的文件对象
                        var fileInfo = new FileInfo
                        {
                            Name = fileName,
                            Size = (long)item.Size,
                            LastModified = DateTime.Parse(item.LastModified),
                            ETag = item.ETag,
                            VersionId = item.VersionId
                        };

                        files.Add(fileInfo);
                    }
                }
            }
            return (files, directories.ToList());
        }
        /// <summary>
        /// 列出文件夹内容
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="prefix"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<FileInfo>> ListFolderContentsAsync(string bucketName, string prefix, bool recursive = true)
        {
            var items = new List<FileInfo>();
            try
            {
                var listArgs = new ListObjectsArgs()
                    .WithBucket(bucketName)
                    .WithPrefix(prefix)
                    .WithRecursive(recursive);

                var objects = minioClient.ListObjectsEnumAsync(listArgs);
                await foreach (var item in objects)
                {
                    items.Add(new FileInfo
                    {
                        Path = item.Key,
                        Size = (long)item.Size,
                        IsDirectory = item.Key.EndsWith('/')
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"列出文件夹内容时发生错误: {ex.Message}", ex);
            }
            return items;
        }

        /// <summary>
        /// 下载文件夹及其子项
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="sourcePath"></param>
        /// <param name="destinationBasePath"></param>
        /// <param name="progress"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task DownloadFolderAsync(
            string bucketName,
            string sourcePath,
            string destinationBasePath,
            IProgress<(string File, double Progress)> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 确保源路径以 / 结尾
                if (sourcePath.StartsWith(bucketName + '/'))
                {
                    sourcePath = sourcePath[(bucketName.Length + 1)..];
                }
                sourcePath = sourcePath.TrimStart('/');
                sourcePath = sourcePath.TrimEnd('/') + '/';

                // 获取文件夹中的所有内容
                var contents = await ListFolderContentsAsync(bucketName, sourcePath);

                foreach (var item in contents)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // 计算相对路径
                    var relativePath = item.Path.Substring(sourcePath.Length);
                    var fullDestinationPath = Path.Combine(destinationBasePath, relativePath);

                    if (item.IsDirectory)
                    {
                        // 创建目录
                        Directory.CreateDirectory(fullDestinationPath);
                    }
                    else
                    {
                        // 确保目标文件夹存在
                        Directory.CreateDirectory(Path.GetDirectoryName(fullDestinationPath));

                        // 下载文件
                        var fileProgress = new Progress<double>(percent =>
                        {
                            progress?.Report((relativePath, percent));
                        });

                        await DownloadFileWithProgressAsync(
                            bucketName,
                            item.Path,
                            fullDestinationPath,
                            fileProgress,
                            cancellationToken
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"下载文件夹时发生错误: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 列出文件
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="prefix"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public async Task<List<string>> ListFilesAsync(string bucketName,string? prefix = null,bool recursive = true)
        {
            var files = new List<string>();
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(recursive);
            var items = minioClient.ListObjectsEnumAsync(listArgs);
            await foreach (var item in items)
            {
                files.Add(Path.GetFileName(item.Key));
            }
            return files;
        }
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task DeleteFileAsync(string fileName, string username)
        {
            string objectName = $"{username}/{fileName}";
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);
            await minioClient.RemoveObjectAsync(removeArgs);
        }
    }
    // 创建一个新的文件信息类来存储更多详细信息
    public class FileInfo
    {
        public bool IsDirectory { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string ETag { get; set; }
        public string VersionId { get; set; }
    }
}

using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Result;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    public class MinIOStorageHelper
    {
        private readonly IMinioClient minioClient;
        private readonly string bucketName = string.Empty;
        public MinIOStorageHelper(string endpoint, string accessKey, string secretKey)
        {
            minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(false)
                .Build();
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
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="destinationPath"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task DownloadFileAsync(string fileName, string destinationPath, string username)
        {
            string objectName = $"{username}/{fileName}";

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithFile(destinationPath);

            await minioClient.GetObjectAsync(getObjectArgs);
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
        /// 列出文件和目录
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public async Task<(List<string> Files, List<string> Directories)> ListFilesAndDirectoriesAsync(string bucketName, string? prefix = null,bool recursive = false)
        {
            var files = new List<string>();
            var directories = new HashSet<string>(); // 使用HashSet避免重复
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(recursive); // 设置为false以获取当前级别
            var items = minioClient.ListObjectsEnumAsync(listArgs);
            await foreach (var item in items)
            {
                // 如果以/结尾，说明是目录
                if (item.Key.EndsWith('/'))
                {
                    var dirName = item.Key.TrimEnd('/');
                    if (prefix != null)
                    {
                        // 如果有前缀，需要去除前缀部分
                        dirName = dirName.Substring(prefix.TrimEnd('/').Length).TrimStart('/');
                    }

                    // 只添加当前级别的目录名
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
                        // 如果有前缀，需要去除前缀部分
                        fileName = fileName.Substring(prefix.Length);
                    }
                    // 只添加当前级别的文件
                    if (!fileName.Contains('/'))
                    {
                        files.Add(fileName);
                    }
                }
            }

            return (files, directories.ToList());
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
}

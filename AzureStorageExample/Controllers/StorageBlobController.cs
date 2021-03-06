﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Ionic.Zip;
using Microsoft.WindowsAzure.Storage.Blob;
using StorageExamples.Models;

namespace StorageExamples.Controllers
{
    public class StorageBlobController : Controller
    {
        public async Task<ActionResult> Index()
        {
            var fileList = new List<string>();

            var helper = new AzureBlobStorageHelper("StorageConnectionString", "BlobContainerName");

            var result = await helper.ListFilesIgnoreMemoryConcernsAsync(null, true, 5);

            foreach (var cloudBlockBlob in result)
            {
                fileList.Add(cloudBlockBlob.Name);
            }
 
            return View(fileList);
        }

        public ActionResult DownloadBlob(string blobName)
        {
            return DownloadAzureBlob(blobName);
        }

        [HttpPost]
        public async Task<ActionResult> DeleteBlob(string blobName)
        {
            var helper = new AzureBlobStorageHelper("StorageConnectionString", "BlobContainerName");
            await helper.DeleteBlobAsync(blobName);

            return RedirectToAction("Index");
        }

        public async Task<ActionResult> ZipBlob(string blobName)
        {
            var helper = new AzureBlobStorageHelper("StorageConnectionString", "BlobContainerName");

            using (ZipFile theZipFile = new ZipFile())
            {
                List<CloudBlockBlob> blobList = await helper.ListFilesIgnoreMemoryConcernsAsync(blobName, true, 100);
                foreach (CloudBlockBlob myCloudBlob in blobList)
                {
                    // Download fails if your try to dispose of this cloud blob stream with using statement here.
                    var myStream = myCloudBlob.OpenRead();

                    // Remove first part of the path
                    if (myCloudBlob.Name.StartsWith("Images"))
                    {
                        string newName = myCloudBlob.Name.Substring(7);
                        theZipFile.AddEntry(newName, myStream);
                    }
                    else theZipFile.AddEntry(myCloudBlob.Name, myStream);   
                    
                }

                return new ZipResult(theZipFile, "Report.zip");
            } 
        }


        [HttpPost]
        public async Task<ActionResult> UploadFile()
        {
            var httpRequest = HttpContext.Request;

            if (httpRequest.Files.Count > 0)
            {
                const int fileIndex = 0;
                var helper = new AzureBlobStorageHelper("StorageConnectionString", "BlobContainerName");
                
                var directory = FileUploadHelper.GetFormValue<string>(httpRequest, "directory", string.Empty);
                var fileName = string.IsNullOrWhiteSpace(directory) ?
                    FileUploadHelper.GetFileName(httpRequest, fileIndex) :
                    Path.Combine(directory, FileUploadHelper.GetFileName(httpRequest, fileIndex));

                using (Stream fileStream = FileUploadHelper.GetInputStream(httpRequest, fileIndex))
                    await helper.UploadBlobAsync(fileName, fileStream, null);
                
                return RedirectToAction("Index");
            }
         
            throw new ArgumentException("File count is zero.");            
        }


        private FileResult DownloadAzureBlob(string blobName)
        {
            var helper = new AzureBlobStorageHelper("StorageConnectionString", "BlobContainerName");

            CloudBlockBlob myCloudBlob = helper.FindBlob(blobName);

            if (myCloudBlob.Exists() == false)
                throw new FileNotFoundException($"Could not find the blob named: {blobName}");

            // Get a stream to the blob
            var myStream = myCloudBlob.OpenRead();

            // Determine the Mime Type
            string mimeType = MimeMapping.GetMimeMapping(blobName) ?? "application/octet-stream";

            // The stream will be destoyed by FileSreamResult.WriteFile deep within HttpResponseBase according to this post
            // https://stackoverflow.com/questions/3084366/how-do-i-dispose-my-filestream-when-implementing-a-file-download-in-asp-net
            return File(myStream, mimeType, myCloudBlob.Name);
        }

    }
}
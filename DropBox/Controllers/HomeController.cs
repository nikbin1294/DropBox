﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DropBox.Common;
using DropBox.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DropBox.Controllers
{
    public class HomeController : Controller
    {
        private readonly LiteDbContext _db;
        private readonly IHubContext<UploadHub, IUploadHub> _uploadHub;

        public HomeController(LiteDbContext db, IHubContext<UploadHub, IUploadHub> uploadHub)
        {
            _db = db;
            _uploadHub = uploadHub;
        }

        [Route("/")]
        public IActionResult Index()
        {
            var model = _db.Context.FileStorage.FindAll();

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(IFormFile file)
        {
            if (file.Length > 0)
            {
                using (var stream = new MemoryStream())
                {
                    file.CopyTo(stream);
                    stream.Position = 0;
                    _db.Context.FileStorage.Upload(file.FileName, file.FileName, stream);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Details(string id)
        {
            var model = _db.Context.FileStorage.FindById(id);
            var memory = new MemoryStream();

            model.CopyTo(memory);
            memory.Position = 0;

            return new FileStreamResult(memory, model.MimeType);
        }

        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IEnumerable<IFormFile> files,
            [FromServices] IHostingEnvironment env,
            CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                using (FileStream output = System.IO.File.Create(env.ContentRootPath + "/files/" + file.FileName))
                {
                    await SaveFile(file, output);
                }

                TempData["message"] = $"File [{file.FileName}] uploaded successfully.";

            }

            return View();
        }

        private async Task SaveFile(IFormFile file, FileStream output)
        {
            byte[] buffer = new byte[16 * 1024];
            long totalBytes = file.Length;

            using (Stream input = file.OpenReadStream())
            {
                long totalReadBytes = 0;
                int readBytes;

                while ((readBytes = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                    int progress = (int)((float)totalReadBytes / (float)totalBytes * 100.0);
                    await _uploadHub.Clients.All.SendProgress(file.FileName, progress);
                    await Task.Delay(50); // It is only to make the process slower
                }
            }

        }

    }
}
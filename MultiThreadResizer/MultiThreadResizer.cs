﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageResizer;
using System.Threading;
using System.Diagnostics;

namespace MultiThreadResizer
{
    public class MultiThreadResizerWorker
    {
        #region Properties
        public readonly List<CustomResizeSettings> ListOfResizeSettingsDefault = 
            new List<CustomResizeSettings>()
            {
               // new CustomResizeSettings("_thumb", 20, 20)
            };        
        private const int DefaultTaskCount = 1;
        private const int DefaultImagesCountinOneThread = 10;
        public string NameSubFolderForNewFiles { get; set; }
        public int MaxTaskCount { get; set; }
        public int MaxImagesCountinOneThread { get; set; }
        public List<CustomResizeSettings> ListOfResizeSettings { get; set; }
        public ConcurrentDictionary<FileAndCustomResizeSetting, StatusOfImage> ListOfFileAndCustomResizeSettings { get; set; }
        public MultiThreadResizerWorker():this(DefaultTaskCount, DefaultImagesCountinOneThread){}
        public MultiThreadResizerWorker(List<CustomResizeSettings> listOfResizeSettings):this(DefaultTaskCount, DefaultImagesCountinOneThread, listOfResizeSettings){}
        public MultiThreadResizerWorker(int maxTaskCount, int maxImagesCountinOneThread, List<CustomResizeSettings> listOfResizeSettings = null)
        {
            MaxTaskCount = maxTaskCount> DefaultTaskCount ? maxTaskCount : DefaultTaskCount;
            MaxImagesCountinOneThread = maxImagesCountinOneThread> DefaultImagesCountinOneThread ? maxImagesCountinOneThread: DefaultImagesCountinOneThread;
            ListOfResizeSettings = listOfResizeSettings!=null? listOfResizeSettings : ListOfResizeSettingsDefault;
            ListOfFileAndCustomResizeSettings = new ConcurrentDictionary<FileAndCustomResizeSetting, StatusOfImage>();
            Log = new ConcurrentQueue<string>();
        }
        #endregion
        #region InfoProperties
        public int AllImages
        { get { return  ListOfFileAndCustomResizeSettings.Select(f=>f.Key.FileSource).Distinct().Count();} }
        public int AllResizingImages
        { get { return ListOfFileAndCustomResizeSettings.Count(); } }
        public int ResizedImages
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value.Status== 3).Count(); } }
        public int ImagesInProccess
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value.Status == 2).Count(); } }
        public int FreeImages
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value.Status == 1).Count(); } }
        public int ResizedWithErrorsImages
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value.Status == 0).Count(); } }
        public string StateSummary
        { get { return String.Format("AllImages:{0} AllResizingImages:{1} FreeImages:{2} ImagesInProccess:{3} ResizedImages:{4} ResizedWithErrorsImages:{5}", 
            AllImages, AllResizingImages, FreeImages, ImagesInProccess, ResizedImages, ResizedWithErrorsImages); } }
        public string ShortStateSummary
        { get { return String.Format("{0}-{1}-{2}-{3}-{4}-{5}", AllImages, AllResizingImages, FreeImages, ImagesInProccess, ResizedImages, ResizedWithErrorsImages); } }
        private ConcurrentQueue<string> Log { get; set; }
        public string GetLogMessage()
        {
            string result;
            Log.TryDequeue(out result);
            return result;
        }
        #endregion
        public string StartResizing(int timeOutInSec)
        {
            var Task = StartResizingTask(timeOutInSec);
            Task.Wait();
            return "OK";
        }
        public Task StartResizingTask(long timeOutInSec)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            return Task.Factory.StartNew(() =>
            {                 
                var watch = new Stopwatch();
                watch.Start();
                while (watch.ElapsedMilliseconds< timeOutInSec * 1000 && FreeImages > 0)
                {
                    var tasks = new Task[MaxTaskCount];
                    for (int i = 0; i < MaxTaskCount; i++)
                    {
                        var portion = TakeImagesForThread();
                        tasks[i] = Task.Factory.StartNew(() => ResizeImages(portion), token);
                    }
                    Task.WaitAll(tasks, (int)(timeOutInSec * 1000 - watch.ElapsedMilliseconds), token);
                }
            }).ContinueWith((t)=> ChangeFlagToFree());
        }
        public List<FileAndCustomResizeSetting> TakeImagesForThread()
        {
            var result = new List<FileAndCustomResizeSetting>();
            result = ListOfFileAndCustomResizeSettings.Where(f => f.Value.Status == 1).Take(MaxImagesCountinOneThread).Select(f => f.Key).ToList();
            result.ForEach(f=> {
                StatusOfImage localStatus;
                ListOfFileAndCustomResizeSettings.TryGetValue(f, out localStatus);
                localStatus.Status = 2;
            });
            return result;
        }
        public void ChangeFlagToFree()
        {
            ListOfFileAndCustomResizeSettings.Where(f=>f.Value.Status== 2).Select(f=>f.Key).ToList().ForEach(f => 
            {
                StatusOfImage localStatus;
                ListOfFileAndCustomResizeSettings.TryGetValue(f, out localStatus);
                localStatus.Status = 1;
            });
        }
        public void ChangeFlagToFree(List<FileAndCustomResizeSetting> images)
        {
            images.ForEach(f =>
            {
                StatusOfImage localStatus;
                ListOfFileAndCustomResizeSettings.TryGetValue(f, out localStatus);
                localStatus.Status = 1;
            });
        }
        public int ResizeImage(FileAndCustomResizeSetting image)
        {
            int result;
            try
            {
                ImageJob i = new ImageJob(image.FileSource, image.NewFileName, image.CustomResizeSetting, false, false);
                i.CreateParentDirectory = true;//Auto-create the uploads directory.
                i.Build();
                StatusOfImage localStatus;
                ListOfFileAndCustomResizeSettings.TryGetValue(image, out localStatus);
                localStatus.Status = 3;
                localStatus.FinishTime = DateTime.Now;
                result = localStatus.Status;
            }
            catch
            {
                StatusOfImage localStatus;
                ListOfFileAndCustomResizeSettings.TryGetValue(image, out localStatus);
                localStatus.Status = 0;
                result = 0;
            }
            return result;
        }
        public void ResizeImages(List<FileAndCustomResizeSetting> images)
        {
            var result = images.Select(f => ResizeImage(f)).ToList();            
            Log.Enqueue(string.Concat("Sized =", result.Count, " Successfully =", result.Where(r => r == 3).Count(), " with errors=", result.Where(r => r == 0).Count()));
        }
        public string SetFolderWithImages(string path)
        {
            var result = "";
            try
            {
                var files = new List<string>();
                result = GetListOfFiles(path, out files);
                result = AddListOfImages(files);
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }
        public string GetListOfFiles(string path, out List<string> files)
        {
            string[] extensions = { ".jpg", ".png", ".bmp", ".gif" };
            if (Directory.Exists(path))
            {
                files = Directory.GetFiles(path)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower())).ToList();
            }
            else
            {
                throw new Exception("Path is not Exist");
            }           
            return string.Concat("Found ", files.Count, " files");
        }
        public string AddListOfImages(List<string> files)
        {
            files.ForEach(f => AddListOfImagesForFile(f));
            return string.Concat("Added ", files.Count * ListOfResizeSettings.Count, " FileAndCustomResizeSettings");
        }
        public void AddListOfImagesForFile(string file)
        {
            ListOfResizeSettings.ForEach(r => 
            ListOfFileAndCustomResizeSettings.TryAdd(
                new FileAndCustomResizeSetting(file, r, NameSubFolderForNewFiles), new StatusOfImage()));
        }
    }

    public class FileAndCustomResizeSetting
    {
        private string _fileName { get; set; }
        public string FileSource { get; set; }
        public string FileName { get; set; }
        public CustomResizeSettings CustomResizeSetting { get; set; }
        public string NewFileName { get { return _fileName + FileName; } }
        public FileAndCustomResizeSetting(string fileSource, CustomResizeSettings customResizeSetting, string nameSubFolderForNewFiles = "")
        {
            FileSource = fileSource;
            FileName = Path.GetFileNameWithoutExtension(fileSource) + customResizeSetting.Suffix + Path.GetExtension(FileSource);
            _fileName = Path.GetDirectoryName(fileSource) + nameSubFolderForNewFiles + "\\";
            CustomResizeSetting = customResizeSetting;
        }
    }

    public class CustomResizeSettings : Instructions
    {
        public string Suffix { get ; set; }
        public CustomResizeSettings(string suffix, int width, int height, FitMode mode = FitMode.Stretch, string imageFormat = null)
        {
            Suffix = suffix;
            Height = height;
            Width = width;
            Mode = mode;
            Format = imageFormat;
        }
    }
    public class StatusOfImage 
    {
        // TValue = int
        // 0 - error
        // 1 - free
        // 2 - ready for resize
        // 3 - resized
        public int Status { get; set; }
        public DateTime StartTime { get; }
        public DateTime FinishTime { get; set; }
        public StatusOfImage()
        {
            Status = 1;
            StartTime = DateTime.Now;
        }
    }
}

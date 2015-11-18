﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageResizer;
using System.Threading;

namespace MultiThreadResizer
{
    public class MultiThreadResizerWorker
    {
        #region Properties

        public readonly List<CustomResizeSettings> ListOfResizeSettingsDefault = 
            new List<CustomResizeSettings>()
            {
                new CustomResizeSettings("_nano", 10, 10),
                new CustomResizeSettings("_micro", 50, 50),
                new CustomResizeSettings("_small", 200, 200),
                new CustomResizeSettings("_medium", 400, 400),
                new CustomResizeSettings("_large", 1200, 1200)
            };        
        private const int DefaultTaskCount = 1;
        private const int DefaultImagesCountinOneThread = 10;
        public string NameSubFolderForNewFiles { get; set; }
        public int MaxTaskCount { get; set; }
        public int MaxImagesCountinOneThread { get; set; }
        private List<CustomResizeSettings> ListOfResizeSettings { get; set; }
        // TValue = int
        // 0 - error
        // 1 - free
        // 2 - ready for resize
        // 3 - resized
        public ConcurrentDictionary<FileAndCustomResizeSetting,int> ListOfFileAndCustomResizeSettings { get; set; }
        public MultiThreadResizerWorker():this(DefaultTaskCount, DefaultImagesCountinOneThread){}
        public MultiThreadResizerWorker(List<CustomResizeSettings> listOfResizeSettings):this(DefaultTaskCount, DefaultImagesCountinOneThread, listOfResizeSettings){}
        public MultiThreadResizerWorker(int maxTaskCount, int maxImagesCountinOneThread, List<CustomResizeSettings> listOfResizeSettings = null)
        {
            MaxTaskCount = maxTaskCount> DefaultTaskCount ? maxTaskCount : DefaultTaskCount;
            MaxImagesCountinOneThread = maxImagesCountinOneThread> DefaultImagesCountinOneThread ? maxImagesCountinOneThread: DefaultImagesCountinOneThread;
            ListOfResizeSettings = listOfResizeSettings!=null? listOfResizeSettings : ListOfResizeSettingsDefault;
            ListOfFileAndCustomResizeSettings = new ConcurrentDictionary<FileAndCustomResizeSetting, int>();
        }
        #endregion

        #region InfoProperties
        public int AllImages
        { get { return  ListOfFileAndCustomResizeSettings.Select(f=>f.Key.FileSource).Distinct().Count();} }
        public int AllResizingImages
        { get { return ListOfFileAndCustomResizeSettings.Count(); } }

        public int ResizedImages
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value==3).Count(); } }

        public int ImagesInProccess
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value == 2).Count(); } }
        public int FreeImages
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value == 1).Count(); } }
        public int ResizedWithErrorsImages
        { get { return ListOfFileAndCustomResizeSettings.Where(f => f.Value == 0).Count(); } }

        public string StateSummary
        { get { return String.Format("AllImages:{0} AllResizingImages:{1} ResizedImages:{2} ImagesInProccess:{3} FreeImages:{4} ResizedWithErrorsImages:{5}", AllImages, AllResizingImages, FreeImages, ImagesInProccess, ResizedImages, ResizedWithErrorsImages); } }

        public string ShortStateSummary
        { get { return String.Format("{0}-{1}-{2}-{3}-{4}-{5}", AllImages, AllResizingImages, FreeImages, ImagesInProccess, ResizedImages, ResizedWithErrorsImages); } }

        #endregion


        public string StartResizing(int timeOutInSec) {

            string result = "";

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            var tasks = new Task<string>[MaxTaskCount];
            for (int i = 0; i < MaxTaskCount; i++)
            {
                tasks[i] = new Task<string>(TaskResizing, token);
                tasks[i].Start();
            }

            Task.WaitAll(tasks, timeOutInSec * 1000, token);

            for (int i = 0; i < MaxTaskCount; i++)
            {
                if (tasks[i].IsCompleted) result += i.ToString() + " " + tasks[i].Result + " ";
                if (tasks[i].IsFaulted) result += i.ToString() + " IsFaulted ";
                if (tasks[i].IsCanceled) result += i.ToString() + " IsCanceled ";
            }

            return result;
        }
        public string TaskResizing()
        {
            return ResizeImages(TakeImagesForThread());
        }
        public List<FileAndCustomResizeSetting> TakeImagesForThread()
        {
            var result = new List<FileAndCustomResizeSetting>();
            result = ListOfFileAndCustomResizeSettings.Where(f => f.Value == 1).Take(MaxImagesCountinOneThread).Select(f => f.Key).ToList();
            result.ForEach(f=> ListOfFileAndCustomResizeSettings.TryUpdate(f,2,1));
            return result;
        }
        public int ResizeImage(FileAndCustomResizeSetting image)
        {
            int result;
            try
            {
                ImageJob i = new ImageJob(image.FileSource, image.NewFileName, image.CustomResizeSetting, false, true);
                i.CreateParentDirectory = true;//Auto-create the uploads directory.
                i.Build();
                result = ListOfFileAndCustomResizeSettings.TryUpdate(image, 3, 2)?3:0;
            }
            catch
            {
                ListOfFileAndCustomResizeSettings.TryUpdate(image, 0, 2);
                result = 0;
            }
            return result;
        }
        public string ResizeImages(List<FileAndCustomResizeSetting> images)
        {
            var result = images.Select(f => ResizeImage(f)).ToList();
            return string.Concat("Sized =", result.Count," Successfully =", result.Where(r=>r==3).Count(), " with errors=", result.Where(r => r == 0).Count());
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
            ListOfResizeSettings.ForEach(r => ListOfFileAndCustomResizeSettings.TryAdd(new FileAndCustomResizeSetting(file, r, NameSubFolderForNewFiles), 1));
        }
    }

    public class FileAndCustomResizeSetting
    {
        private string _fileName { get; set; }
        public string FileSource { get; set; }
        public CustomResizeSettings CustomResizeSetting { get; set; }
        public string NewFileName { get { return _fileName + CustomResizeSetting.Suffix + Path.GetExtension(FileSource); }}
        public FileAndCustomResizeSetting(string fileSource, CustomResizeSettings customResizeSetting, string nameSubFolderForNewFiles = "")
        {
            FileSource = fileSource;
            _fileName = Path.GetDirectoryName(fileSource) + nameSubFolderForNewFiles + "\\" + Path.GetFileNameWithoutExtension(fileSource);
            CustomResizeSetting = customResizeSetting;
        }
    }

    public class CustomResizeSettings : Instructions
    {
        public string Suffix { get ; set; }
        public CustomResizeSettings(string suffix, int width, int height, FitMode mode = FitMode.None, string imageFormat = null)
        {
            Suffix = suffix;
            Height = height;
            Width = width;
            Mode = mode;
            Format = imageFormat;
        }

    }
}

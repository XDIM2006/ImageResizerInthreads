using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using MultiThreadResizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestMultiThreadResizer
{
    [TestClass]
    public class UnitTestMultiThreadResizer
    {
        string Path = @"D:\Sephora\2015.8";
        int CountFiles = 19;
        [TestMethod]
        public void TestFileAndCustomResizeSettingClass()
        {
            var suffix = "_large";
            var CustomResizeSettings = new CustomResizeSettings(suffix, 100, 100);
            var FileAndCustomResizeSetting = new FileAndCustomResizeSetting(Path+ @"\CalvinBlog.png", CustomResizeSettings);

            Assert.AreEqual(Path + @"\CalvinBlog.png", FileAndCustomResizeSetting.FileSource);
            Assert.AreEqual(Path + @"\CalvinBlog"+ suffix + @".png", FileAndCustomResizeSetting.NewFileName);
        }

        [TestMethod]
        public void TestCustomResizeSettingsClass()
        {
            var suffix = "_large";
            var CustomResizeSettings = new CustomResizeSettings(suffix, 100, 100);
            Assert.AreEqual(suffix, CustomResizeSettings.Suffix);
        }
        [TestMethod]
        public void TestMultiThreadResizerClass()
        {
            var MultiThreadResizer = new MultiThreadResizerWorker();
            
            Assert.AreEqual(0,MultiThreadResizer.ListOfFileAndCustomResizeSettings.Count);
        }
        [TestMethod]
        public void AddListOfImages()
        {
            var MultiThreadResizer = new MultiThreadResizerWorker();
            var files = new List<string>();
            MultiThreadResizer.GetListOfFiles(Path, out files);
            MultiThreadResizer.AddListOfImages(files);
            Assert.AreEqual(string.Concat("Added ", MultiThreadResizer.ListOfResizeSettingsDefault.Count* files.Count, " FileAndCustomResizeSettings"), MultiThreadResizer.AddListOfImages(files));
        }
        [TestMethod]
        public void AddListOfImagesForFile()
        {
            var MultiThreadResizer = new MultiThreadResizerWorker();
            var files = new List<string>();
            MultiThreadResizer.GetListOfFiles(Path, out files);

            MultiThreadResizer.AddListOfImagesForFile(files[0]);
            Assert.AreEqual(MultiThreadResizer.ListOfResizeSettingsDefault.Count, MultiThreadResizer.ListOfFileAndCustomResizeSettings.Count);
        }
        [TestMethod]
        public void GetListOfFiles()
        {
            var MultiThreadResizer = new MultiThreadResizerWorker();
            var files = new List<string>();
            Assert.AreEqual(string.Concat("Found ", CountFiles, " files"), MultiThreadResizer.GetListOfFiles(Path, out files));
        }
        [TestMethod]
        public void SetFolderWithImages()
        {
            var MultiThreadResizer = new MultiThreadResizerWorker();
            Assert.AreEqual(string.Concat("Added ", MultiThreadResizer.ListOfResizeSettingsDefault.Count*19, " FileAndCustomResizeSettings"), MultiThreadResizer.SetFolderWithImages(Path));
        }
        [TestMethod]
        public void TakeImagesForThread()
        {
            var MultiThreadResizer = new MultiThreadResizerWorker(2,20);
            MultiThreadResizer.SetFolderWithImages(Path);

            var result = MultiThreadResizer.TakeImagesForThread();

            Assert.AreEqual(20, result.Count);
            Assert.AreEqual(20, result.Where(f=> MultiThreadResizer.ListOfFileAndCustomResizeSettings[f]==2).Count());
            result = MultiThreadResizer.TakeImagesForThread();

            Assert.AreEqual(20, result.Count);
            Assert.AreEqual(20, result.Where(f => MultiThreadResizer.ListOfFileAndCustomResizeSettings[f] == 2).Count());
        }
        [TestMethod]
        public void ResizeImages()
        {
            int CountOfImage = 10;
            var MultiThreadResizer = new MultiThreadResizerWorker(2, CountOfImage);
            MultiThreadResizer.NameSubFolderForNewFiles = @"\NewImages\ResizeImages";
            MultiThreadResizer.SetFolderWithImages(Path);
            var images = MultiThreadResizer.TakeImagesForThread();
            var result = MultiThreadResizer.ResizeImages(images);
            Assert.AreEqual(string.Concat("Sized =", images.Count, " Successfully =", images.Count, " with errors=", 0), result);
            Assert.AreEqual(images.Count, images.Where(f => MultiThreadResizer.ListOfFileAndCustomResizeSettings[f] == 3).Count());

            var images2 = MultiThreadResizer.TakeImagesForThread();
            var result2 = MultiThreadResizer.ResizeImages(images2);
            Assert.AreEqual(string.Concat("Sized =", images2.Count, " Successfully =", images2.Count, " with errors=", 0), result2);
            Assert.AreEqual(images2.Count, images2.Where(f => MultiThreadResizer.ListOfFileAndCustomResizeSettings[f] == 3).Count());

            Assert.AreEqual(CountOfImage*2, MultiThreadResizer.ListOfFileAndCustomResizeSettings.Where(f => f.Value == 3).Count());
        }
        [TestMethod]
        public void StartResizing()
        {
            int CountOfImage = 10;
            int CountOfThreads = 1;
            var MultiThreadResizer = new MultiThreadResizerWorker(CountOfThreads, CountOfImage);
            MultiThreadResizer.NameSubFolderForNewFiles = @"\NewImages\StartResizing";
            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", 0, 0, 0, 0, 0,0), MultiThreadResizer.ShortStateSummary);
            MultiThreadResizer.SetFolderWithImages(Path);
            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", CountFiles, CountFiles * 5, CountFiles * 5, 0, 0, 0), MultiThreadResizer.ShortStateSummary);
            Assert.AreEqual(
                String.Format("{0}-{1}-{2}-{3}-{4}-{5}",
                CountFiles, CountFiles * 5, CountFiles * 5 - CountOfImage* CountOfThreads, 0, CountOfImage * CountOfThreads, 0), 
                MultiThreadResizer.StartResizing(2));
        }
        [TestMethod]
        public void TaskResizing()
        {
            int CountOfImage = 10;
            int CountOfThreads = 1;
            var MultiThreadResizer = new MultiThreadResizerWorker(CountOfThreads, CountOfImage);
            MultiThreadResizer.NameSubFolderForNewFiles = @"\NewImages\TaskResizing";
            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", 0, 0, 0, 0, 0, 0), MultiThreadResizer.ShortStateSummary);
            MultiThreadResizer.SetFolderWithImages(Path);
            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", CountFiles, CountFiles * 5, CountFiles * 5, 0, 0, 0), MultiThreadResizer.ShortStateSummary);
            MultiThreadResizer.TaskResizing();
            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", CountFiles, CountFiles * 5, CountFiles * 5 - CountOfImage * CountOfThreads, 0, CountOfImage * CountOfThreads, 0), MultiThreadResizer.ShortStateSummary);
        }


        [TestMethod]
        public void StateSummary()
        {
            int CountOfImage = 10;
            var MultiThreadResizer = new MultiThreadResizerWorker(2, CountOfImage);
            MultiThreadResizer.NameSubFolderForNewFiles = @"\NewImages\StateSummary";
            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", 0, 0, 0, 0, 0, 0),MultiThreadResizer.ShortStateSummary);

            MultiThreadResizer.SetFolderWithImages(Path);

            Assert.AreEqual(String.Format("{0}-{1}-{2}-{3}-{4}-{5}", CountFiles, CountFiles *5, CountFiles * 5, 0, 0, 0),MultiThreadResizer.ShortStateSummary);
        }


    }
}

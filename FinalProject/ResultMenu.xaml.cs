using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Dicom.Imaging;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for ResultMenu.xaml
    /// </summary>
    public partial class ResultMenu : Page
    {
        private List<(string Name, string ImageClass, int ImageProbability, BitmapImage Image)> photoList = new List<(string, string, int, BitmapImage)>();

        private Dictionary<int, int> scoreOrderMap = new Dictionary<int, int>();

        private bool zooming = false;

        private int _currentPhoto;
        private int CurrentPhoto
         { 
            get
            {
                return _currentPhoto;
            }
            set
            {
                if(value >= 0 && value < photoList.Count)
                {
                    _currentPhoto = value;

                    ImgPhoto.Source = photoList[_currentPhoto].Image;
                    TxtBlockPhoto.Text = $"Photo \"{photoList[_currentPhoto].Name}\"" +
                        $" result {photoList[_currentPhoto].ImageClass}" +
                        $" with probability {photoList[_currentPhoto].ImageProbability}%";
                }        
            }
        }

        private int _currentSuspiciousPhoto;
        private int CurrentSuspiciousPhoto
        { 
            get
            {
                return _currentSuspiciousPhoto;
            }
            set
            {
                _currentSuspiciousPhoto = value;        
                CurrentPhoto = scoreOrderMap[_currentSuspiciousPhoto];
                TxtBlockCurrent.Text = $"Current high score photo: {_currentSuspiciousPhoto + 1}";

                if (_currentSuspiciousPhoto == 0)
                {
                    BtnPreviousPhoto.IsEnabled = false;
                }
                else if(!BtnPreviousPhoto.IsEnabled)
                {
                    BtnPreviousPhoto.IsEnabled = true;
                }

                if(_currentSuspiciousPhoto == photoList.Count - 1)
                {
                    BtnNextPhoto.IsEnabled = false;
                }
                else if (!BtnNextPhoto.IsEnabled)
                {
                    BtnNextPhoto.IsEnabled = true;
                }
            }
        }

        private string pathToFolder;

        private Process pyProcess;

        public ResultMenu(string path)
        {
            InitializeComponent();
            pathToFolder = path;
        }
        void OnLoad(object sender, RoutedEventArgs e)
        {
            var args = $"\"{pathToFolder}\" \"{Path.GetFullPath(ConfigurationManager.AppSettings["databasePath"])}\" \"{Path.GetFullPath(ConfigurationManager.AppSettings["metadataPath"])}\"";
            pyProcess = PythonProcessHelper.GetProcess(ConfigurationManager.AppSettings["classifyScriptPath"], args, this);
            
            if(pyProcess == null)
            {
                return;
            }

            ReadOutput();
        }

        private void ReadOutput()
        {
            string outputReceived;
            string prev;
            while ((outputReceived = pyProcess.StandardOutput.ReadLine()) != null)
            {
               
                if (outputReceived.StartsWith("*"))
                {
                    var window = new ErrorWindow(outputReceived, this, pathToFolder);
                    window.ShowDialog();

                    if (window.TargetPage != null)
                    {
                        App.ParentWindowRef.ParentFrame.Navigate(window.TargetPage);
                        return;
                    }    
                }

                var outputs = outputReceived.Split(';');

                if (outputs[2] != "")
                {
                    photoList.Add((
                        outputs[0],
                        outputs[1],
                        (int)Math.Round((float.Parse(outputs[2]) * 100)),
                        ConvertBitmap(new DicomImage(Path.Combine(pathToFolder, outputs[0])).RenderImage().AsClonedBitmap())));
                }
                else
                {
                    TxtBlockPatient.Text = $"Patient result {outputs[0]} with probability of {(int)Math.Round((float.Parse(outputs[1]) * 100))}%";

                    var ordered = photoList.Where(x => x.ImageClass == "positive").OrderBy(x => x.ImageProbability).Reverse().ToList();
                    ordered = ordered.Concat(photoList.Where(x => x.ImageClass == "negative").OrderBy(x => x.ImageProbability)).ToList();

                    ordered.ForEach(x => scoreOrderMap.Add(ordered.IndexOf(x), photoList.IndexOf(x)));

                    CurrentSuspiciousPhoto = 0;
                    return;
                }
                prev = outputReceived;
            }

            if(pyProcess.HasExited)
            {
                PythonProcessHelper.ReadErrors(pyProcess, this);
                return;
            }
        }

        public BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();

            return image;
        }

        private void BtnGoToMainManuClick(object sender, RoutedEventArgs e)
        {
            pyProcess.StandardInput.WriteLine("none");
            pyProcess.WaitForExit();
            App.ParentWindowRef.ParentFrame.Navigate(new MainMenu());
        }

        private void BtnSaveResultClick(object sender, RoutedEventArgs e)
        {           
            var window = new ConfirmSaveResultWindow();
            var result = window.ShowDialog();

            if (result != null)
            {
                pyProcess.StandardInput.WriteLine((bool)result ? "positive" : "negative");
                pyProcess.StandardInput.Flush();
                pyProcess.WaitForExit();
                App.ParentWindowRef.ParentFrame.Navigate(new MainMenu());
            }
        }

        private void BtnPreviousPhotoClick(object sender, RoutedEventArgs e)
        {
            CurrentSuspiciousPhoto -= 1;
        }

        private void BtnNextPhotoClick(object sender, RoutedEventArgs e)
        {
            CurrentSuspiciousPhoto += 1;
        }

        private void ResultMenuMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if(zooming)
            {
                zooming = false;
                return;
            }

            if (e.Delta > 0)
            {
                CurrentPhoto += 1;
            }
            else if (e.Delta < 0)
            {
                CurrentPhoto -= 1;
            }
        }

        private void ImgMouseWheel(object sender, MouseWheelEventArgs e)
        {
            zooming = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ExpImageProcessing.Helpers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Rectangle = Windows.UI.Xaml.Shapes.Rectangle;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ExpImageProcessing
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        CameraCapture _cameraCapture = new CameraCapture();
        private const string previewStartDescription = "Start Preview";
        private const string previewStopDescription = "Stop Preview";
        private FaceDetector _faceDetector;
        private FaceTracker _faceTracker;

        private BitmapPixelFormat _faceDectorSupportedPixelFormat;
        private BitmapPixelFormat _faceTrackerSupportedPixelFormat;

        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
        private readonly double lineThickness = 2.0;
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        private List<ImageInfo> _capturedList = new List<ImageInfo>();


        public MainPage()
        {
            InitializeComponent();
            _cameraCapture.IsInitialized = false;
            UpdateUi();
        }

        private void UpdateUi()
        {
            ButtonPreview.Content = _cameraCapture.IsPreviewActive ? previewStopDescription : previewStartDescription;
        }

        private async void ButtonPreview_Click(object sender, RoutedEventArgs e)
        {

            await _cameraCapture.Initialize(CaptureElementPreview);
            await InitializeFaceDetection();
            if (_cameraCapture.IsInitialized)
            {
                await UpdatePreviewState();
                UpdateUi();
            }
            else
            {
                Debug.WriteLine("Video capture device could not be initialized");
            }
        }

        private async Task InitializeFaceDetection()
        {
            if (FaceDetector.IsSupported)
            {
                if (_faceDetector == null)
                {
                    _faceDetector = await FaceDetector.CreateAsync();
                    _faceDectorSupportedPixelFormat = FaceDetector.GetSupportedBitmapPixelFormats().FirstOrDefault();
                }
            }
            else
            {
                Debug.WriteLine("Face detection is not supported");
            }

            if (FaceTracker.IsSupported)
            {
                if (_faceTracker == null)
                {
                    _faceTracker = await FaceTracker.CreateAsync();
                    _faceTrackerSupportedPixelFormat = FaceTracker.GetSupportedBitmapPixelFormats().FirstOrDefault();
                }

            }
            else
            {
                Debug.WriteLine("Face tracking is not suppoted");
            }

        }

        private async Task<IList<DetectedFace>> DetectFaces(SoftwareBitmap inputbitMapBitmap)
        {
            var conertedIfrequired = inputbitMapBitmap;
            if (!FaceDetector.IsBitmapPixelFormatSupported(inputbitMapBitmap.BitmapPixelFormat))
            {
                conertedIfrequired = SoftwareBitmap.Convert(inputbitMapBitmap, _faceDectorSupportedPixelFormat);
            }
            return await _faceDetector.DetectFacesAsync(conertedIfrequired);
        }

        private async Task UpdatePreviewState()
        {
            if (!_cameraCapture.IsPreviewActive)
            {
                await _cameraCapture.Start();
                BeginTracking();
            }
            else
            {
                await _cameraCapture.Stop();
                CanvasFaceDisplay.Children.Clear();
            }

        }

        private void BeginTracking()
        {
            if (_faceTracker != null)
            {
#pragma warning disable 4014
                Task.Run(async () =>
                    {
                        while (_cameraCapture.IsPreviewActive)
                        {
                            await ProcessVideoFrame();
                        }
                    }
                );
#pragma warning restore 4014

            }


        }

        private async Task ProcessVideoFrame()
        {
            using (VideoFrame videoFrame = new VideoFrame(_faceTrackerSupportedPixelFormat, (int)_cameraCapture.FrameWidth, (int)_cameraCapture.FrameHeight))
            {
                await _cameraCapture.MediaCapture.GetPreviewFrameAsync(videoFrame);
                var faces = await _faceTracker.ProcessNextFrameAsync(videoFrame);
                DisplayFaces(videoFrame.SoftwareBitmap, faces);

            }


        }

        private async void DisplayFaces(SoftwareBitmap displayBitMap, IList<DetectedFace> faces)
        {
            if (Dispatcher.HasThreadAccess)
            {
                var xScalingFactor = CanvasFaceDisplay.ActualWidth / displayBitMap.PixelWidth;
                var yScalingFactor = CanvasFaceDisplay.ActualHeight / displayBitMap.PixelHeight;
                CanvasFaceDisplay.Children.Clear();
                foreach (var detectedFace in faces)
                {
                    DrawFaceBox(detectedFace.FaceBox, xScalingFactor, yScalingFactor);
                }
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    DisplayFaces(displayBitMap, faces);
                });
            }

        }

        private void DrawFaceBox(BitmapBounds faceBox, double xScalingFactor, double yScalingFactor)
        {
            var rectangle = new Rectangle
            {
                Stroke = new SolidColorBrush(Colors.Yellow),
                StrokeThickness = 5,
                Width = (faceBox.Width + 50) * xScalingFactor,
                Height = (faceBox.Height + 50) * yScalingFactor

            };

            var translateTransform = new TranslateTransform()
            {
                X = faceBox.X * xScalingFactor,
                Y = faceBox.Y * yScalingFactor
            };

            rectangle.RenderTransform = translateTransform;
            CanvasFaceDisplay.Children.Add(rectangle);

        }

        private async void ButtonDetectFaces_Click(object sender, RoutedEventArgs e)
        {
            if (_faceDetector != null)
            {
                var inputBitMap = await _cameraCapture.CapturePhotoToSoftwareBitMap();
                var facesDetected = await DetectFaces(inputBitMap);
                 //DisplayFaceLocations(facesDetected);
                ShowDetectedFaces(inputBitMap, facesDetected);
            }


        }

        //private void DisplayFaceLocations(IList<DetectedFace> facesDetected)
        //{

        //    for (int i = 0; i < facesDetected.Count; i++)
        //    {
        //        var detectedFace = facesDetected[i];
        //        var detectedFaceLocations = DetectedFaceToString(i + 1, detectedFace.FaceBox);
        //        AddItemToList(detectedFaceLocations);

        //    }

        //}

        //private void AddItemToList(string detectedFaceLocations)
        //{
        //    if (ListBoxInfo.Items != null)
        //    {
        //        ListBoxInfo.Items.Add(detectedFaceLocations);
        //        ListBoxInfo.SelectedIndex = ListBoxInfo.Items.Count - 1;
        //    }
        //    else
        //    {
        //        Debug.WriteLine("Somethign wrong with ListBox");
        //    }
        //}

        //private string DetectedFaceToString(int index, BitmapBounds detectedFaceFaceBox)
        //{
        //    return
        //        $"Face no: {index}. X: {detectedFaceFaceBox.X}, Y: {detectedFaceFaceBox.Y}, Width: {detectedFaceFaceBox.Width}. Height: {detectedFaceFaceBox.Height}";

        //}



        public async Task<StorageFile> CapturePhoto()
        {
            // Create storage file in local app storage
            string fileName = GenerateNewFileName() + ".jpg";
            CreationCollisionOption collisionOption = CreationCollisionOption.GenerateUniqueName;
            StorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, collisionOption);

            // Captures and stores new Jpeg image file
            await _cameraCapture.MediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

            // Return image file
            return file;
        }

        /// <summary>
        /// Generates unique file name based on current time and date. Returns value as string.
        /// </summary>
        private string GenerateNewFileName()
        {
            return DateTime.UtcNow.ToString("yyyy.MMM.dd HH-mm-ss") + " PhotoCapture";
        }




        private void ButtonClearInfo_Click(object sender, RoutedEventArgs e)
        {

            _capturedList.Clear();
            ListBoxInfo.ItemsSource = null;

        }




        private async void ShowDetectedFaces(SoftwareBitmap sourceBitmap, IList<DetectedFace> detectedFaces)
        {
            ImageBrush brush = new ImageBrush();
            SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();

            //  const BitmapPixelFormat faceDetectionPixelFormat = BitmapPixelFormat.Bgra8;

            SoftwareBitmap convertedBitmap;

            // if (sourceBitmap.BitmapPixelFormat != faceDetectionPixelFormat)
            {
                convertedBitmap = SoftwareBitmap.Convert(sourceBitmap, BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
            }
            //else
            {
                //  convertedBitmap = sourceBitmap;
            }
            // convertedBitmap.BitmapAlphaMode= BitmapAlphaMode.Ignore;

            await bitmapSource.SetBitmapAsync(convertedBitmap);
            brush.ImageSource = bitmapSource;
            //  brush.Stretch = Stretch.Fill;
            //this.VisualizationCanvas.Background = brush;

            if (detectedFaces != null)
            {
                // double widthScale = convertedBitmap.PixelWidth / this.VisualizationCanvas.ActualWidth;
                // double heightScale = convertedBitmap.PixelHeight / this.VisualizationCanvas.ActualHeight;
                _capturedList = new List<ImageInfo>();
                foreach (DetectedFace face in detectedFaces)
                {
                    var storateFile = await CapturePhoto();
                    // var result = await GetCroppedBitmapAsync(storateFile,
                    //     face.FaceBox.X, face.FaceBox.Y, face.FaceBox.Width , face.FaceBox.Height , 1);
                    // _capturedList.Add(new ImageInfo { Name = result.DisplayName, Path = result.Path });

                    //var result = await GetCroppedBitmapAsync1(storateFile, face.FaceBox.X-10, face.FaceBox.Y + 10, face.FaceBox.Width + 20, face.FaceBox.Height + 10, 1);
                    var result = await GetCroppedBitmapAsync1(storateFile, face.FaceBox.X - 20, face.FaceBox.Y + 20, face.FaceBox.Width + 30, face.FaceBox.Height + 20, 1);


                }
                // ListBoxInfo.ItemsSource = _capturedList;

            }
        }

        public async Task<StorageFile> GetCroppedBitmapAsync1(StorageFile savedStorageFile,
            uint startPointX, uint startPointY, uint width, uint height, double scale)
        {
            using (IRandomAccessStream stream = await savedStorageFile.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                uint scaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale);
                uint scaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale);

                BitmapTransform transform = new BitmapTransform();
                BitmapBounds bounds = new BitmapBounds();
                bounds.X = startPointX;
                bounds.Y = startPointY;
                bounds.Height = height;
                bounds.Width = width;
                transform.Bounds = bounds;

                PixelDataProvider pix = await decoder.GetPixelDataAsync(
      BitmapPixelFormat.Bgra8,
      BitmapAlphaMode.Straight,
      transform,
      ExifOrientationMode.IgnoreExifOrientation,
      ColorManagementMode.ColorManageToSRgb);


                byte[] pixels = pix.DetachPixelData();


                // Stream the bytes into a WriteableBitmap 
                WriteableBitmap cropBmp = new WriteableBitmap((int)width, (int)height);

                Stream pixStream = cropBmp.PixelBuffer.AsStream();
                pixStream.Write(pixels, 0, (int)(width * height * 4));

                var fileName = Guid.NewGuid() + "Bps1.jpg";
                Guid bitMapEncoderGuid = BitmapEncoder.JpegEncoderId;
                var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName,
                    CreationCollisionOption.GenerateUniqueName);
                using (var irStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // var bmenc = await BitmapEncoder.CreateAsync(bitMapEncoderGuid, irStream);
                    var bmenc = await BitmapEncoder.CreateForTranscodingAsync(irStream, decoder);
                    Stream sstrm = cropBmp.PixelBuffer.AsStream();
                    byte[] pxls = new byte[sstrm.Length];
                    await sstrm.ReadAsync(pxls, 0, pxls.Length);
                    bmenc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)cropBmp.PixelWidth,
                        (uint)cropBmp.PixelHeight, scaledWidth, scaledHeight, pxls);
                    await bmenc.FlushAsync();
                }
                return file;
            }
        }

        public async Task<StorageFile> GetCroppedBitmapAsync(StorageFile savedStorageFile,
        uint startPointX, uint startPointY, uint width, uint height, double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale))
            {
                scale = 1;
            }
            using (IRandomAccessStream stream = await savedStorageFile.OpenReadAsync())
            {


                // Create a decoder from the stream. With the decoder, we can get  
                // the properties of the image. 
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                // The scaledSize of original image. 
                uint scaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale);
                uint scaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale);



                // Refine the start point and the size.  
                /*   if (startPointX + width > scaledWidth)
                   {
                       startPointX = scaledWidth - width;
                   }

                   if (startPointY + height > scaledHeight)
                   {
                       startPointY = scaledHeight - height;
                   }
   */

                // Create cropping BitmapTransform and define the bounds. 
                BitmapTransform transform = new BitmapTransform();
                BitmapBounds bounds = new BitmapBounds();
                bounds.X = startPointX;
                bounds.Y = startPointY;
                bounds.Height = height;
                bounds.Width = width;
                transform.Bounds = bounds;


                // transform.ScaledWidth = 100;
                // transform.ScaledHeight = 100;

                // Get the cropped pixels within the bounds of transform. 
                PixelDataProvider pix = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);
                //new
                var originalPixelWidth = decoder.PixelWidth;
                var originalPixelHeight = decoder.PixelHeight;
                //

                byte[] pixels = pix.DetachPixelData();


                // Stream the bytes into a WriteableBitmap 
                WriteableBitmap cropBmp = new WriteableBitmap((int)width, (int)height);
                Stream pixStream = cropBmp.PixelBuffer.AsStream();
                pixStream.Write(pixels, 0, (int)(width * height * 4));

                var fileName = Guid.NewGuid() + "Bps1.jpeg";
                Guid bitMapEncoderGuid = BitmapEncoder.JpegEncoderId;
                var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName,
                    CreationCollisionOption.GenerateUniqueName);

                using (var irStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // var bmenc = await BitmapEncoder.CreateAsync(bitMapEncoderGuid, irStream);
                    var bmenc = await BitmapEncoder.CreateForTranscodingAsync(irStream, decoder);
                    Stream sstrm = cropBmp.PixelBuffer.AsStream();
                    byte[] pxls = new byte[sstrm.Length];
                    await sstrm.ReadAsync(pxls, 0, pxls.Length);
                    bmenc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)cropBmp.PixelWidth,
                        (uint)cropBmp.PixelHeight, originalPixelWidth, originalPixelHeight, pxls);
                    await bmenc.FlushAsync();


                    /*  var requestedMinSide = height;
                      var encoder = await BitmapEncoder.CreateForTranscodingAsync(irStream, decoder);
                      double widthRatio = (double)requestedMinSide / originalPixelWidth;
                      double heightRatio = (double)requestedMinSide / originalPixelHeight;
                      uint aspectHeight = (uint)requestedMinSide;
                      uint aspectWidth = (uint)requestedMinSide;

                      bounds.Height = aspectHeight;
                      bounds.Width = aspectWidth;

                      uint cropX = 0, cropY = 0;
                      var scaledSize = (uint)requestedMinSide;
                      if (originalPixelWidth > originalPixelHeight)
                      {
                          aspectWidth = (uint)(heightRatio * originalPixelWidth);
                          cropX = (aspectWidth - aspectHeight) / 2;
                      }
                      else
                      {
                          aspectHeight = (uint)(widthRatio * originalPixelHeight);
                          cropY = (aspectHeight - aspectWidth) / 2;
                      }

                      //you can adjust interpolation and other options here, so far linear is fine for thumbnails
                      encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;
                      encoder.BitmapTransform.ScaledHeight = aspectHeight;
                      encoder.BitmapTransform.ScaledWidth = aspectWidth;
                      encoder.BitmapTransform.Bounds = bounds;
                          await encoder.FlushAsync();*/
                }
                return file;
            }
        }

        private async void ButtonUpload_Click(object sender, RoutedEventArgs e)
        {

            var storageCred = new StorageCredentials("", "");

            // CloudStorageAccount stroAccount = new CloudStorageAccount(storageCred,true);
            CloudBlobContainer blobContainer =
                new CloudBlobContainer(new Uri(""), storageCred);


            //blobContainer.CreateIfNotExistsAsync()
            foreach (var imageInfo in _capturedList)
            {
                var blob = blobContainer.GetBlockBlobReference(imageInfo.Name);
                using (var strem = File.Open(imageInfo.Path, FileMode.Open))
                {
                    await blob.UploadFromStreamAsync(strem);
                }
            }

            ButtonClearInfo_Click(null, null);
        }
    }
}
